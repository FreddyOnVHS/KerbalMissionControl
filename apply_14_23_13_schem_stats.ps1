# KMC 14.23.13 - UI-only SCHEM page layout and telemetry stats.
# Run at repo root. No KSP DLL or transport changes.
$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'KMC.MissionControl\Pages\SchematicPage.cs'
if (-not (Test-Path -LiteralPath $path)) { throw "SchematicPage.cs not found: $path" }
$original = [System.IO.File]::ReadAllText($path)
if ($original.Contains('14.23.13 VESSEL STATS')) { throw '14.23.13 already applied.' }
$updated = $original
function Replace-Once([string]$old, [string]$new) {
    $script:updated = $script:updated.Replace($old, $new)
    $count = ([regex]::Matches($original, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected one anchor, found $count : $($old.Substring(0,[Math]::Min(70,$old.Length)))" }
}
Replace-Once '(int)(content.Width * 0.22)' '(int)(content.Width * 0.30)'
Replace-Once '"SCHEMATIC STATUS");' '"VESSEL INFORMATION");'
# Pass live flight telemetry through existing status rendering (including invalid snapshot paths).
Replace-Once 'DrawStatus(context, statusBounds, null, DateTime.MinValue, 0, 0);' 'DrawStatus(context, statusBounds, null, DateTime.MinValue, 0, 0, telemetry);'
Replace-Once @'
                    DateTime.MinValue,
                    0,
                    0);
'@ @'
                    DateTime.MinValue,
                    0,
                    0,
                    telemetry);
'@
Replace-Once @'
                snapshot.ReceivedUtc,
                outlineParts,
                fallbackParts);
'@ @'
                snapshot.ReceivedUtc,
                outlineParts,
                fallbackParts,
                telemetry);
'@
$startMarker = '        private static void DrawStatus('
$endMarker = '        private static void DrawField('
$start = $updated.IndexOf($startMarker)
$end = $updated.IndexOf($endMarker, $start)
if ($start -lt 0 -or $end -le $start -or ([regex]::Matches($updated, [regex]::Escape($startMarker))).Count -ne 1) { throw 'Could not isolate existing DrawStatus method.' }
$newMethod = @'
        // 14.23.13 VESSEL STATS: presentation-only; uses existing flight telemetry.
        private static void DrawStatus(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            DateTime receivedUtc,
            int outlineParts,
            int fallbackParts,
            MissionTelemetry telemetry)
        {
            int x = bounds.Left + 14;
            int valueX = bounds.Left + Math.Max(145, (int)(bounds.Width * 0.56));
            int y = bounds.Top + PanelHeaderHeight + 16;
            int step = 29;
            bool valid = topology != null;
            bool live = valid && telemetry != null &&
                string.Equals(topology.VesselName, telemetry.VesselName,
                    StringComparison.OrdinalIgnoreCase);

            DrawField(context, "VESSEL", valid ? Safe(topology.VesselName) : "---", x, valueX, y);
            y += step;
            DrawField(context, "LINK", valid ? "SCHEM ONLINE" : "WAITING", x, valueX, y);
            y += step + 19;
            DrawSectionLabel(context, "MASS / PERFORMANCE", x, y);
            y += step;
            DrawField(context, "MASS", live ? telemetry.VesselMass.ToString("0.0") + " t" : "---", x, valueX, y);
            y += step;
            // KMC has no authoritative dry-mass telemetry yet.
            DrawField(context, "DRY MASS", "N/A", x, valueX, y);
            y += step;
            DrawField(context, "MAX THRUST", live ? telemetry.MaximumThrust.ToString("0.0") + " kN" : "---", x, valueX, y);
            y += step;
            DrawField(context, "TWR (TELEM)", live ? telemetry.ThrustToWeightRatio.ToString("0.00") : "---", x, valueX, y);
            y += step + 19;
            DrawSectionLabel(context, "CONFIGURATION", x, y);
            y += step;
            DrawField(context, "PARTS", valid ? topology.PartCount.ToString() : "---", x, valueX, y);
            y += step;
            DrawField(context, "ENGINES", live ? telemetry.EngineCount.ToString() : "---", x, valueX, y);
            y += step;
            DrawField(context, "STAGE", valid ? topology.CurrentStage.ToString() : "---", x, valueX, y);
            y += step;
            DrawField(context, "NEXT STAGE", valid && topology.NextStage >= 0 ? topology.NextStage.ToString() : "---", x, valueX, y);
            y += step + 19;
            DrawSectionLabel(context, "SCHEM DIAGNOSTICS", x, y);
            y += step;
            DrawField(context, "REVISION", valid ? topology.Revision.ToString() : "---", x, valueX, y);
            y += step;
            DrawField(context, "AGE", valid ? Math.Max(0, (DateTime.UtcNow - receivedUtc).TotalSeconds).ToString("0.0") + " s" : "---", x, valueX, y);
            y += step;
            DrawField(context, "OUTLINES", valid ? outlineParts.ToString() : "---", x, valueX, y);
            y += step;
            DrawField(context, "FALLBACK", valid ? fallbackParts.ToString() : "---", x, valueX, y);
        }

        private static void DrawSectionLabel(MissionRenderContext context, string label, int x, int y)
        {
            using (SolidBrush brush = new SolidBrush(context.DimPhosphorColor))
                context.Graphics.DrawString(label, context.SmallFont, brush, x, y);
        }

'@
$updated = $updated.Substring(0,$start) + $newMethod + $updated.Substring($end)
if ($updated -eq $original) { throw 'No changes produced.' }
# Save a backup OUTSIDE the repository to avoid accidentally committing it.
$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) 'KMC_14_23_13_backup'
[System.IO.Directory]::CreateDirectory($backupDir) | Out-Null
$backupPath = Join-Path $backupDir ('SchematicPage_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.cs')
[System.IO.File]::WriteAllText($backupPath, $original)
try { [System.IO.File]::WriteAllText($path, $updated) }
catch { [System.IO.File]::WriteAllText($path, $original); throw }
Write-Host 'KMC 14.23.13 UI-only patch applied.' -ForegroundColor Green
Write-Host "Backup: $backupPath"
Write-Host 'Build KMC.MissionControl in Debug. No KSP DLL replacement is needed.'
