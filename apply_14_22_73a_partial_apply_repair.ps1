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

function ReplaceMethodRange(
    [string]$relativePath,
    [string]$startMarker,
    [string]$nextMarker,
    [string]$replacement)
{
    $path =
        Join-Path $RepoRoot $relativePath

    $text =
        ReadText $path

    $start =
        $text.IndexOf(
            $startMarker,
            [StringComparison]::Ordinal)

    if ($start -lt 0) {
        throw "Could not locate $startMarker in $relativePath"
    }

    $next =
        $text.IndexOf(
            $nextMarker,
            $start + $startMarker.Length,
            [StringComparison]::Ordinal)

    if ($next -lt 0) {
        throw "Could not locate next method $nextMarker in $relativePath"
    }

    $prefix =
        $text.Substring(
            0,
            $start)

    $suffix =
        $text.Substring(
            $next)

    $newText =
        $prefix +
        $replacement +
        "`r`n`r`n        " +
        $suffix.TrimStart()

    WriteText $path $newText
}

# ===========================================================================
# 1. REPAIR / COMPLETE ALL FIVE TIME FORMATTERS
# ===========================================================================

$mapMethod = @'
private static string FormatTransferInterval(double seconds)
        {
            if (!IsFinite(seconds) || seconds < 0.0)
            {
                return "---";
            }

            return
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(
                    seconds);
        }
'@

ReplaceMethodRange `
    "KMC.MissionControl\Pages\MapPage.cs" `
    "private static string FormatTransferInterval(double seconds)" `
    "private void EnsureTransferSelection(" `
    $mapMethod

$maneuverMethod = @'
private static string FormatDuration(double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            return
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(
                    seconds);
        }
'@

ReplaceMethodRange `
    "KMC.MissionControl\Pages\ManeuverPage.cs" `
    "private static string FormatDuration(double seconds)" `
    "private static bool IsFinite(" `
    $maneuverMethod

$guidanceMethod = @'
private static string FormatDuration(
            double seconds)
        {
            if (!IsFinite(seconds) || seconds < 0.0)
            {
                return "---";
            }

            return
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(
                    seconds);
        }
'@

ReplaceMethodRange `
    "KMC.MissionControl\Pages\GuidancePage.cs" `
    "private static string FormatDuration(" `
    "private static string FormatDeltaV(" `
    $guidanceMethod

$orbitMethod = @'
private static string FormatDuration(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) || totalSeconds < 0.0)
            {
                return "---";
            }

            return
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(
                    totalSeconds);
        }
'@

ReplaceMethodRange `
    "KMC.MissionControl\Pages\OrbitPage.cs" `
    "private static string FormatDuration(" `
    "private static string FormatSignedDuration(" `
    $orbitMethod

$footerMethod = @'
private static string FormatMissionTime(
            double seconds)
        {
            if (!IsFinite(seconds) || seconds < 0.0)
            {
                return "---";
            }

            return
                KMC.MissionControl.Telemetry.KspTimeDisplay.FormatDuration(
                    seconds);
        }
'@

ReplaceMethodRange `
    "KMC.MissionControl\Rendering\Ascent\FooterRenderer.cs" `
    "private static string FormatMissionTime(" `
    "private static string FormatDistance(" `
    $footerMethod

# ===========================================================================
# 2. VERIFY THE PARTIAL APPLY DID CREATE THE CENTRAL TIME SOURCE
# ===========================================================================

$storePath =
    Join-Path $RepoRoot "KMC.MissionControl\Telemetry\OrbitMapSnapshotStore.cs"

$store =
    ReadText $storePath

if (-not $store.Contains("public static class KspTimeDisplay")) {
    throw "14.22.73 partial apply did not create KspTimeDisplay. Stop and report this message."
}

$packetPath =
    Join-Path $RepoRoot "KMC.shared\OrbitMapPacket.cs"

$packet =
    ReadText $packetPath

if (-not $packet.Contains("CalendarDayLengthSeconds")) {
    throw "14.22.73 partial apply did not extend OrbitMapPacket calendar telemetry. Stop and report this message."
}

$senderPath =
    Join-Path $RepoRoot "KMC.Plugin\OrbitMapTelemetrySender.cs"

$sender =
    ReadText $senderPath

if (-not $sender.Contains("packet.CalendarDayLengthSeconds")) {
    throw "14.22.73 partial apply did not add KSP calendar telemetry sender. Stop and report this message."
}

# ===========================================================================
# 3. VERIFY STANDALONE UDP 5103 EXPERIMENT IS REMOVED
# ===========================================================================

$velocitySenderPath =
    Join-Path $RepoRoot "KMC.Plugin\VelocityVectorTelemetrySender.cs"

$velocitySender =
    ReadText $velocitySenderPath

if ($velocitySender.Contains("KMC-MNVV1") -or
    $velocitySender.Contains("ManeuverVectorTelemetryPort")) {
    throw "Standalone KMC-MNVV1 plugin sender was not fully removed by the partial apply."
}

$transportPath =
    Join-Path $RepoRoot "KMC.MissionControl\Transport\VelocityVectorTelemetrySample.cs"

$transport =
    ReadText $transportPath

if ($transport.Contains("ManeuverVectorTelemetryReceiver")) {
    throw "Standalone ManeuverVectorTelemetryReceiver was not fully removed by the partial apply."
}

# ===========================================================================
# 4. COMPLETE REGRESSION UPDATES THAT THE FAILED SCRIPT NEVER REACHED
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

$oldTimeTest = @'
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

$newTimeTest = @'
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

if ($tests.Contains($oldTimeTest)) {
    $tests =
        $tests.Replace(
            $oldTimeTest,
            $newTimeTest)
}
elseif (-not $tests.Contains("KSP Kerbin-time convention: 6-hour days.")) {
    throw "Could not locate mission-time regression for 14.22.73a repair."
}

if (-not $tests.Contains(
        'Run("standalone maneuver-vector UDP path removed"')) {

    $runAnchor =
        '        Run("GUID selects OrbitMap authoritative vector by vessel and node UT", ManeuverVectorStoreExactMatch);'

    if (-not $tests.Contains($runAnchor)) {
        throw "Could not locate 14.22.72 regression run anchor."
    }

    $tests =
        $tests.Replace(
            $runAnchor,
            $runAnchor + "`r`n" +
            '        Run("standalone maneuver-vector UDP path removed", StandaloneManeuverVectorUdpRemoved);')
}

if (-not $tests.Contains(
        "private static void StandaloneManeuverVectorUdpRemoved()")) {

    $methodAnchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($methodAnchor)) {
        throw "Could not locate NavigationTests method anchor."
    }

    $cleanupTest = @'
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

    $tests =
        $tests.Replace(
            $methodAnchor,
            $cleanupTest + $methodAnchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.73a partial-apply repair."
Write-Host "All five time formatters now use KSP calendar telemetry."
Write-Host "UDP 5103 cleanup verified."
Write-Host "Run NavigationTests next."
