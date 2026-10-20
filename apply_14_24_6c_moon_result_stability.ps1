$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$path = Join-Path $root 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $path)) { throw 'Run from the KMC repository root.' }
$m = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
if (!$m.Contains('private void StartMoonTransferCalculation()') -or
    !$m.Contains('private void DrawMoonTransferPreview(')) {
    throw '14.24.6a moon background preview is missing. No changes made.'
}
if ($m.Contains('14.24.6c stable operator-request key')) {
    throw '14.24.6c is already applied. No changes made.'
}
$start = $m.IndexOf('            string key = (packet.VesselId ?? string.Empty) + "|" +', [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Expected moon preview cache-key start not found. No changes made.' }
$endMarker = '            if (!string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))'
$end = $m.IndexOf($endMarker, $start, [StringComparison]::Ordinal)
if ($end -lt 0 -or $end - $start -gt 1600) { throw 'Expected cache-key end missing. No changes made.' }
$oldKey = $m.Substring($start, $end - $start)
if (!$oldKey.Contains('packet.ActiveOrbit')) {
    throw 'Orbital elements not present in cache key; inspect current source instead of patching.'
}
$newKey = @'
            // 14.24.6c stable operator-request key: solve the captured orbit
            // snapshot only when CALCULATE MOON is pressed. Osculating orbit
            // element/UT changes must not discard the in-progress result.
            // Operator must explicitly recalculate after an orbit change.
            string key = (packet.VesselId ?? string.Empty) + "|" +
                (packet.ReferenceBodyName ?? string.Empty) + "|" +
                destination.Name + "|" + pe.ToString("R");
'@
$m = $m.Substring(0,$start) + $newKey + "`n" + $m.Substring($end)
# Strengthen result-state diagnostics without changing solver behavior.
$oldCompletion = @'
                if (System.Threading.Interlocked.CompareExchange(
                        ref _moonPreviewGeneration, generation, generation) == generation &&
                    string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
                {
                    _moonPreview = plan;
                    _moonPreviewStatus = state;
                }
                _moonPreviewRunning = false;
'@
$newCompletion = @'
                if (System.Threading.Interlocked.CompareExchange(
                        ref _moonPreviewGeneration, generation, generation) == generation &&
                    string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
                {
                    _moonPreview = plan;
                    _moonPreviewStatus = state;
                    _moonPreviewRunning = false;
                }
                // A stale worker must not clear a newer worker's running flag.
'@
if (!$m.Contains($oldCompletion)) { throw 'Expected async completion handler differs. No changes made.' }
$m = $m.Replace($oldCompletion,$newCompletion)
$oldOnChange = @'
                _moonPreview = null;
                _moonPreviewStatus = "SELECT CALCULATE TO SEARCH";
'@
$newOnChange = @'
                _moonPreview = null;
                _moonPreviewStatus = "SELECTION CHANGED - CALCULATE AGAIN";
'@
if (!$m.Contains($oldOnChange)) { throw 'Expected reset status not found. No changes made.' }
$m = $m.Replace($oldOnChange,$newOnChange)
$backup = Join-Path $env:TEMP ('KMC_14_24_6c_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $path (Join-Path $backup 'MapPage.cs')
[IO.File]::WriteAllText($path,$m,(New-Object Text.UTF8Encoding($false)))
Write-Host '14.24.6c stable moon result state installed (MissionControl only).'
Write-Host "Backup: $backup"
Write-Host 'Rebuild KMC.MissionControl in Debug and run NavigationTests.'
Write-Host 'No KSP Plugin or Shared DLL replacement required.'
