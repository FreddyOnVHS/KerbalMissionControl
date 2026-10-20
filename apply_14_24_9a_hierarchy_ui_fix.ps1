$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$path = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $path)) { throw 'Run from KMC repository root.' }
$source = [IO.File]::ReadAllText($path)
$startAnchor = '        // Hierarchy uses live KSP ParentName relationships.'
$endAnchor = '        private static OrbitMapBody FindBody('
$start = $source.IndexOf($startAnchor)
$end = $source.IndexOf($endAnchor, [Math]::Max(0,$start))
if ($start -lt 0 -or $end -le $start) { throw 'Expected 14.24.9 hierarchy not found. No changes made.' }
if ($source.Contains('14.24.9a compact destination rows')) { throw '14.24.9a already installed.' }
$replacement = @'
        // 14.24.9a compact destination rows. All bodies remain visible, and
        // ParentName defines the hierarchy, including modded solar systems.
        private void DrawTransferHierarchy(MissionRenderContext context,
            OrbitMapPacket packet, string origin, Rectangle panel)
        {
            List<OrbitMapBody> bodies = packet.Bodies;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int row = 0;
            int top = panel.Top + 100;
            int lineHeight = 27;
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
                Math.Max(context.SmallFont.SizeInPoints + 2.0f, 12.0f), FontStyle.Bold))
            using (Font headingFont = new Font(context.SmallFont.FontFamily,
                Math.Max(context.SmallFont.SizeInPoints + 2.5f, 12.5f), FontStyle.Bold))
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
                            context.Graphics.DrawString("SYSTEM  " + roots[i],
                                headingFont, brush, header.Left, header.Top + 2);
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
            int indent = Math.Min(depth, 8) * 27;
            int x = panel.Left + 20 + indent;
            int y = top + row * lineHeight;
            bool current = string.Equals(body.Name, origin, StringComparison.OrdinalIgnoreCase);
            bool selected = string.Equals(body.Name, _selectedTransferBodyName, StringComparison.OrdinalIgnoreCase);
            string label = (depth == 0 ? "" : "\u2514\u2500 ") + body.Name +
                (current ? "   [CURRENT ORBIT]" : "");
            // Button hitbox fits the label rather than spanning the entire pane.
            int measured = (int)Math.Ceiling(context.Graphics.MeasureString(label, treeFont).Width);
            int width = Math.Min(Math.Max(115, measured + 18), Math.Max(115, panel.Right - x - 20));
            Rectangle button = new Rectangle(x, y, width, lineHeight - 2);
            Color color = selected ? context.PhosphorColor : context.DimPhosphorColor;
            if (selected)
            {
                using (SolidBrush tint = new SolidBrush(Color.FromArgb(35, color)))
                    context.Graphics.FillRectangle(tint, button);
                using (Pen pen = new Pen(color, 1.3f))
                    context.Graphics.DrawRectangle(pen, button);
            }
            using (SolidBrush brush = new SolidBrush(color))
                context.Graphics.DrawString(label, treeFont, brush,
                    button.Left + 6, button.Top + 2);
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
# Use ASCII branch marks for guaranteed GDI+ font coverage on older Windows.
$replacement = $replacement.Replace('"\u2514\u2500 "', '"-- "')
$backup = Join-Path $env:TEMP ('KMC_14_24_9a_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $path (Join-Path $backup 'MapPage.cs')
$updated = $source.Substring(0,$start) + $replacement + $source.Substring($end)
[IO.File]::WriteAllText($path, $updated, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.9a compact hierarchy UI applied.'
Write-Host "Backup: $backup"
Write-Host 'MissionControl only; KSP plugin DLL replacement NOT needed.'
