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
# 1. SHARED ORBIT-MAP PROTOCOL
#    Extend each maneuver node with the authoritative KSP burn direction.
#    New node layout (14 fields):
#      existing 10
#      valid
#      right
#      nose
#      reference-forward
#
#    Parser remains backward compatible with legacy 10-field nodes.
# ===========================================================================
$packetPath =
    Join-Path $RepoRoot "KMC.shared\OrbitMapPacket.cs"

$packet =
    ReadText $packetPath

if (-not $packet.Contains("AuthoritativeBurnVectorAvailable")) {
    $oldClass = @'
    public sealed class OrbitMapManeuverNode
    {
        public int Index { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public double TotalDeltaVMetersPerSecond { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public int PatchIndex { get; set; }
    }
'@

    $newClass = @'
    public sealed class OrbitMapManeuverNode
    {
        public OrbitMapManeuverNode()
        {
            AuthoritativeBurnRight = double.NaN;
            AuthoritativeBurnNose = double.NaN;
            AuthoritativeBurnReferenceForward = double.NaN;
        }

        public int Index { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public double TotalDeltaVMetersPerSecond { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public double PositionZ { get; set; }
        public int PatchIndex { get; set; }

        /*
         * 14.22.71:
         * KSP-authoritative maneuver direction from
         * ManeuverNode.GetBurnVector(active.orbit), normalized and resolved
         * into the active vessel ReferenceTransform.
         */
        public bool AuthoritativeBurnVectorAvailable { get; set; }
        public double AuthoritativeBurnRight { get; set; }
        public double AuthoritativeBurnNose { get; set; }
        public double AuthoritativeBurnReferenceForward { get; set; }
    }
'@

    if (-not $packet.Contains($oldClass)) {
        throw "Could not locate OrbitMapManeuverNode class."
    }

    $packet =
        $packet.Replace(
            $oldClass,
            $newClass)
}

if (-not $packet.Contains('n.AuthoritativeBurnVectorAvailable ? "1" : "0"')) {
    $oldSerialize = @'
        private static string SerializeNode(OrbitMapManeuverNode n)
        {
            return string.Join("~", new[] { n.Index.ToString(CultureInfo.InvariantCulture), F(n.UniversalTimeSeconds), F(n.ProgradeDeltaVMetersPerSecond), F(n.NormalDeltaVMetersPerSecond), F(n.RadialDeltaVMetersPerSecond), F(n.TotalDeltaVMetersPerSecond), F(n.PositionX), F(n.PositionY), F(n.PositionZ), n.PatchIndex.ToString(CultureInfo.InvariantCulture) });
        }
'@

    $newSerialize = @'
        private static string SerializeNode(OrbitMapManeuverNode n)
        {
            return string.Join(
                "~",
                new[]
                {
                    n.Index.ToString(CultureInfo.InvariantCulture),
                    F(n.UniversalTimeSeconds),
                    F(n.ProgradeDeltaVMetersPerSecond),
                    F(n.NormalDeltaVMetersPerSecond),
                    F(n.RadialDeltaVMetersPerSecond),
                    F(n.TotalDeltaVMetersPerSecond),
                    F(n.PositionX),
                    F(n.PositionY),
                    F(n.PositionZ),
                    n.PatchIndex.ToString(CultureInfo.InvariantCulture),
                    n.AuthoritativeBurnVectorAvailable ? "1" : "0",
                    n.AuthoritativeBurnVectorAvailable
                        ? F(n.AuthoritativeBurnRight)
                        : string.Empty,
                    n.AuthoritativeBurnVectorAvailable
                        ? F(n.AuthoritativeBurnNose)
                        : string.Empty,
                    n.AuthoritativeBurnVectorAvailable
                        ? F(n.AuthoritativeBurnReferenceForward)
                        : string.Empty
                });
        }
'@

    if (-not $packet.Contains($oldSerialize)) {
        throw "Could not locate OrbitMap maneuver-node serializer."
    }

    $packet =
        $packet.Replace(
            $oldSerialize,
            $newSerialize)
}

if (-not $packet.Contains("f.Length != 10 && f.Length != 14")) {
    $oldParse = @'
        private static bool TryParseNode(string text, out OrbitMapManeuverNode node)
        {
            node = null;
            string[] f = text.Split('~');
            int index;
            int patchIndex;
            if (f.Length != 10 || !int.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) || !int.TryParse(f[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out patchIndex)) return false;
            double d;
            OrbitMapManeuverNode v = new OrbitMapManeuverNode();
            v.Index = index;
            v.PatchIndex = patchIndex;
            if (!TryFinite(f[1], out d)) return false; v.UniversalTimeSeconds = d;
            if (!TryFinite(f[2], out d)) return false; v.ProgradeDeltaVMetersPerSecond = d;
            if (!TryFinite(f[3], out d)) return false; v.NormalDeltaVMetersPerSecond = d;
            if (!TryFinite(f[4], out d)) return false; v.RadialDeltaVMetersPerSecond = d;
            if (!TryFinite(f[5], out d)) return false; v.TotalDeltaVMetersPerSecond = d;
            if (!TryFinite(f[6], out d)) return false; v.PositionX = d;
            if (!TryFinite(f[7], out d)) return false; v.PositionY = d;
            if (!TryFinite(f[8], out d)) return false; v.PositionZ = d;
            node = v;
            return true;
        }
'@

    $newParse = @'
        private static bool TryParseNode(string text, out OrbitMapManeuverNode node)
        {
            node = null;
            string[] f = text.Split('~');
            int index;
            int patchIndex;

            if ((f.Length != 10 && f.Length != 14) ||
                !int.TryParse(
                    f[0],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out index) ||
                !int.TryParse(
                    f[9],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out patchIndex))
            {
                return false;
            }

            double d;
            OrbitMapManeuverNode v =
                new OrbitMapManeuverNode();

            v.Index = index;
            v.PatchIndex = patchIndex;

            if (!TryFinite(f[1], out d)) return false;
            v.UniversalTimeSeconds = d;

            if (!TryFinite(f[2], out d)) return false;
            v.ProgradeDeltaVMetersPerSecond = d;

            if (!TryFinite(f[3], out d)) return false;
            v.NormalDeltaVMetersPerSecond = d;

            if (!TryFinite(f[4], out d)) return false;
            v.RadialDeltaVMetersPerSecond = d;

            if (!TryFinite(f[5], out d)) return false;
            v.TotalDeltaVMetersPerSecond = d;

            if (!TryFinite(f[6], out d)) return false;
            v.PositionX = d;

            if (!TryFinite(f[7], out d)) return false;
            v.PositionY = d;

            if (!TryFinite(f[8], out d)) return false;
            v.PositionZ = d;

            /*
             * Legacy 10-field packets remain valid. New 14-field packets
             * carry KSP's authoritative future-node steering direction.
             */
            if (f.Length == 14)
            {
                if (f[10] != "0" && f[10] != "1")
                {
                    return false;
                }

                v.AuthoritativeBurnVectorAvailable =
                    f[10] == "1";

                if (v.AuthoritativeBurnVectorAvailable)
                {
                    if (!TryFinite(f[11], out d)) return false;
                    v.AuthoritativeBurnRight = d;

                    if (!TryFinite(f[12], out d)) return false;
                    v.AuthoritativeBurnNose = d;

                    if (!TryFinite(f[13], out d)) return false;
                    v.AuthoritativeBurnReferenceForward = d;

                    double magnitude =
                        Math.Sqrt(
                            v.AuthoritativeBurnRight *
                            v.AuthoritativeBurnRight +
                            v.AuthoritativeBurnNose *
                            v.AuthoritativeBurnNose +
                            v.AuthoritativeBurnReferenceForward *
                            v.AuthoritativeBurnReferenceForward);

                    if (magnitude < 0.95 ||
                        magnitude > 1.05)
                    {
                        return false;
                    }
                }
            }

            node = v;
            return true;
        }
'@

    if (-not $packet.Contains($oldParse)) {
        throw "Could not locate OrbitMap maneuver-node parser."
    }

    $packet =
        $packet.Replace(
            $oldParse,
            $newParse)
}

WriteText $packetPath $packet

# ===========================================================================
# 2. KSP PLUGIN ORBIT-MAP SENDER
#    Use KSP's own ManeuverNode.GetBurnVector(active.orbit).
# ===========================================================================
$senderPath =
    Join-Path $RepoRoot "KMC.Plugin\OrbitMapTelemetrySender.cs"

$sender =
    ReadText $senderPath

if (-not $sender.Contains("AuthoritativeBurnVectorAvailable = true")) {
    $oldBuild = @'
                item.TotalDeltaVMetersPerSecond = dv.magnitude;
                item.PositionX = position.x;
                item.PositionY = position.y;
                item.PositionZ = position.z;
                item.PatchIndex = 0;
                packet.ManeuverNodes.Add(item);
'@

    $newBuild = @'
                item.TotalDeltaVMetersPerSecond = dv.magnitude;
                item.PositionX = position.x;
                item.PositionY = position.y;
                item.PositionZ = position.z;
                item.PatchIndex = 0;

                /*
                 * 14.22.71:
                 * Do not reconstruct a far-future maneuver direction from the
                 * vessel's current P/N/R frame. Ask KSP for the exact burn
                 * vector it uses for this ManeuverNode at node UT.
                 */
                if (active.ReferenceTransform != null)
                {
                    try
                    {
                        Vector3d burnVector =
                            node.GetBurnVector(
                                active.orbit);

                        double burnMagnitude =
                            burnVector.magnitude;

                        if (burnMagnitude > 1.0e-9 &&
                            IsFinite(burnMagnitude))
                        {
                            burnVector =
                                burnVector /
                                burnMagnitude;

                            Vector3d vesselRight =
                                active.ReferenceTransform.right;

                            Vector3d vesselNose =
                                active.ReferenceTransform.up;

                            Vector3d vesselReferenceForward =
                                active.ReferenceTransform.forward;

                            item.AuthoritativeBurnRight =
                                Vector3d.Dot(
                                    burnVector,
                                    vesselRight);

                            item.AuthoritativeBurnNose =
                                Vector3d.Dot(
                                    burnVector,
                                    vesselNose);

                            item.AuthoritativeBurnReferenceForward =
                                Vector3d.Dot(
                                    burnVector,
                                    vesselReferenceForward);

                            double resolvedMagnitude =
                                Math.Sqrt(
                                    item.AuthoritativeBurnRight *
                                    item.AuthoritativeBurnRight +
                                    item.AuthoritativeBurnNose *
                                    item.AuthoritativeBurnNose +
                                    item.AuthoritativeBurnReferenceForward *
                                    item.AuthoritativeBurnReferenceForward);

                            if (resolvedMagnitude > 1.0e-9 &&
                                IsFinite(resolvedMagnitude))
                            {
                                item.AuthoritativeBurnRight /=
                                    resolvedMagnitude;

                                item.AuthoritativeBurnNose /=
                                    resolvedMagnitude;

                                item.AuthoritativeBurnReferenceForward /=
                                    resolvedMagnitude;

                                item.AuthoritativeBurnVectorAvailable = true;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            "[KMC] Orbit-map authoritative maneuver vector unavailable: " +
                            ex.Message);
                    }
                }

                packet.ManeuverNodes.Add(item);
'@

    if (-not $sender.Contains($oldBuild)) {
        throw "Could not locate OrbitMap BuildNodes insertion point."
    }

    $sender =
        $sender.Replace(
            $oldBuild,
            $newBuild)
}

WriteText $senderPath $sender

# ===========================================================================
# 3. NAVIGATION REGRESSION
#    Protocol roundtrip and legacy compatibility.
# ===========================================================================
$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

if (-not $tests.Contains(
        'Run("OrbitMap maneuver nodes preserve KSP-authoritative burn vector"')) {

    $anchor =
        '        Run("mixed maneuver guidance uses resultant PNR vector", MixedManeuverGuidanceRegression);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.70 NavigationTests run anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("OrbitMap maneuver nodes preserve KSP-authoritative burn vector", OrbitMapAuthoritativeManeuverVectorRoundTrip);')
}

if (-not $tests.Contains(
        "private static void OrbitMapAuthoritativeManeuverVectorRoundTrip()")) {

    $insert =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($insert)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void OrbitMapAuthoritativeManeuverVectorRoundTrip()
    {
        OrbitMapPacket packet =
            new OrbitMapPacket
            {
                Sequence = 1,
                TimestampUtc = DateTime.UtcNow,
                VesselId = "VESSEL",
                VesselName = "TEST",
                UniversalTimeSeconds = 1000.0,
                ReferenceBodyName = "Kerbin",
                ReferenceBodyRadiusMeters = 600000.0,
                ReferenceBodySoiRadiusMeters = 84159286.0,
                ActiveOrbit =
                    new OrbitMapOrbit
                    {
                        ReferenceBodyName = "Kerbin",
                        SemiMajorAxisMeters = 700000.0,
                        Eccentricity = 0.0,
                        InclinationDegrees = 0.0,
                        LongitudeOfAscendingNodeDegrees = 0.0,
                        ArgumentOfPeriapsisDegrees = 0.0,
                        EpochUniversalTimeSeconds = 0.0,
                        MeanAnomalyAtEpochRadians = 0.0,
                        ApoapsisMeters = 100000.0,
                        PeriapsisMeters = 100000.0,
                        PeriodSeconds = 2000.0,
                        PositionX = 700000.0,
                        PositionY = 0.0,
                        PositionZ = 0.0
                    }
            };

        packet.ManeuverNodes.Add(
            new OrbitMapManeuverNode
            {
                Index = 0,
                UniversalTimeSeconds = 5000.0,
                ProgradeDeltaVMetersPerSecond = 1000.0,
                NormalDeltaVMetersPerSecond = -100.0,
                RadialDeltaVMetersPerSecond = 20.0,
                TotalDeltaVMetersPerSecond =
                    Math.Sqrt(
                        1000.0 * 1000.0 +
                        100.0 * 100.0 +
                        20.0 * 20.0),
                PositionX = 1.0,
                PositionY = 2.0,
                PositionZ = 3.0,
                PatchIndex = 0,
                AuthoritativeBurnVectorAvailable = true,
                AuthoritativeBurnRight = 0.25,
                AuthoritativeBurnNose = 0.75,
                AuthoritativeBurnReferenceForward =
                    Math.Sqrt(0.375)
            });

        string serialized =
            packet.Serialize();

        OrbitMapPacket parsed;

        Check(
            OrbitMapPacket.TryParse(
                serialized,
                out parsed),
            "extended OrbitMap packet did not parse");

        Check(
            parsed != null &&
            parsed.ManeuverNodes.Count == 1,
            "extended OrbitMap maneuver node missing");

        OrbitMapManeuverNode node =
            parsed.ManeuverNodes[0];

        Check(
            node.AuthoritativeBurnVectorAvailable,
            "authoritative burn-vector flag lost");

        Near(
            node.AuthoritativeBurnRight,
            0.25,
            1e-12);

        Near(
            node.AuthoritativeBurnNose,
            0.75,
            1e-12);

        Near(
            node.AuthoritativeBurnReferenceForward,
            Math.Sqrt(0.375),
            1e-12);

        /*
         * Legacy 10-field node remains accepted.
         */
        string extendedNode =
            string.Join(
                "~",
                new[]
                {
                    "0",
                    "5000",
                    "1000",
                    "-100",
                    "20",
                    "1005.18654985",
                    "1",
                    "2",
                    "3",
                    "0",
                    "1",
                    "0.25",
                    "0.75",
                    Math.Sqrt(0.375).ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture)
                });

        string legacyNode =
            string.Join(
                "~",
                extendedNode.Split('~'),
                0,
                10);

        string legacyPacket =
            serialized.Replace(
                extendedNode,
                legacyNode);

        Check(
            OrbitMapPacket.TryParse(
                legacyPacket,
                out parsed),
            "legacy 10-field OrbitMap maneuver node no longer parses");

        Check(
            parsed.ManeuverNodes.Count == 1 &&
            !parsed.ManeuverNodes[0].AuthoritativeBurnVectorAvailable,
            "legacy maneuver node invented authoritative vector");
    }


'@

    $tests =
        $tests.Replace(
            $insert,
            $method + $insert)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.71 OrbitMap authoritative maneuver-vector telemetry."
Write-Host "This build changes KMC.shared and KMC.Plugin."
Write-Host "Rebuild the solution and replace KMC.Plugin.dll in KSP before runtime validation."
