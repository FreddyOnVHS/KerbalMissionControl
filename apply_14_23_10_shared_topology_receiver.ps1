# KMC 14.23.10 - One owner for UDP topology port 5082.
# Run from repository root. Only MainForm.cs is changed.
param()
$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location) 'KMC.MissionControl/MainForm.cs'
if (!(Test-Path -LiteralPath $path)) { throw "Missing $path. Run in the KMC repository root." }
$enc = New-Object System.Text.UTF8Encoding($false)
$original = [System.IO.File]::ReadAllText($path)
$changes = @(
    @('        private readonly OrbitMapTelemetryReceiver _orbitMapReceiver;        private readonly VesselSchematicTelemetryReceiver _vesselSchematicReceiver;', '        private readonly OrbitMapTelemetryReceiver _orbitMapReceiver;'),
    @("            _vesselSchematicReceiver =`n                new VesselSchematicTelemetryReceiver();`n", ''),
    @('            _vesselSchematicReceiver.SnapshotReceived +=', '            _receiver.TopologyReceived +='),
    @('                _orbitMapReceiver.Start();                _vesselSchematicReceiver.Start();', '                _orbitMapReceiver.Start();'),
    @('            _vesselSchematicReceiver.SnapshotReceived -=', '            _receiver.TopologyReceived -='),
    @('            _orbitMapReceiver.Dispose();            _vesselSchematicReceiver.Dispose();', '            _orbitMapReceiver.Dispose();')
)
# Normalize to LF for predictable preflight, restore original newline style afterward.
$hasCrlf = $original.Contains("`r`n")
$text = $original.Replace("`r`n", "`n")
foreach ($change in $changes) {
    $needle = [string]$change[0]
    $replacement = [string]$change[1]
    $count = ([regex]::Matches($text, [regex]::Escape($needle))).Count
    if ($count -ne 1) { throw "Preflight failed: expected one occurrence ($count found): $needle. No files changed." }
    $text = $text.Replace($needle, $replacement)
}
$callbackNeedle = @'
        private void OnVesselSchematicSnapshotReceived(
            KMC.Shared.Topology.VesselTopology topology)
        {
'@
$callbackReplacement = @'
        private void OnVesselSchematicSnapshotReceived(
            KMC.Shared.Topology.VesselTopology topology)
        {
            // Shared decoded topology: the engineering transport owns UDP 5082.
            VesselSchematicTelemetryStore.Publish(topology, DateTime.UtcNow);
'@
if (([regex]::Matches($text,[regex]::Escape($callbackNeedle))).Count -ne 1) { throw 'Preflight failed: callback not found exactly once. No files changed.' }
$text = $text.Replace($callbackNeedle, $callbackReplacement)
# Ensure the new code does not attempt to start a standalone topology socket.
if ($text.Contains('_vesselSchematicReceiver')) { throw 'Preflight failed: residual dedicated receiver reference. No files changed.' }
if ($hasCrlf) { $text = $text.Replace("`n", "`r`n") }
$backup = "$path.before_14_23_10.bak"
if (Test-Path -LiteralPath $backup) { throw "Backup already exists: $backup. No files changed." }
[System.IO.File]::Copy($path, $backup)
$temp = "$path.14_23_10.tmp"
try {
    [System.IO.File]::WriteAllText($temp, $text, $enc)
    [System.IO.File]::Copy($temp, $path, $true)
} finally { if (Test-Path $temp) { Remove-Item -LiteralPath $temp -Force } }
Write-Host '14.23.10 applied: one UDP 5082 receiver; SCHEM shares engineering topology.'
Write-Host "Backup: $backup"
Write-Host 'Run NavigationTests and rebuild Mission Control only.'
