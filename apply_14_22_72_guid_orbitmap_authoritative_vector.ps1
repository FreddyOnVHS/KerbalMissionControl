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

# ===========================================================================
# 1. ENGINE STORE
#    Keep authoritative maneuver vectors keyed by vessel + node UT.
# ===========================================================================
$modelPath =
    Join-Path $RepoRoot "KMC.Engine\Orbit\VelocityVectorTelemetryModel.cs"

$model =
    ReadText $modelPath

if (-not $model.Contains("FindForPlan(")) {
    $oldStore = @'
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

    $newStore = @'
    public static class ManeuverVectorTelemetryStore
    {
        private const double NodeUtMatchToleranceSeconds = 0.25;
        private const int MaxSamples = 16;

        private static readonly object SyncRoot =
            new object();

        private static readonly List<ManeuverVectorTelemetryModel> Samples =
            new List<ManeuverVectorTelemetryModel>();

        private static ManeuverVectorTelemetryModel _latest =
            new ManeuverVectorTelemetryModel();

        public static void Publish(
            ManeuverVectorTelemetryModel sample)
        {
            if (sample == null)
            {
                return;
            }

            ManeuverVectorTelemetryModel clone =
                ManeuverVectorTelemetryModel.Clone(
                    sample);

            lock (SyncRoot)
            {
                _latest =
                    ManeuverVectorTelemetryModel.Clone(
                        clone);

                int replaceIndex = -1;

                for (int index = 0;
                     index < Samples.Count;
                     index++)
                {
                    ManeuverVectorTelemetryModel existing =
                        Samples[index];

                    if (string.Equals(
                            existing.VesselId ?? string.Empty,
                            clone.VesselId ?? string.Empty,
                            StringComparison.Ordinal) &&
                        !double.IsNaN(
                            existing.NodeUniversalTimeSeconds) &&
                        !double.IsNaN(
                            clone.NodeUniversalTimeSeconds) &&
                        Math.Abs(
                            existing.NodeUniversalTimeSeconds -
                            clone.NodeUniversalTimeSeconds) <=
                            NodeUtMatchToleranceSeconds)
                    {
                        replaceIndex = index;
                        break;
                    }
                }

                if (replaceIndex >= 0)
                {
                    Samples[replaceIndex] = clone;
                }
                else
                {
                    Samples.Add(clone);
                }

                Samples.Sort(
                    delegate(
                        ManeuverVectorTelemetryModel left,
                        ManeuverVectorTelemetryModel right)
                    {
                        return
                            right.ReceivedUtc.CompareTo(
                                left.ReceivedUtc);
                    });

                while (Samples.Count > MaxSamples)
                {
                    Samples.RemoveAt(
                        Samples.Count - 1);
                }
            }
        }

        public static ManeuverVectorTelemetryModel FindForPlan(
            string vesselId,
            double nodeUniversalTimeSeconds,
            DateTime analysisUtc)
        {
            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Samples.Count;
                     index++)
                {
                    ManeuverVectorTelemetryModel sample =
                        Samples[index];

                    if (sample != null &&
                        sample.IsUsableFor(
                            vesselId,
                            nodeUniversalTimeSeconds,
                            analysisUtc))
                    {
                        return
                            ManeuverVectorTelemetryModel.Clone(
                                sample);
                    }
                }
            }

            return
                new ManeuverVectorTelemetryModel();
        }

        public static ManeuverVectorTelemetryModel GetLatest()
        {
            lock (SyncRoot)
            {
                return
                    ManeuverVectorTelemetryModel.Clone(
                        _latest);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverVectorTelemetryModel();

                Samples.Clear();
            }
        }
    }
'@

    if (-not $model.Contains($oldStore)) {
        throw "Could not locate ManeuverVectorTelemetryStore from 14.22.70b."
    }

    $model =
        $model.Replace(
            $oldStore,
            $newStore)

    WriteText $modelPath $model
}

# ===========================================================================
# 2. ORBITMAP RECEIVER
#    Publish every authoritative OrbitMap maneuver node into the keyed store.
# ===========================================================================
$receiverPath =
    Join-Path $RepoRoot "KMC.MissionControl\Telemetry\OrbitMapTelemetryReceiver.cs"

$receiver =
    ReadText $receiverPath

if (-not $receiver.Contains("using KMC.Engine.Orbit;")) {
    $receiver =
        $receiver.Replace(
            "using KMC.Shared;",
            "using KMC.Shared;`r`nusing KMC.Engine.Orbit;")
}

if (-not $receiver.Contains("PublishAuthoritativeManeuverVectors(packet")) {
    $oldReceive = @'
                    OrbitMapSnapshotStore.SetLatest(packet, DateTime.UtcNow);
                    Action<OrbitMapPacket> handler = SnapshotReceived;
'@

    $newReceive = @'
                    DateTime receivedUtc =
                        DateTime.UtcNow;

                    OrbitMapSnapshotStore.SetLatest(
                        packet,
                        receivedUtc);

                    PublishAuthoritativeManeuverVectors(
                        packet,
                        receivedUtc);

                    Action<OrbitMapPacket> handler = SnapshotReceived;
'@

    if (-not $receiver.Contains($oldReceive)) {
        throw "Could not locate OrbitMap receive publication point."
    }

    $receiver =
        $receiver.Replace(
            $oldReceive,
            $newReceive)

    $insertMarker =
        "        public void Dispose() { Stop(); }"

    $helper = @'
        private static void PublishAuthoritativeManeuverVectors(
            OrbitMapPacket packet,
            DateTime receivedUtc)
        {
            if (packet == null ||
                packet.ManeuverNodes == null ||
                string.IsNullOrWhiteSpace(
                    packet.VesselId))
            {
                return;
            }

            for (int index = 0;
                 index < packet.ManeuverNodes.Count;
                 index++)
            {
                OrbitMapManeuverNode node =
                    packet.ManeuverNodes[index];

                if (node == null ||
                    !node.AuthoritativeBurnVectorAvailable)
                {
                    continue;
                }

                ManeuverVectorTelemetryStore.Publish(
                    new ManeuverVectorTelemetryModel
                    {
                        TelemetryAvailable = true,
                        SourceTimestampUtc =
                            packet.TimestampUtc,
                        ReceivedUtc =
                            receivedUtc,
                        VesselId =
                            packet.VesselId,
                        NodeUniversalTimeSeconds =
                            node.UniversalTimeSeconds,
                        RightComponent =
                            node.AuthoritativeBurnRight,
                        NoseComponent =
                            node.AuthoritativeBurnNose,
                        ReferenceForwardComponent =
                            node.AuthoritativeBurnReferenceForward,
                        BurnMagnitudeMetersPerSecond =
                            node.TotalDeltaVMetersPerSecond
                    });
            }
        }

'@

    if (-not $receiver.Contains($insertMarker)) {
        throw "Could not locate OrbitMap receiver helper insertion point."
    }

    $receiver =
        $receiver.Replace(
            $insertMarker,
            $helper + $insertMarker)

    WriteText $receiverPath $receiver
}

# ===========================================================================
# 3. GUIDANCE
#    Resolve the authoritative vector by exact vessel + node UT.
# ===========================================================================
$guidancePath =
    Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSystem.cs"

$guidance =
    ReadText $guidancePath

$oldLookup = @'
            ManeuverVectorTelemetryModel authoritative =
                ManeuverVectorTelemetryStore.GetLatest();

            if (plan != null &&
                authoritative != null &&
                authoritative.IsUsableFor(
                    plan.VesselId,
                    plan.NodeUniversalTimeSeconds,
                    receivedUtc))
'@

$newLookup = @'
            ManeuverVectorTelemetryModel authoritative =
                plan != null
                    ? ManeuverVectorTelemetryStore.FindForPlan(
                        plan.VesselId,
                        plan.NodeUniversalTimeSeconds,
                        receivedUtc)
                    : new ManeuverVectorTelemetryModel();

            if (plan != null &&
                authoritative != null &&
                authoritative.IsUsableFor(
                    plan.VesselId,
                    plan.NodeUniversalTimeSeconds,
                    receivedUtc))
'@

if ($guidance.Contains($oldLookup)) {
    $guidance =
        $guidance.Replace(
            $oldLookup,
            $newLookup)

    WriteText $guidancePath $guidance
}
elseif (-not $guidance.Contains(
            "ManeuverVectorTelemetryStore.FindForPlan(")) {
    throw "Could not locate 14.22.70b authoritative Guidance lookup."
}

# ===========================================================================
# 4. REGRESSION TESTS
# ===========================================================================
$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

if (-not $tests.Contains(
        'Run("GUID selects OrbitMap authoritative vector by vessel and node UT"')) {

    $anchor =
        '        Run("OrbitMap maneuver nodes preserve KSP-authoritative burn vector", OrbitMapAuthoritativeManeuverVectorRoundTrip);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.71 test-run anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("GUID selects OrbitMap authoritative vector by vessel and node UT", ManeuverVectorStoreExactMatch);')
}

if (-not $tests.Contains(
        "private static void ManeuverVectorStoreExactMatch()")) {

    $insert =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($insert)) {
        throw "Could not locate NavigationTests insertion point."
    }

    $method = @'
    private static void ManeuverVectorStoreExactMatch()
    {
        DateTime now =
            DateTime.UtcNow;

        ManeuverVectorTelemetryStore.Clear();

        ManeuverVectorTelemetryStore.Publish(
            new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = true,
                SourceTimestampUtc = now,
                ReceivedUtc = now,
                VesselId = "VESSEL-A",
                NodeUniversalTimeSeconds = 1000.0,
                RightComponent = 0.0,
                NoseComponent = 1.0,
                ReferenceForwardComponent = 0.0,
                BurnMagnitudeMetersPerSecond = 100.0
            });

        ManeuverVectorTelemetryStore.Publish(
            new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = true,
                SourceTimestampUtc = now,
                ReceivedUtc = now,
                VesselId = "VESSEL-A",
                NodeUniversalTimeSeconds = 2000.0,
                RightComponent = 1.0,
                NoseComponent = 0.0,
                ReferenceForwardComponent = 0.0,
                BurnMagnitudeMetersPerSecond = 200.0
            });

        ManeuverVectorTelemetryModel first =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                1000.0,
                now);

        Check(
            first != null &&
            first.TelemetryAvailable,
            "first authoritative maneuver-vector sample not found");

        Near(
            first.RightComponent,
            0.0,
            1e-12);

        Near(
            first.NoseComponent,
            1.0,
            1e-12);

        ManeuverVectorTelemetryModel second =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                2000.0,
                now);

        Check(
            second != null &&
            second.TelemetryAvailable,
            "second authoritative maneuver-vector sample not found");

        Near(
            second.RightComponent,
            1.0,
            1e-12);

        Near(
            second.NoseComponent,
            0.0,
            1e-12);

        ManeuverVectorTelemetryModel wrongVessel =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-B",
                1000.0,
                now);

        Check(
            wrongVessel != null &&
            !wrongVessel.TelemetryAvailable,
            "authoritative vector crossed vessel identity");

        ManeuverVectorTelemetryModel wrongNode =
            ManeuverVectorTelemetryStore.FindForPlan(
                "VESSEL-A",
                3000.0,
                now);

        Check(
            wrongNode != null &&
            !wrongNode.TelemetryAvailable,
            "authoritative vector crossed node identity");

        ManeuverVectorTelemetryStore.Clear();
    }


'@

    $tests =
        $tests.Replace(
            $insert,
            $method + $insert)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.72 GUID OrbitMap authoritative maneuver-vector consumption."
Write-Host "GUID now resolves the exact vector by vessel ID + node UT."
Write-Host "No new UDP channel is used."
