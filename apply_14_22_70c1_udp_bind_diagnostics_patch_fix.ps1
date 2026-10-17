param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function ReadText($path) {
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    return [System.IO.File]::ReadAllText($path)
}

function WriteText($path, $text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

function Replace-InClass(
    [string]$text,
    [string]$className,
    [string]$old,
    [string]$new)
{
    $classToken = "public sealed class " + $className
    $classStart = $text.IndexOf($classToken)

    if ($classStart -lt 0) {
        throw "Could not locate class $className."
    }

    $nextClass = $text.IndexOf(
        "public sealed class ",
        $classStart + $classToken.Length)

    if ($nextClass -lt 0) {
        $nextClass = $text.Length
    }

    $match = $text.IndexOf(
        $old,
        $classStart,
        $nextClass - $classStart)

    if ($match -lt 0) {
        throw "Could not locate expected bind inside $className."
    }

    return $text.Substring(0, $match) +
           $new +
           $text.Substring($match + $old.Length)
}

# ---------------------------------------------------------------------------
# Orbit normal / radial / maneuver-vector receivers
# ---------------------------------------------------------------------------
$vectorPath = Join-Path $RepoRoot "KMC.MissionControl\Transport\VelocityVectorTelemetrySample.cs"
$text = ReadText $vectorPath

if (-not $text.Contains("KMC UDP BIND FAILED | ORBIT NORMAL")) {
    $old = @'
            _client =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        Port));
'@
    $new = @'
            try
            {
                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            Port));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | ORBIT NORMAL | UDP " +
                    Port +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
'@
    $text = Replace-InClass $text "OrbitNormalTelemetryReceiver" $old $new
}

if (-not $text.Contains("KMC UDP BIND FAILED | RADIAL")) {
    $old = '            _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port));'
    $new = @'
            try
            {
                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            Port));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | RADIAL | UDP " +
                    Port +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
'@
    $text = Replace-InClass $text "RadialTelemetryReceiver" $old $new
}

if ($text.Contains("class ManeuverVectorTelemetryReceiver") -and
    -not $text.Contains("KMC UDP BIND FAILED | MANEUVER VECTOR")) {
    $old = '            _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port));'
    $new = @'
            try
            {
                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            Port));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | MANEUVER VECTOR | UDP " +
                    Port +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
'@
    $text = Replace-InClass $text "ManeuverVectorTelemetryReceiver" $old $new
}

WriteText $vectorPath $text

# ---------------------------------------------------------------------------
# Orbit-map receiver
# ---------------------------------------------------------------------------
$mapPath = Join-Path $RepoRoot "KMC.MissionControl\Telemetry\OrbitMapTelemetryReceiver.cs"
$text = ReadText $mapPath

if (-not $text.Contains("KMC UDP BIND FAILED | ORBIT MAP")) {
    $old = '            _client = new UdpClient(new IPEndPoint(IPAddress.Loopback, OrbitMapPacket.TelemetryPort));'
    $new = @'
            try
            {
                _client =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Loopback,
                            OrbitMapPacket.TelemetryPort));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | ORBIT MAP | UDP " +
                    OrbitMapPacket.TelemetryPort +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
'@
    if (-not $text.Contains($old)) {
        throw "Could not locate OrbitMapTelemetryReceiver bind."
    }
    $text = $text.Replace($old, $new)
    WriteText $mapPath $text
}

# ---------------------------------------------------------------------------
# Maneuver-link sockets
# ---------------------------------------------------------------------------
$linkPath = Join-Path $RepoRoot "KMC.MissionControl\Transport\ManeuverLinkTransport.cs"
$text = ReadText $linkPath

if (-not $text.Contains("KMC UDP BIND FAILED | MANEUVER EPOCH")) {
    $old = @'
            _epochClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        ManeuverEpochPacket.TelemetryPort));

            _ackClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        ManeuverUplinkPacket.AckPort));

            _nodeStateClient =
                new UdpClient(
                    new IPEndPoint(
                        IPAddress.Any,
                        ManeuverUplinkPacket.NodeStatePort));
'@
    $new = @'
            try
            {
                _epochClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            ManeuverEpochPacket.TelemetryPort));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | MANEUVER EPOCH | UDP " +
                    ManeuverEpochPacket.TelemetryPort +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }

            try
            {
                _ackClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            ManeuverUplinkPacket.AckPort));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | MANEUVER ACK | UDP " +
                    ManeuverUplinkPacket.AckPort +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }

            try
            {
                _nodeStateClient =
                    new UdpClient(
                        new IPEndPoint(
                            IPAddress.Any,
                            ManeuverUplinkPacket.NodeStatePort));
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException(
                    "KMC UDP BIND FAILED | MANEUVER NODE STATE | UDP " +
                    ManeuverUplinkPacket.NodeStatePort +
                    " | " +
                    ex.SocketErrorCode +
                    " | " +
                    ex.Message,
                    ex);
            }
'@
    if (-not $text.Contains($old)) {
        throw "Could not locate ManeuverLinkTransport fixed binds."
    }
    $text = $text.Replace($old, $new)
    WriteText $linkPath $text
}

# ---------------------------------------------------------------------------
# Main startup error: show nested exception chain.
# ---------------------------------------------------------------------------
$mainPath = Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"
$text = ReadText $mainPath

if (-not $text.Contains("BuildStartupExceptionMessage")) {
    $old = @'
                    "Mission Control could not start " +
                    "the telemetry receiver.\n\n" +
                    ex.Message,
'@
    $new = @'
                    "Mission Control could not start " +
                    "the telemetry receiver.\n\n" +
                    BuildStartupExceptionMessage(ex),
'@
    if (-not $text.Contains($old)) {
        throw "Could not locate MainForm startup error message."
    }
    $text = $text.Replace($old, $new)

    $marker = @'
        private void OnRadialTelemetryReceived(
'@
    $helper = @'
        private static string BuildStartupExceptionMessage(
            Exception ex)
        {
            if (ex == null)
            {
                return "Unknown startup failure.";
            }

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            Exception current = ex;

            while (current != null)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                    builder.Append("Caused by: ");
                }

                builder.Append(current.Message);

                current = current.InnerException;
            }

            return builder.ToString();
        }

'@
    if (-not $text.Contains($marker)) {
        throw "Could not locate MainForm helper insertion point."
    }
    $text = $text.Replace($marker, $helper + $marker)
    WriteText $mainPath $text
}

Write-Host "Applied KMC 14.22.70c1 UDP startup bind diagnostics."
Write-Host "Rebuild KMC.MissionControl and launch it again."
