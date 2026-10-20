$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location).Path 'KMC.MissionControl\Pages\MapPage.cs'
if (!(Test-Path $path)) { throw 'Run from the KMC repository root.' }
$m = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
$oldKey = @'
                destination.Name + "|" +
                Math.Floor(packet.UniversalTimeSeconds / 600.0).ToString("0") + "|" +
                pe.ToString("R") + "|" +
'@
$newKey = @'
                destination.Name + "|" +
                pe.ToString("R") + "|" +
'@
if (!$m.Contains($oldKey)) { throw '14.24.6a expected cache-key code not found. No changes made.' }
if (!$m.Contains('private void StartMoonTransferCalculation()')) { throw 'Expected 14.24.6a background calculation missing. No changes made.' }
$oldOutcome = @'
                if (System.Threading.Interlocked.CompareExchange(
                        ref _moonPreviewGeneration, generation, generation) == generation &&
                    string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
                {
                    _moonPreview = plan;
                    _moonPreviewStatus = state;
                }
                _moonPreviewRunning = false;
'@
$newOutcome = @'
                // Keep the completion state, including a no-solution diagnostic,
                // until the operator changes the vessel, destination, or Pe.
                // Do not allow an old worker to replace a newer selection.
                if (System.Threading.Interlocked.CompareExchange(
                        ref _moonPreviewGeneration, generation, generation) == generation &&
                    string.Equals(_moonPreviewKey, key, StringComparison.Ordinal))
                {
                    _moonPreview = plan;
                    _moonPreviewStatus = state;
                }
                _moonPreviewRunning = false;
'@
if (!$m.Contains($oldOutcome)) { throw 'Expected worker completion code not found. No changes made.' }
$m = $m.Replace($oldKey, $newKey).Replace($oldOutcome, $newOutcome)
$backupDir = Join-Path $env:TEMP ('KMC_14_24_6b_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backupDir | Out-Null
Copy-Item $path (Join-Path $backupDir 'MapPage.cs')
[IO.File]::WriteAllText($path, $m, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.6b preview-result persistence fix applied.'
Write-Host "Backup: $backupDir"
Write-Host 'No KSP DLL replacement. Rebuild MissionControl and run NavigationTests.'
