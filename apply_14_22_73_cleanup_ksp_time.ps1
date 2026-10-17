param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path, [string]$text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

# ===========================================================================
# 1. CLEAN UP ABANDONED UDP 5103 / KMC-MNVV1 PATH
# ===========================================================================

# --- KSP sender -------------------------------------------------------------
$pluginPath =
    Join-Path $RepoRoot "KMC.Plugin\VelocityVectorTelemetrySender.cs"

$plugin =
    ReadText $pluginPath

# constants
$plugin = [regex]::Replace(
    $plugin,
    '(?ms)\r?\n\s*private const int ManeuverVectorTelemetryPort\s*=\s*5103;\s*',
    "`r`n")

$plugin = [regex]::Replace(
    $plugin,
    '(?ms)\r?\n\s*private const string ManeuverVectorProtocolId\s*=\s*"KMC-MNVV1";\s*',
    "`r`n")

# endpoint field
$plugin = [regex]::Replace(
    $plugin,
    '(?m)^\s*private IPEndPoint _maneuverVectorEndpoint;\r?\n',
    '')

# endpoint initialization
$plugin = [regex]::Replace(
    $plugin,
    '(?ms)\r?\n\s*_maneuverVectorEndpoint\s*=\s*new IPEndPoint\(\s*IPAddress\.Loopback,\s*ManeuverVectorTelemetryPort\);\s*',
    "`r`n")

# authoritative standalone send block added in 14.22.70b
$standaloneBlock = @'
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

if ($plugin.Contains($standaloneBlock)) {
    $plugin =
        $plugin.Replace(
            $standaloneBlock,
            '')
}

WriteText $pluginPath $plugin

# --- Mission Control standalone receiver -----------------------------------
$transportPath =
    Join-Path $RepoRoot "KMC.MissionControl\Transport\VelocityVectorTelemetrySample.cs"

$transport =
    ReadText $transportPath

$transport = [regex]::Replace(
    $transport,
    '(?ms)\r?\n    public sealed class ManeuverVectorTelemetrySample.*?(?=\r?\n})',
    '')

WriteText $transportPath $transport

# --- MainForm ownership/startup/handler cleanup ----------------------------
$mainPath =
    Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"

$main =
    ReadText $mainPath

$main = [regex]::Replace(
    $main,
    '(?m)^\s*private readonly ManeuverVectorTelemetryReceiver _maneuverVectorReceiver;\r?\n',
    '')

$main = [regex]::Replace(
    $main,
    '(?ms)\r?\n\s*_maneuverVectorReceiver\s*=\s*new ManeuverVectorTelemetryReceiver\(\);\s*',
    "`r`n")

$main = [regex]::Replace(
    $main,
    '(?ms)\r?\n\s*_maneuverVectorReceiver\.SampleReceived\s*\+=\s*OnManeuverVectorTelemetryReceived;\s*',
    "`r`n")

$main = [regex]::Replace(
    $main,
    '(?m)^\s*_maneuverVectorReceiver\.Start\(\);\r?\n',
    '')

$main = [regex]::Replace(
    $main,
    '(?m)^\s*_maneuverVectorReceiver\.Dispose\(\);\r?\n',
    '')

$main = [regex]::Replace(
    $main,
    '(?ms)\r?\n\s*private void OnManeuverVectorTelemetryReceived\(\s*ManeuverVectorTelemetrySample sample\).*?(?=\r?\n\s*private void )',
    "`r`n")

WriteText $mainPath $main

# ===========================================================================
# 2. KSP-AUTHORITATIVE CALENDAR DAY LENGTH IN ORBITMAP PACKET
# ===========================================================================

$packetPath =
    Join-Path $RepoRoot "KMC.shared\OrbitMapPacket.cs"

$packet =
    ReadText $packetPath

if (-not $packet.Contains("CalendarDayLengthSeconds")) {
    $propertyNeedle =
        "        public double UniversalTimeSeconds { get; set; }"

    $propertyReplacement = @'
        public double UniversalTimeSeconds { get; set; }

        /// <summary>
        /// KSP display-calendar day length. 21600 seconds for Kerbin time,
        /// 86400 seconds for Earth-style time.
        /// </summary>
        public double CalendarDayLengthSeconds { get; set; }
'@

    if (-not $packet.Contains($propertyNeedle)) {
        throw "Could not locate OrbitMapPacket UniversalTimeSeconds property."
    }

    $packet =
        $packet.Replace(
            $propertyNeedle,
            $propertyReplacement)

    $ctorNeedle =
        "            ReferenceBodyName = string.Empty;"

    $ctorReplacement = @'
            ReferenceBodyName = string.Empty;
            CalendarDayLengthSeconds = 21600.0;
'@

    $packet =
        $packet.Replace(
            $ctorNeedle,
            $ctorReplacement)

    $serializeNeedle = @'
            fields.Add(Patches.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Patches.Count; i++) fields.Add(SerializePatch(Patches[i]));
            return string.Join("|", fields.ToArray());
'@

    $serializeReplacement = @'
            fields.Add(Patches.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < Patches.Count; i++) fields.Add(SerializePatch(Patches[i]));

            /*
             * 14.22.73 optional tail field. Keeping it at the packet tail
             * preserves parsing of all earlier OrbitMap packet layouts.
             */
            fields.Add(F(CalendarDayLengthSeconds));

            return string.Join("|", fields.ToArray());
'@

    if (-not $packet.Contains($serializeNeedle)) {
        throw "Could not locate OrbitMapPacket serializer tail."
    }

    $packet =
        $packet.Replace(
            $serializeNeedle,
            $serializeReplacement)

    $parseNeedle = @'
                if (p != fields.Length) return false;
                packet = value;
'@

    $parseReplacement = @'
                /*
                 * 14.22.73 optional calendar-day tail field.
                 * Legacy packets omit it and default to Kerbin time.
                 */
                value.CalendarDayLengthSeconds = 21600.0;

                if (p < fields.Length)
                {
                    if (fields.Length - p != 1 ||
                        !TryFinite(
                            fields[p++],
                            out valueDouble) ||
                        (Math.Abs(valueDouble - 21600.0) > 0.5 &&
                         Math.Abs(valueDouble - 86400.0) > 0.5))
                    {
                        return false;
                    }

                    value.CalendarDayLengthSeconds =
                        valueDouble;
                }

                if (p != fields.Length) return false;
                packet = value;
'@

    if (-not $packet.Contains($parseNeedle)) {
        throw "Could not locate OrbitMapPacket parser tail."
    }

    $packet =
        $packet.Replace(
            $parseNeedle,
            $parseReplacement)

    WriteText $packetPath $packet
}

# ===========================================================================
# 3. KSP PLUGIN SETS CALENDAR FROM KSP'S OWN SETTING
# ===========================================================================

$orbitSenderPath =
    Join-Path $RepoRoot "KMC.Plugin\OrbitMapTelemetrySender.cs"

$orbitSender =
    ReadText $orbitSenderPath

if (-not $orbitSender.Contains("packet.CalendarDayLengthSeconds")) {
    $needle =
        "                packet.UniversalTimeSeconds = ut;"

    $replacement = @'
                packet.UniversalTimeSeconds = ut;

                /*
                 * Mirror KSP's own display convention. GameSettings.KERBIN_TIME
                 * is true for 6-hour Kerbin days and false for 24-hour days.
                 */
                packet.CalendarDayLengthSeconds =
                    GameSettings.KERBIN_TIME
                        ? 21600.0
                        : 86400.0;
'@

    if (-not $orbitSender.Contains($needle)) {
        throw "Could not locate OrbitMap sender UT assignment."
    }

    $orbitSender =
        $orbitSender.Replace(
            $needle,
            $replacement)

    WriteText $orbitSenderPath $orbitSender
}

# ===========================================================================
# 4. CENTRAL KSP TIME FORMATTER IN EXISTING TELEMETRY FILE
# ===========================================================================

$storePath =
    Join-Path $RepoRoot "KMC.MissionControl\Telemetry\OrbitMapSnapshotStore.cs"

$store =
    ReadText $storePath

if (-not $store.Contains("public static class KspTimeDisplay")) {
    $namespaceClose =
        $store.LastIndexOf("}")

    if ($namespaceClose -lt 0) {
        throw "OrbitMapSnapshotStore.cs malformed."
    }

    $helper = @'

    /// <summary>
    /// Central KSP mission-time formatter. Uses the calendar convention
    /// reported by the live KSP OrbitMap telemetry stream.
    /// </summary>
    public static class KspTimeDisplay
    {
        private const double KerbinDaySeconds = 21600.0;
        private const double EarthDaySeconds = 86400.0;

        public static double GetDayLengthSeconds()
        {
            OrbitMapPacket packet;
            DateTime receivedUtc;

            if (OrbitMapSnapshotStore.TryGetLatest(
                    out packet,
                    out receivedUtc) &&
                packet != null &&
                IsSupportedDayLength(
                    packet.CalendarDayLengthSeconds))
            {
                return
                    packet.CalendarDayLengthSeconds;
            }

            return
                KerbinDaySeconds;
        }

        public static string FormatDuration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds))
            {
                return "---";
            }

            bool negative =
                seconds < 0.0;

            double magnitude =
                Math.Abs(seconds);

            long totalSeconds =
                (long)Math.Round(
                    magnitude);

            long daySeconds =
                (long)Math.Round(
                    GetDayLengthSeconds());

            long days =
                totalSeconds /
                daySeconds;

            long remainder =
                totalSeconds %
                daySeconds;

            long hours =
                remainder /
                3600;

            long minutes =
                (remainder % 3600) /
                60;

            long secondsPart =
                remainder %
                60;

            string text =
                days.ToString() +
                "d " +
                hours.ToString("00") +
                "h " +
                minutes.ToString("00") +
                "m " +
                secondsPart.ToString("00") +
                "s";

            return
                negative
                    ? "-" + text
                    : text;
        }

        private static bool IsSupportedDayLength(
            double seconds)
        {
            return
                Math.Abs(
                    seconds -
                    KerbinDaySeconds) <
                    0.5 ||
                Math.Abs(
                    seconds -
                    EarthDaySeconds) <
                    0.5;
        }
    }
'@

    $store =
        $store.Substring(
            0,
            $namespaceClose) +
        $helper +
        "`r`n}" +
        $store.Substring(
            $namespaceClose + 1)

    WriteText $storePath $store
}

# ===========================================================================
# 5. ROUTE ALL MISSION-TIME DISPLAY HELPERS THROUGH CENTRAL FORMATTER
# ===========================================================================

function ReplaceTimeFormatter(
    [string]$relativePath,
    [string]$methodName)
{
    $path =
        Join-Path $RepoRoot $relativePath

    $text =
        ReadText $path

    $pattern =
        '(?ms)(private static string ' +
        [regex]::Escape($methodName) +
        '\s*\(\s*double seconds\s*\)\s*\{).*?(\r?\n\s*\})'

    $match =
        [regex]::Match(
            $text,
            $pattern)

    if (-not $match.Success) {
        throw "Could not locate $methodName in $relativePath"
    }

    $replacement =
        $match.Groups[1].Value +
        "`r`n            return KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(seconds);" +
        $match.Groups[2].Value

    $text =
        $text.Substring(
            0,
            $match.Index) +
        $replacement +
        $text.Substring(
            $match.Index +
            $match.Length)

    WriteText $path $text
}

ReplaceTimeFormatter "KMC.MissionControl\Pages\MapPage.cs" "FormatTransferInterval"
ReplaceTimeFormatter "KMC.MissionControl\Pages\ManeuverPage.cs" "FormatDuration"
ReplaceTimeFormatter "KMC.MissionControl\Pages\GuidancePage.cs" "FormatDuration"
ReplaceTimeFormatter "KMC.MissionControl\Pages\OrbitPage.cs" "FormatDuration"
ReplaceTimeFormatter "KMC.MissionControl\Rendering\Ascent\FooterRenderer.cs" "FormatMissionTime"

# MapPage's other wrappers already call FormatTransferInterval.

# ===========================================================================
# 6. REGRESSION UPDATE
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

# Replace the old 24-hour expectation with Kerbin-time expectation and add
# a direct Earth-day override check using a live OrbitMap snapshot.
$oldTest = @'
    private static void HumanReadableMissionTimeDisplay()
    {
        const double sample = 90061.0; // 1d 01h 01m 01s
        const string expected = "1d 01h 01m 01s";

        CheckPrivateTimeFormatter(typeof(MapPage), "FormatTransferInterval", sample, expected);
        CheckPrivateTimeFormatter(typeof(ManeuverPage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(typeof(GuidancePage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(typeof(OrbitPage), "FormatDuration", sample, expected);
        CheckPrivateTimeFormatter(
            typeof(KMC.MissionControl.Rendering.Ascent.FooterRenderer),
            "FormatMissionTime",
            sample,
            expected);
    }
'@

$newTest = @'
    private static void HumanReadableMissionTimeDisplay()
    {
        /*
         * KSP Kerbin-time convention: 6-hour days.
         * 90061 s = 4d 01h 01m 01s.
         */
        OrbitMapSnapshotStore.Clear();

        OrbitMapPacket kerbin =
            new OrbitMapPacket
            {
                CalendarDayLengthSeconds = 21600.0
            };

        OrbitMapSnapshotStore.SetLatest(
            kerbin,
            DateTime.UtcNow);

        const double sample = 90061.0;
        const string kerbinExpected = "4d 01h 01m 01s";

        CheckPrivateTimeFormatter(typeof(MapPage), "FormatTransferInterval", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(ManeuverPage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(GuidancePage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(typeof(OrbitPage), "FormatDuration", sample, kerbinExpected);
        CheckPrivateTimeFormatter(
            typeof(KMC.MissionControl.Rendering.Ascent.FooterRenderer),
            "FormatMissionTime",
            sample,
            kerbinExpected);

        OrbitMapPacket earth =
            new OrbitMapPacket
            {
                CalendarDayLengthSeconds = 86400.0
            };

        OrbitMapSnapshotStore.SetLatest(
            earth,
            DateTime.UtcNow);

        Check(
            string.Equals(
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(sample),
                "1d 01h 01m 01s",
                StringComparison.Ordinal),
            "Earth-style KSP day length not honored");

        OrbitMapSnapshotStore.Clear();
    }
'@

if ($tests.Contains($oldTest)) {
    $tests =
        $tests.Replace(
            $oldTest,
            $newTest)
}
elseif (-not $tests.Contains("KSP Kerbin-time convention: 6-hour days.")) {
    throw "Could not locate HumanReadableMissionTimeDisplay regression."
}

# Add cleanup regression to existing test list/method area.
if (-not $tests.Contains(
        'Run("standalone maneuver-vector UDP path removed"')) {

    $anchor =
        '        Run("GUID selects OrbitMap authoritative vector by vessel and node UT", ManeuverVectorStoreExactMatch);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.72 run anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("standalone maneuver-vector UDP path removed", StandaloneManeuverVectorUdpRemoved);')
}

if (-not $tests.Contains(
        "private static void StandaloneManeuverVectorUdpRemoved()")) {

    $insert =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    $method = @'
    private static void StandaloneManeuverVectorUdpRemoved()
    {
        string pluginPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Plugin",
                "VelocityVectorTelemetrySender.cs");

        string transportPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Transport",
                "VelocityVectorTelemetrySample.cs");

        Check(
            File.Exists(pluginPath),
            "VelocityVectorTelemetrySender source unavailable");

        Check(
            File.Exists(transportPath),
            "VelocityVectorTelemetrySample source unavailable");

        string plugin =
            File.ReadAllText(
                pluginPath);

        string transport =
            File.ReadAllText(
                transportPath);

        Check(
            plugin.IndexOf(
                "KMC-MNVV1",
                StringComparison.Ordinal) < 0 &&
            plugin.IndexOf(
                "ManeuverVectorTelemetryPort",
                StringComparison.Ordinal) < 0,
            "standalone plugin maneuver-vector UDP sender still present");

        Check(
            transport.IndexOf(
                "ManeuverVectorTelemetryReceiver",
                StringComparison.Ordinal) < 0,
            "standalone Mission Control maneuver-vector UDP receiver still present");
    }


'@

    if (-not $tests.Contains($insert)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $tests =
        $tests.Replace(
            $insert,
            $method + $insert)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.73 cleanup + KSP calendar-time synchronization."
Write-Host "Removed standalone UDP 5103 / KMC-MNVV1 maneuver-vector path."
Write-Host "Mission-time displays now follow KSP Kerbin/Earth time via OrbitMap UDP 5110."
Write-Host "IMPORTANT: rebuild KMC.Plugin.dll and copy it into KSP."
