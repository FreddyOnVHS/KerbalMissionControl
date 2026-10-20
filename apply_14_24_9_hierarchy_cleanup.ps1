$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$mapPath = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
$mnvPath = Join-Path $root 'KMC.MissionControl\Pages\ManeuverPage.cs'
if (!(Test-Path $mapPath) -or !(Test-Path $mnvPath)) { throw 'Run from the KMC repository root.' }
$map = [IO.File]::ReadAllText($mapPath)
$mnv = [IO.File]::ReadAllText($mnvPath)
if ($map.Contains('private void DrawTransferHierarchy(')) { throw '14.24.9 already applied.' }
$gridStart = '            int columns = 3;'
$gridEnd = '            int x = plannerPanel.Left + 12;'
$start = $map.IndexOf($gridStart)
$end = $map.IndexOf($gridEnd, $start)
if ($start -lt 0 -or $end -lt $start) { throw 'MAP destination grid anchors not found. No changes made.' }
$helperAnchor = '        private static OrbitMapBody FindBody('
if (!$map.Contains($helperAnchor)) { throw 'Hierarchy helper anchor missing. No changes made.' }
$objectiveOld = '"INTERPLANETARY TRANSFER TO " +'
if (!$map.Contains($objectiveOld)) { throw 'Objective anchor missing. No changes made.' }
$peOld = '"PREDICTED PE", FormatDistance(plan.PredictedPeriapsisMeters)'
if (!$mnv.Contains($peOld)) { throw 'MNV periapsis anchor missing. No changes made.' }
$backup = Join-Path $env:TEMP ('KMC_14_24_9_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $mapPath (Join-Path $backup 'MapPage.cs')
Copy-Item $mnvPath (Join-Path $backup 'ManeuverPage.cs')
$map = $map.Substring(0,$start) + '            DrawTransferHierarchy(context, packet, origin, systemPanel);' + [Environment]::NewLine + [Environment]::NewLine + $map.Substring($end)
$hierarchy = @'
        // Hierarchy uses live KSP ParentName relationships. No stock-body names,
        // collapse state, or alternate transfer-route logic.
        private void DrawTransferHierarchy(MissionRenderContext context,
            OrbitMapPacket packet, string origin, Rectangle panel)
        {
            List<OrbitMapBody> bodies = packet.Bodies;
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int row = 0;
            int top = panel.Top + 106;
            int lineHeight = 27;
            int maximumRows = Math.Max(0, (panel.Bottom - top - 9) / lineHeight);
            // A parent absent from the bounded catalog is drawn as a header.
            List<string> roots = new List<string>();
            for (int i = 0; i < bodies.Count; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body == null || string.IsNullOrWhiteSpace(body.Name)) continue;
                if (string.IsNullOrWhiteSpace(body.ParentName))
                {
                    if (!roots.Contains(body.Name)) roots.Add(body.Name);
                }
                else if (FindBody(bodies, body.ParentName) == null)
                {
                    if (!roots.Contains(body.ParentName)) roots.Add(body.ParentName);
                }
            }
            roots.Sort(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < roots.Count && row < maximumRows; i++)
            {
                OrbitMapBody rootBody = FindBody(bodies, roots[i]);
                if (rootBody != null)
                    DrawHierarchyBody(context, bodies, rootBody, origin, panel,
                        0, top, lineHeight, maximumRows, ref row, visited);
                else
                {
                    Rectangle header = new Rectangle(panel.Left + 12,
                        top + row * lineHeight, panel.Width - 24, lineHeight - 2);
                    using (SolidBrush brush = new SolidBrush(context.PhosphorColor))
                        context.Graphics.DrawString("[SYSTEM] " + roots[i],
                            context.SmallFont, brush, header.Location);
                    row++;
                    DrawHierarchyChildren(context, bodies, roots[i], origin, panel,
                        1, top, lineHeight, maximumRows, ref row, visited);
                }
            }
            // Handle orphan/cyclic catalog entries without losing destinations.
            for (int i = 0; i < bodies.Count && row < maximumRows; i++)
                if (bodies[i] != null && !string.IsNullOrWhiteSpace(bodies[i].Name) &&
                    !visited.Contains(bodies[i].Name))
                    DrawHierarchyBody(context, bodies, bodies[i], origin, panel,
                        0, top, lineHeight, maximumRows, ref row, visited);
        }

        private void DrawHierarchyChildren(MissionRenderContext context,
            List<OrbitMapBody> bodies, string parent, string origin,
            Rectangle panel, int depth, int top, int lineHeight, int maxRows,
            ref int row, HashSet<string> visited)
        {
            for (int i = 0; i < bodies.Count && row < maxRows; i++)
            {
                OrbitMapBody body = bodies[i];
                if (body != null && !string.IsNullOrWhiteSpace(body.Name) &&
                    string.Equals(body.ParentName, parent, StringComparison.OrdinalIgnoreCase))
                    DrawHierarchyBody(context, bodies, body, origin, panel,
                        depth, top, lineHeight, maxRows, ref row, visited);
            }
        }

        private void DrawHierarchyBody(MissionRenderContext context,
            List<OrbitMapBody> bodies, OrbitMapBody body, string origin,
            Rectangle panel, int depth, int top, int lineHeight, int maxRows,
            ref int row, HashSet<string> visited)
        {
            if (row >= maxRows || !visited.Add(body.Name)) return;
            int indent = Math.Min(depth, 8) * 24;
            Rectangle r = new Rectangle(panel.Left + 12 + indent,
                top + row * lineHeight,
                Math.Max(95, panel.Width - 26 - indent), lineHeight - 3);
            bool current = string.Equals(body.Name, origin, StringComparison.OrdinalIgnoreCase);
            bool selected = string.Equals(body.Name, _selectedTransferBodyName, StringComparison.OrdinalIgnoreCase);
            string prefix = depth == 0 ? "" : "+-- ";
            string label = prefix + body.Name + (current ? "  [CURRENT ORBIT]" : "");
            Color color = selected ? context.PhosphorColor : context.DimPhosphorColor;
            if (selected)
                using (Pen pen = new Pen(color, 1.5f))
                    context.Graphics.DrawRectangle(pen, r);
            using (SolidBrush brush = new SolidBrush(color))
                context.Graphics.DrawString(label, context.SmallFont, brush,
                    r.Left + 5, r.Top + 3);
            if (!current)
            {
                _transferBodyButtons.Add(r);
                _transferBodyNames.Add(body.Name);
            }
            row++;
            DrawHierarchyChildren(context, bodies, body.Name, origin, panel,
                depth + 1, top, lineHeight, maxRows, ref row, visited);
        }
'@
$map = $map.Replace($helperAnchor, $hierarchy + [Environment]::NewLine + $helperAnchor)
$map = $map.Replace('"INTERPLANETARY TRANSFER TO " +', '(packet.PlanId != null && packet.PlanId.StartsWith("MAP-MOON-", StringComparison.OrdinalIgnoreCase) ? "MOON TRANSFER TO " : "INTERPLANETARY TRANSFER TO ") +')
$mnv = $mnv.Replace($peOld, '(Safe(plan.Objective).StartsWith("MOON TRANSFER TO ", StringComparison.OrdinalIgnoreCase) ? "TARGET PE RADIUS" : "PREDICTED PE"), FormatDistance(plan.PredictedPeriapsisMeters)')
[IO.File]::WriteAllText($mapPath,$map,(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText($mnvPath,$mnv,(New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.9 hierarchy and moon maneuver labels applied.'
Write-Host "Backup: $backup"
Write-Host 'Mission Control only; KSP DLLs do not need replacement.'
