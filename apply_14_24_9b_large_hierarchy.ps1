$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$file = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $file)) { throw 'Run from the KMC repository root.' }
$source = [IO.File]::ReadAllText($file)
$startAnchor = '        // 14.24.9a compact destination rows.'
$endAnchor = '        private static OrbitMapBody FindBody('
$start = $source.IndexOf($startAnchor)
$end = $source.IndexOf($endAnchor, [Math]::Max(0, $start))
if ($start -lt 0 -or $end -le $start) { throw 'Expected 14.24.9a hierarchy not found. Nothing changed.' }
if ($source.Contains('14.24.9b large aligned destination rows')) { throw '14.24.9b already applied.' }
$replacement = @'
        // 14.24.9b large aligned destination rows. All bodies remain visible, and
        // ParentName defines the hierarchy, including modded solar systems.
        private void DrawTransferHierarchy(MissionRenderContext context,
            OrbitMapPacket packet, string origin, Rectangle panel)
        {
            List<OrbitMapBody> bodies = packet.Bodies;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int row = 0;
            int top = panel.Top + 100;
            // Use available panel height: the stock system occupies most of the left pane.
            // Compact automatically if a modded catalog contains additional entries.
            int lineHeight = Math.Min(40, Math.Max(26,
                (panel.Bottom - top - 12) / Math.Max(1, bodies.Count + 1)));
            int maximumRows = Math.Max(0, (panel.Bottom - top - 8) / lineHeight);
            List<string> roots = new List<string>();
            for (int i = 0; i < bodies.Count; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body == null || string.IsNullOrWhiteSpace(body.Name)) continue;
                string root = string.IsNullOrWhiteSpace(body.ParentName)
                    ? body.Name : (FindBody(bodies, body.ParentName) == null
                        ? body.ParentName : null);
                if (root != null && !roots.Exists(
                    item => string.Equals(item, root, StringComparison.OrdinalIgnoreCase)))
                    roots.Add(root);
            }
            roots.Sort(StringComparer.OrdinalIgnoreCase);
            using (Font treeFont = new Font(context.SmallFont.FontFamily,
                Math.Max(15.0f, Math.Min(19.0f, lineHeight * 0.45f)), FontStyle.Bold))
            using (Font headingFont = new Font(context.SmallFont.FontFamily,
                Math.Max(15.5f, Math.Min(19.5f, lineHeight * 0.48f)), FontStyle.Bold))
            {
                for (int i = 0; i < roots.Count && row < maximumRows; i++)
                {
                    OrbitMapBody rootBody = FindBody(bodies, roots[i]);
                    if (rootBody != null)
                        DrawHierarchyBody(context, bodies, rootBody, origin, panel,
                            0, top, lineHeight, maximumRows, ref row, visited, treeFont);
                    else
                    {
                        Rectangle header = new Rectangle(panel.Left + 18,
                            top + row * lineHeight, panel.Width - 36, lineHeight);
                        using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
                        using (StringFormat format = new StringFormat())
                        {
                            format.LineAlignment = StringAlignment.Center;
                            context.Graphics.DrawString("SYSTEM  " + roots[i],
                                headingFont, brush, header, format);
                        }
                        row++;
                        DrawHierarchyChildren(context, bodies, roots[i], origin, panel,
                            1, top, lineHeight, maximumRows, ref row, visited, treeFont);
                    }
                }
                for (int i = 0; i < bodies.Count && row < maximumRows; i++)
                    if (bodies[i] != null && !string.IsNullOrWhiteSpace(bodies[i].Name) &&
                        !visited.Contains(bodies[i].Name))
                        DrawHierarchyBody(context, bodies, bodies[i], origin, panel,
                            0, top, lineHeight, maximumRows, ref row, visited, treeFont);
            }
        }

        private void DrawHierarchyChildren(MissionRenderContext context,
            List<OrbitMapBody> bodies, string parent, string origin,
            Rectangle panel, int depth, int top, int lineHeight, int maxRows,
            ref int row, HashSet<string> visited, Font treeFont)
        {
            for (int i = 0; i < bodies.Count && row < maxRows; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body != null && !string.IsNullOrWhiteSpace(body.Name) &&
                    string.Equals(body.ParentName, parent, StringComparison.OrdinalIgnoreCase))
                    DrawHierarchyBody(context, bodies, body, origin, panel,
                        depth, top, lineHeight, maxRows, ref row, visited, treeFont);
            }
        }

        private void DrawHierarchyBody(MissionRenderContext context,
            List<OrbitMapBody> bodies, OrbitMapBody body, string origin,
            Rectangle panel, int depth, int top, int lineHeight, int maxRows,
            ref int row, HashSet<string> visited, Font treeFont)
        {
            if (row >= maxRows || !visited.Add(body.Name)) return;
            int indent = Math.Min(depth, 8) * 48;
            int x = panel.Left + 20 + indent;
            int y = top + row * lineHeight;
            bool current = string.Equals(body.Name, origin, StringComparison.OrdinalIgnoreCase);
            bool selected = string.Equals(body.Name, _selectedTransferBodyName, StringComparison.OrdinalIgnoreCase);
            string label = body.Name +
                (current ? "   [CURRENT ORBIT]" : "");
            // Button hitbox fits the label rather than spanning the entire pane.
            int measured = (int)Math.Ceiling(context.Graphics.MeasureString(label, treeFont).Width);
            int width = Math.Min(Math.Max(180, measured + 44), Math.Max(1, panel.Right - x - 20));
            Rectangle button = new Rectangle(x, y + 2, width, Math.Max(1, lineHeight - 4));
            Color color = selected || current ? context.PhosphorColor : context.DimPhosphorColor;
            if (selected)
            {
                using (SolidBrush tint = new SolidBrush(Color.FromArgb(35, color)))
                    context.Graphics.FillRectangle(tint, button);
                using (Pen pen = new Pen(color, 1.3f))
                    context.Graphics.DrawRectangle(pen, button);
            }
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                Rectangle textRect = Rectangle.Inflate(button, -16, 0);
                context.Graphics.DrawString(label, treeFont, brush, textRect, format);
            }
            if (!current)
            {
                _transferBodyButtons.Add(button);
                _transferBodyNames.Add(body.Name);
            }
            row++;
            DrawHierarchyChildren(context, bodies, body.Name, origin, panel,
                depth + 1, top, lineHeight, maxRows, ref row, visited, treeFont);
        }

'@
$backup = Join-Path $env:TEMP ('KMC_14_24_9b_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $file (Join-Path $backup 'MapPage.cs')
$updated = $source.Substring(0,$start) + $replacement + $source.Substring($end)
[IO.File]::WriteAllText($file, $updated, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.9b larger hierarchy layout applied.'
Write-Host "Backup: $backup"
Write-Host 'Mission Control only. No KSP DLL replacement.'
