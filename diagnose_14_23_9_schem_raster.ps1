# KMC 14.23.9 - reversible SCHEM raster isolation test
# Run from the repository root. Default = disable raster; -Restore = re-enable.
param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$relativePath = 'KMC.Plugin/Topology/VesselTopologyBuilder.cs'
$path = Join-Path (Get-Location).Path $relativePath
if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Source not found: $path. Run from the KMC repository root." }
$original = [System.IO.File]::ReadAllText($path)
$active = 'try { BuildVisibleSideRaster(vessel, topology); }'
$disabled = 'try { /* KMC 14.23.9 diagnostic: depth raster temporarily disabled */ }'
$from = if ($Restore) { $disabled } else { $active }
$to   = if ($Restore) { $active } else { $disabled }
if (-not $original.Contains($from)) {
    if ($original.Contains($to)) { throw 'Already in requested state. No changes made.' }
    throw 'Expected source anchor missing. No changes made. Share your current code before proceeding.'
}
if ($original.Split([string[]]@($from), [System.StringSplitOptions]::None).Length -ne 2) { throw 'Anchor appears more than once. No changes made.' }
$catchAnchor = 'catch { topology.SchematicPixels = null; }'
if (-not $original.Contains($catchAnchor)) { throw 'Expected raster exception handler missing. No changes made.' }
$updated = $original.Replace($from, $to)
$backupPath = "$path.diagnostic-backup"
if (-not (Test-Path -LiteralPath $backupPath)) { [System.IO.File]::Copy($path, $backupPath, $false) }
$encoding = New-Object System.Text.UTF8Encoding($false)
# Write temp in same directory and replace only after all checks pass.
$temp = "$path.diagnostic-tmp"
try {
    [System.IO.File]::WriteAllText($temp, $updated, $encoding)
    [System.IO.File]::Copy($temp, $path, $true)
} finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Force } }
if ($Restore) { Write-Host 'SCHEM raster restored. Rebuild KMC.Plugin.dll to return to 14.23.9 behavior.' -ForegroundColor Green }
else { Write-Host 'SCHEM raster disabled for diagnosis. Rebuild KMC.Plugin.dll and restart KSP.' -ForegroundColor Yellow }
Write-Host "Changed only $relativePath. Do not commit this diagnostic change."
