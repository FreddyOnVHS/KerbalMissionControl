param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function ReadText($path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText($path, $text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

# ---------------------------------------------------------------------------
# KSP plugin sender: move authoritative maneuver-vector telemetry 5102 -> 5103
# ---------------------------------------------------------------------------
$pluginPath =
    Join-Path $RepoRoot "KMC.Plugin\VelocityVectorTelemetrySender.cs"

$plugin =
    ReadText $pluginPath

$oldPlugin = @'
        private const int ManeuverVectorTelemetryPort =
            5102;
'@

$newPlugin = @'
        private const int ManeuverVectorTelemetryPort =
            5103;
'@

if ($plugin.Contains($oldPlugin)) {
    $plugin =
        $plugin.Replace(
            $oldPlugin,
            $newPlugin)

    WriteText $pluginPath $plugin
}
elseif (-not $plugin.Contains("ManeuverVectorTelemetryPort =`r`n            5103;") -and
        -not $plugin.Contains("ManeuverVectorTelemetryPort =`n            5103;")) {
    throw "Could not locate ManeuverVectorTelemetryPort in KMC.Plugin."
}

# ---------------------------------------------------------------------------
# Mission Control receiver: move authoritative maneuver-vector bind 5102 -> 5103
# ---------------------------------------------------------------------------
$receiverPath =
    Join-Path $RepoRoot "KMC.MissionControl\Transport\VelocityVectorTelemetrySample.cs"

$receiver =
    ReadText $receiverPath

$classToken =
    "public sealed class ManeuverVectorTelemetryReceiver"

$classStart =
    $receiver.IndexOf(
        $classToken)

if ($classStart -lt 0) {
    throw "Could not locate ManeuverVectorTelemetryReceiver."
}

$nextClass =
    $receiver.IndexOf(
        "public sealed class ",
        $classStart + $classToken.Length)

if ($nextClass -lt 0) {
    $nextClass =
        $receiver.Length
}

$classText =
    $receiver.Substring(
        $classStart,
        $nextClass - $classStart)

$oldPort =
    "private const int Port = 5102;"

$newPort =
    "private const int Port = 5103;"

if ($classText.Contains($oldPort)) {
    $classText =
        $classText.Replace(
            $oldPort,
            $newPort)

    $receiver =
        $receiver.Substring(
            0,
            $classStart) +
        $classText +
        $receiver.Substring(
            $nextClass)

    WriteText $receiverPath $receiver
}
elseif (-not $classText.Contains($newPort)) {
    throw "Could not locate maneuver-vector receiver port."
}

Write-Host "Applied KMC 14.22.70d maneuver-vector UDP port fix."
Write-Host "Authoritative maneuver-vector telemetry now uses UDP 5103."
Write-Host "IMPORTANT: rebuild BOTH KMC.MissionControl and KMC.Plugin."
Write-Host "Copy the rebuilt KMC.Plugin.dll into KSP before runtime testing."
