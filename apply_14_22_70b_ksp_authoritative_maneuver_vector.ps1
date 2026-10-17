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

# ---------------------------------------------------------------------------
# 1. KSP PLUGIN: publish KSP-authoritative ManeuverNode.GetBurnVector()
# ---------------------------------------------------------------------------
$pluginPath = Join-Path $RepoRoot "KMC.Plugin\VelocityVectorTelemetrySender.cs"
$plugin = ReadText $pluginPath

if (-not $plugin.Contains("KMC-MNVV1")) {
    $plugin = $plugin.Replace(
@'
        private const int RadialTelemetryPort =
            5099;
'@,
@'
        private const int RadialTelemetryPort =
            5099;

        private const int ManeuverVectorTelemetryPort =
            5102;
'@)

    $plugin = $plugin.Replace(
@'
        private const string RadialProtocolId =
            "KMC-RAD1";
'@,
@'
        private const string RadialProtocolId =
            "KMC-RAD1";

        private const string ManeuverVectorProtocolId =
            "KMC-MNVV1";
'@)

    $plugin = $plugin.Replace(
@'
        private IPEndPoint _radialEndpoint;
'@,
@'
        private IPEndPoint _radialEndpoint;
        private IPEndPoint _maneuverVectorEndpoint;
'@)

    $plugin = $plugin.Replace(
@'
                _radialEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        RadialTelemetryPort);
'@,
@'
                _radialEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        RadialTelemetryPort);

                _maneuverVectorEndpoint =
                    new IPEndPoint(
                        IPAddress.Loopback,
                        ManeuverVectorTelemetryPort);
'@)

    $radialSend = @'
                Send(
                    radialMessage,
                    _radialEndpoint);
'@

    $authoritativeSend = @'
                Send(
                    radialMessage,
                    _radialEndpoint);

                /*
                 * 14.22.70b:
                 * KSP is authoritative for the actual maneuver-node steering
                 * vector. Node.DeltaV is P/N/R node coordinates; GetBurnVector
                 * resolves those coordinates at the node epoch into the world
                 * burn vector KSP uses for the maneuver marker.
                 */
                if (vessel.patchedConicSolver != null &&
                    vessel.patchedConicSolver.maneuverNodes != null &&
                    vessel.patchedConicSolver.maneuverNodes.Count > 0)
                {
                    ManeuverNode node =
                        vessel.patchedConicSolver.maneuverNodes[0];

                    if (node != null)
                    {
                        Vector3d burnVector =
                            node.GetBurnVector(
                                vessel.orbit);

                        double burnMagnitude =
                            burnVector.magnitude;

                        if (burnMagnitude > 1.0e-9)
                        {
                            burnVector =
                                burnVector /
                                burnMagnitude;

                            VectorComponents maneuver =
                                ResolveVector(
                                    burnVector,
                                    vessel);

                            string maneuverMessage =
                                ManeuverVectorProtocolId +
                                "|" +
                                nowUtc.Ticks.ToString(
                                    CultureInfo.InvariantCulture) +
                                "|" +
                                Uri.EscapeDataString(
                                    vessel.id.ToString()) +
                                "|" +
                                node.UT.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture) +
                                "|" +
                                Format(maneuver.Right) +
                                "|" +
                                Format(maneuver.Nose) +
                                "|" +
                                Format(maneuver.ReferenceForward) +
                                "|" +
                                burnMagnitude.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture);

                            Send(
                                maneuverMessage,
                                _maneuverVectorEndpoint);
                        }
                    }
                }
'@

    if (-not $plugin.Contains($radialSend)) {
        throw "Could not locate radial telemetry send block."
    }
    $plugin = $plugin.Replace($radialSend, $authoritativeSend)
    WriteText $pluginPath $plugin
}

# ---------------------------------------------------------------------------
# 2. ENGINE: authoritative maneuver-vector model/store
# ---------------------------------------------------------------------------
$modelPath = Join-Path $RepoRoot "KMC.Engine\Orbit\VelocityVectorTelemetryModel.cs"
$model = ReadText $modelPath

if (-not $model.Contains("class ManeuverVectorTelemetryModel")) {
    $insert = @'

    /// <summary>
    /// 14.22.70b KSP-authoritative maneuver-node burn direction.
    /// Components are a unit vector resolved in vessel ReferenceTransform.
    /// </summary>
    public sealed class ManeuverVectorTelemetryModel
    {
        public bool TelemetryAvailable { get; set; }
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselId { get; set; } = string.Empty;
        public double NodeUniversalTimeSeconds { get; set; } = double.NaN;
        public double RightComponent { get; set; }
        public double NoseComponent { get; set; }
        public double ReferenceForwardComponent { get; set; }
        public double BurnMagnitudeMetersPerSecond { get; set; } = double.NaN;

        public bool IsUsableFor(
            string vesselId,
            double nodeUt,
            DateTime analysisUtc)
        {
            if (!TelemetryAvailable ||
                ReceivedUtc == DateTime.MinValue ||
                Math.Abs((analysisUtc - ReceivedUtc).TotalSeconds) > 0.75 ||
                string.IsNullOrWhiteSpace(vesselId) ||
                !string.Equals(
                    VesselId ?? string.Empty,
                    vesselId ?? string.Empty,
                    StringComparison.Ordinal) ||
                double.IsNaN(nodeUt) ||
                double.IsInfinity(nodeUt) ||
                double.IsNaN(NodeUniversalTimeSeconds) ||
                double.IsInfinity(NodeUniversalTimeSeconds) ||
                Math.Abs(NodeUniversalTimeSeconds - nodeUt) > 0.25)
            {
                return false;
            }

            double magnitude =
                Math.Sqrt(
                    RightComponent * RightComponent +
                    NoseComponent * NoseComponent +
                    ReferenceForwardComponent *
                    ReferenceForwardComponent);

            return
                magnitude >= 0.95 &&
                magnitude <= 1.05;
        }

        internal static ManeuverVectorTelemetryModel Clone(
            ManeuverVectorTelemetryModel source)
        {
            if (source == null)
            {
                return new ManeuverVectorTelemetryModel();
            }

            return new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = source.TelemetryAvailable,
                SourceTimestampUtc = source.SourceTimestampUtc,
                ReceivedUtc = source.ReceivedUtc,
                VesselId = source.VesselId,
                NodeUniversalTimeSeconds = source.NodeUniversalTimeSeconds,
                RightComponent = source.RightComponent,
                NoseComponent = source.NoseComponent,
                ReferenceForwardComponent = source.ReferenceForwardComponent,
                BurnMagnitudeMetersPerSecond =
                    source.BurnMagnitudeMetersPerSecond
            };
        }
    }

    public static class ManeuverVectorTelemetryStore
    {
        private static readonly object SyncRoot = new object();
        private static ManeuverVectorTelemetryModel _latest =
            new ManeuverVectorTelemetryModel();

        public static void Publish(ManeuverVectorTelemetryModel sample)
        {
            lock (SyncRoot)
            {
                _latest = ManeuverVectorTelemetryModel.Clone(sample);
            }
        }

        public static ManeuverVectorTelemetryModel GetLatest()
        {
            lock (SyncRoot)
            {
                return ManeuverVectorTelemetryModel.Clone(_latest);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = new ManeuverVectorTelemetryModel();
            }
        }
    }
'@

    $last = $model.LastIndexOf("}")
    if ($last -lt 0) { throw "VelocityVectorTelemetryModel.cs malformed." }
    # Insert before namespace closing brace.
    $model = $model.Substring(0, $last) + $insert + "`r`n}" + $model.Substring($last + 1)
    WriteText $modelPath $model
}

# ---------------------------------------------------------------------------
# 3. MISSION CONTROL: UDP 5102 receiver
# ---------------------------------------------------------------------------
$transportPath = Join-Path $RepoRoot "KMC.MissionControl\Transport\VelocityVectorTelemetrySample.cs"
$transport = ReadText $transportPath

if (-not $transport.Contains("class ManeuverVectorTelemetryReceiver")) {
    $receiver = @'

    public sealed class ManeuverVectorTelemetrySample
    {
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselId { get; set; } = string.Empty;
        public double NodeUniversalTimeSeconds { get; set; }
        public double RightComponent { get; set; }
        public double NoseComponent { get; set; }
        public double ReferenceForwardComponent { get; set; }
        public double BurnMagnitudeMetersPerSecond { get; set; }
    }

    /// <summary>
    /// 14.22.70b receiver for KSP-authoritative maneuver burn vector.
    /// </summary>
    public sealed class ManeuverVectorTelemetryReceiver : IDisposable
    {
        private const int Port = 5102;
        private const string ProtocolId = "KMC-MNVV1";
        private UdpClient _client;
        private Thread _thread;
        private volatile bool _running;

        public event Action<ManeuverVectorTelemetrySample> SampleReceived;

        public void Start()
        {
            if (_running) return;
            _client = new UdpClient(new IPEndPoint(IPAddress.Any, Port));
            _running = true;
            _thread = new Thread(ReceiveLoop);
            _thread.IsBackground = true;
            _thread.Name = "KMC Maneuver Vector UDP " + Port;
            _thread.Start();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = _client.Receive(ref sender);
                    ManeuverVectorTelemetrySample sample;
                    if (!TryParse(data, out sample)) continue;
                    Action<ManeuverVectorTelemetrySample> handler = SampleReceived;
                    if (handler != null) handler(sample);
                }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { if (!_running) return; }
            }
        }

        private static bool TryParse(
            byte[] data,
            out ManeuverVectorTelemetrySample sample)
        {
            sample = null;
            if (data == null || data.Length == 0) return false;

            string[] fields = Encoding.UTF8.GetString(data).Split('|');
            if (fields.Length != 8 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal))
                return false;

            long ticks;
            double nodeUt, right, nose, forward, magnitude;

            if (!long.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                !double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out nodeUt) ||
                !double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out right) ||
                !double.TryParse(fields[5], NumberStyles.Float, CultureInfo.InvariantCulture, out nose) ||
                !double.TryParse(fields[6], NumberStyles.Float, CultureInfo.InvariantCulture, out forward) ||
                !double.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out magnitude))
                return false;

            DateTime sourceUtc;
            try { sourceUtc = new DateTime(ticks, DateTimeKind.Utc); }
            catch (ArgumentOutOfRangeException) { return false; }

            sample = new ManeuverVectorTelemetrySample
            {
                SourceTimestampUtc = sourceUtc,
                ReceivedUtc = DateTime.UtcNow,
                VesselId = Uri.UnescapeDataString(fields[2]),
                NodeUniversalTimeSeconds = nodeUt,
                RightComponent = right,
                NoseComponent = nose,
                ReferenceForwardComponent = forward,
                BurnMagnitudeMetersPerSecond = magnitude
            };

            return true;
        }

        public void Dispose()
        {
            _running = false;
            UdpClient client = _client;
            _client = null;
            if (client != null) { try { client.Close(); } catch { } }
        }
    }
'@

    $last = $transport.LastIndexOf("}")
    if ($last -lt 0) { throw "VelocityVectorTelemetrySample.cs malformed." }
    $transport = $transport.Substring(0, $last) + $receiver + "`r`n}" + $transport.Substring($last + 1)
    WriteText $transportPath $transport
}

# ---------------------------------------------------------------------------
# 4. MAIN FORM: own/start/stop the new receiver and publish to Engine store.
# ---------------------------------------------------------------------------
$mainPath = Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"
$main = ReadText $mainPath

if (-not $main.Contains("_maneuverVectorReceiver")) {
    $main = $main.Replace(
@'
        private readonly RadialTelemetryReceiver _radialReceiver;
'@,
@'
        private readonly RadialTelemetryReceiver _radialReceiver;
        private readonly ManeuverVectorTelemetryReceiver _maneuverVectorReceiver;
'@)

    $main = $main.Replace(
@'
            _radialReceiver =
                new RadialTelemetryReceiver();
'@,
@'
            _radialReceiver =
                new RadialTelemetryReceiver();

            _maneuverVectorReceiver =
                new ManeuverVectorTelemetryReceiver();
'@)

    $main = $main.Replace(
@'
            _radialReceiver.SampleReceived +=
                OnRadialTelemetryReceived;
'@,
@'
            _radialReceiver.SampleReceived +=
                OnRadialTelemetryReceived;

            _maneuverVectorReceiver.SampleReceived +=
                OnManeuverVectorTelemetryReceived;
'@)

    $main = $main.Replace(
@'
                RadialTelemetryStore.Clear();
'@,
@'
                RadialTelemetryStore.Clear();
                ManeuverVectorTelemetryStore.Clear();
'@)

    $main = $main.Replace(
@'
                _radialReceiver.Start();
'@,
@'
                _radialReceiver.Start();
                _maneuverVectorReceiver.Start();
'@)

    $handlerMarker = @'
        private void OnOrbitNormalTelemetryReceived(
'@

    $handler = @'
        private void OnManeuverVectorTelemetryReceived(
            ManeuverVectorTelemetrySample sample)
        {
            if (sample == null)
            {
                return;
            }

            ManeuverVectorTelemetryStore.Publish(
                new ManeuverVectorTelemetryModel
                {
                    TelemetryAvailable = true,
                    SourceTimestampUtc = sample.SourceTimestampUtc,
                    ReceivedUtc = sample.ReceivedUtc,
                    VesselId = sample.VesselId ?? string.Empty,
                    NodeUniversalTimeSeconds =
                        sample.NodeUniversalTimeSeconds,
                    RightComponent = sample.RightComponent,
                    NoseComponent = sample.NoseComponent,
                    ReferenceForwardComponent =
                        sample.ReferenceForwardComponent,
                    BurnMagnitudeMetersPerSecond =
                        sample.BurnMagnitudeMetersPerSecond
                });
        }

'@

    if (-not $main.Contains($handlerMarker)) {
        throw "Could not locate MainForm telemetry handler insertion point."
    }
    $main = $main.Replace($handlerMarker, $handler + $handlerMarker)

    # Dispose alongside existing receivers. Accept either Dispose or Stop region.
    $disposeNeedle = @'
            _radialReceiver.Dispose();
'@
    if ($main.Contains($disposeNeedle)) {
        $main = $main.Replace(
            $disposeNeedle,
            $disposeNeedle + "`r`n            _maneuverVectorReceiver.Dispose();")
    }

    WriteText $mainPath $main
}

# ---------------------------------------------------------------------------
# 5. GUIDANCE: authoritative KSP vector outranks reconstructed P/N/R vector.
# ---------------------------------------------------------------------------
$guidPath = Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSystem.cs"
$guid = ReadText $guidPath

if (-not $guid.Contains("KSP-AUTHORITATIVE MANEUVER VECTOR")) {
    $needle = @'
            double right;
            double nose;
            double forward;

            /*
             * 14.22.70:
'@

    $replacement = @'
            double right;
            double nose;
            double forward;

            /*
             * 14.22.70b:
             * Prefer KSP's own ManeuverNode.GetBurnVector() telemetry. This
             * is the same future-node vector used by KSP's maneuver marker,
             * already resolved into the vessel ReferenceTransform frame.
             */
            ManeuverVectorTelemetryModel authoritative =
                ManeuverVectorTelemetryStore.GetLatest();

            if (plan != null &&
                authoritative != null &&
                authoritative.IsUsableFor(
                    plan.VesselId,
                    plan.NodeUniversalTimeSeconds,
                    receivedUtc))
            {
                right =
                    authoritative.RightComponent;

                nose =
                    authoritative.NoseComponent;

                forward =
                    authoritative.ReferenceForwardComponent;

                double authoritativeMagnitude =
                    Math.Sqrt(
                        right * right +
                        nose * nose +
                        forward * forward);

                if (authoritativeMagnitude > 1.0e-9)
                {
                    right /= authoritativeMagnitude;
                    nose /= authoritativeMagnitude;
                    forward /= authoritativeMagnitude;

                    solution.CommandedRetrograde =
                        false;

                    solution.AttitudeReference =
                        "KSP-AUTHORITATIVE MANEUVER VECTOR";

                    solution.Evidence =
                        "Authoritative maneuver attitude reference from KSP ManeuverNode.GetBurnVector at the verified node epoch.";

                    PopulateResolvedManeuverVector(
                        solution,
                        right,
                        nose,
                        forward);

                    return;
                }
            }

            /*
             * 14.22.70:
'@

    if (-not $guid.Contains($needle)) {
        throw "Could not locate Guidance mixed-vector insertion point."
    }
    $guid = $guid.Replace($needle, $replacement)

    # Factor final output into helper so authoritative branch can use identical math.
    $finalOld = @'
            nose =
                Clamp(
                    nose,
                    -1.0,
                    1.0);

            solution.ManeuverVectorAvailable =
                true;

            solution.ManeuverRightComponent =
                right;

            solution.ManeuverNoseComponent =
                nose;

            solution.ManeuverReferenceForwardComponent =
                forward;

            solution.AlignmentErrorDegrees =
                RadiansToDegrees(
                    Math.Acos(
                        nose));

            solution.LateralErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        right,
                        Math.Max(
                            0.000001,
                            nose)));

            solution.VerticalErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        forward,
                        Math.Sqrt(
                            nose * nose +
                            right * right)));

            solution.Available =
                true;
        }

        private static bool IsMixedManeuver(
'@

    $finalNew = @'
            PopulateResolvedManeuverVector(
                solution,
                right,
                nose,
                forward);
        }

        private static void PopulateResolvedManeuverVector(
            GuidanceSolutionModel solution,
            double right,
            double nose,
            double forward)
        {
            nose =
                Clamp(
                    nose,
                    -1.0,
                    1.0);

            solution.ManeuverVectorAvailable =
                true;

            solution.ManeuverRightComponent =
                right;

            solution.ManeuverNoseComponent =
                nose;

            solution.ManeuverReferenceForwardComponent =
                forward;

            solution.AlignmentErrorDegrees =
                RadiansToDegrees(
                    Math.Acos(
                        nose));

            solution.LateralErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        right,
                        Math.Max(
                            0.000001,
                            nose)));

            solution.VerticalErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        forward,
                        Math.Sqrt(
                            nose * nose +
                            right * right)));

            solution.Available =
                true;
        }

        private static bool IsMixedManeuver(
'@

    if (-not $guid.Contains($finalOld)) {
        throw "Could not locate Guidance final vector output block."
    }
    $guid = $guid.Replace($finalOld, $finalNew)

    WriteText $guidPath $guid
}

Write-Host "Applied KMC 14.22.70b KSP-authoritative maneuver-vector telemetry."
Write-Host "IMPORTANT: rebuild KMC.Plugin.dll and replace the DLL in KSP for runtime testing."
