# KMC 14.23.13a: restore the SCHEM single-view footer required by existing regressions.
# Run at the KMC repository root after applying 14.23.13.
$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'KMC.MissionControl\Pages\SchematicPage.cs'
if (-not (Test-Path -LiteralPath $path)) { throw "Missing file: $path" }
$original = [System.IO.File]::ReadAllText($path)
$anchor = '            DrawField(context, "FALLBACK", valid ? fallbackParts.ToString() : "---", x, valueX, y);'
$replacement = @'
            DrawField(context, "FALLBACK", valid ? fallbackParts.ToString() : "---", x, valueX, y);
            y += step + 12;
            DrawSectionLabel(context, "SINGLE SIDE VIEW", x, y);
'@.TrimEnd("`r", "`n")
if ($original.Contains('DrawSectionLabel(context, "SINGLE SIDE VIEW"')) { throw '14.23.13a already applied.' }
if (([regex]::Matches($original, [regex]::Escape($anchor))).Count -ne 1) { throw 'Expected exactly one 14.23.13 FALLBACK anchor. No files changed.' }
if (-not $original.Contains('14.23.13 VESSEL STATS')) { throw '14.23.13 base patch not detected. No files changed.' }
$updated = $original.Replace($anchor, $replacement)
$backupDir = Join-Path ([System.IO.Path]::GetTempPath()) 'KMC_14_23_13_backup'
[System.IO.Directory]::CreateDirectory($backupDir) | Out-Null
$backupPath = Join-Path $backupDir ('SchematicPage_before_13a_' + (Get-Date -Format 'yyyyMMdd_HHmmss') + '.cs')
[System.IO.File]::WriteAllText($backupPath, $original)
try { [System.IO.File]::WriteAllText($path, $updated) }
catch { [System.IO.File]::WriteAllText($path, $original); throw }
Write-Host 'KMC 14.23.13a footer restored. Run navigation tests again.' -ForegroundColor Green
Write-Host "Backup: $backupPath"
