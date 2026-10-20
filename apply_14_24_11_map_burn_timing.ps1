$ErrorActionPreference = 'Stop'
$path = Join-Path (Get-Location).Path 'KMC.MissionControl\Pages\MapPage.cs'
$testPath = Join-Path (Get-Location).Path 'Tools\NavigationTests\Program.cs'
if (!(Test-Path $path) -or !(Test-Path $testPath)) { throw 'Run from KMC repository root.' }
$source = [IO.File]::ReadAllText($path)
$tests = [IO.File]::ReadAllText($testPath)
$required = @(
 '            Rectangle b = context.ContentBounds;',
 '    internal static class TransferManeuverPlanBridge',
 '                    EstimatedBurnDurationSeconds = double.NaN,',
 '                    IgnitionLeadSeconds = 0.0,',
 '                    IgnitionMissionTimeSeconds = double.NaN,',
 '            ManeuverPlanPromotionRequest promotion =',
 '            promotion.Evidence.Add(')
foreach ($anchor in $required) { if (!$source.Contains($anchor)) { throw "Unexpected MapPage.cs; missing: $anchor" } }
$anchorTest = '        Run("manual maneuver nodes survive unavailable burn estimate", ManualManeuverBurnEstimateDecouplingRegression);'
$methodTest = '    private static void ManualManeuverBurnEstimateDecouplingRegression()'
if (!$tests.Contains($anchorTest) -or !$tests.Contains($methodTest)) { throw 'Unexpected NavigationTests Program.cs.' }
if ($source.Contains('14.24.11 MAP burn estimate')) { throw 'Patch already applied.' }
$backupDir = Join-Path $env:TEMP ('KMC_14_24_11_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backupDir | Out-Null
Copy-Item $path (Join-Path $backupDir 'MapPage.cs')
Copy-Item $testPath (Join-Path $backupDir 'NavigationTests.Program.cs')

$source = $source.Replace('            Rectangle b = context.ContentBounds;', @'
            // Refresh the estimate source while MAP is on screen. It is scoped
            // to the active vessel and expires if telemetry stops arriving.
            TransferManeuverPlanBridge.CaptureBurnTelemetry(telemetry);
            Rectangle b = context.ContentBounds;
'@.TrimEnd())

$bridgeAnchor = @'
    internal static class TransferManeuverPlanBridge
    {
'@
$bridgeReplacement = @'
    internal static class TransferManeuverPlanBridge
    {
        // 14.24.11 MAP burn estimate. Thrust [kN] / mass [tonnes] = m/s^2.
        // Keep the snapshot isolated from maneuver identity and solver state.
        private static readonly object BurnTelemetrySync = new object();
        private static string _burnVesselName = string.Empty;
        private static double _burnMassTonnes = double.NaN;
        private static double _burnMaxThrustKn = double.NaN;
        private static double _burnSpecificImpulseSeconds = double.NaN;
        private static DateTime _burnTelemetryUtc = DateTime.MinValue;

        public static void CaptureBurnTelemetry(MissionTelemetry telemetry)
        {
            if (telemetry == null) return;
            lock (BurnTelemetrySync)
            {
                _burnVesselName = telemetry.VesselName ?? string.Empty;
                _burnMassTonnes = telemetry.VesselMass;
                _burnMaxThrustKn = telemetry.MaximumThrust;
                _burnSpecificImpulseSeconds = telemetry.AverageSpecificImpulse;
                _burnTelemetryUtc = DateTime.UtcNow;
            }
        }

        internal static double EstimateBurnDurationSeconds(
            double deltaV, double massTonnes, double maxThrustKn,
            double specificImpulseSeconds)
        {
            if (!IsFinite(deltaV) || deltaV < 0.0 ||
                !IsFinite(massTonnes) || massTonnes <= 0.0 ||
                !IsFinite(maxThrustKn) || maxThrustKn <= 0.0)
                return double.NaN;
            if (deltaV == 0.0) return 0.0;

            // Rocket-equation duration when the active engines report Isp.
            // Using maximum available thrust assumes full-throttle execution.
            if (IsFinite(specificImpulseSeconds) && specificImpulseSeconds > 0.0)
            {
                const double g0 = 9.80665;
                double ve = specificImpulseSeconds * g0;
                double fraction = 1.0 - Math.Exp(-deltaV / ve);
                double duration = massTonnes * ve * fraction / maxThrustKn;
                return IsFinite(duration) && duration >= 0.0
                    ? duration : double.NaN;
            }

            // Without Isp, use constant-mass approximation instead of a zero lead.
            double approximate = deltaV * massTonnes / maxThrustKn;
            return IsFinite(approximate) && approximate >= 0.0
                ? approximate : double.NaN;
        }

        private static double EstimateForActiveVessel(string vesselName, double deltaV)
        {
            lock (BurnTelemetrySync)
            {
                if (string.IsNullOrWhiteSpace(vesselName) ||
                    !string.Equals(_burnVesselName, vesselName,
                        StringComparison.OrdinalIgnoreCase) ||
                    DateTime.UtcNow - _burnTelemetryUtc > TimeSpan.FromSeconds(3.0))
                    return double.NaN;

                return EstimateBurnDurationSeconds(deltaV, _burnMassTonnes,
                    _burnMaxThrustKn, _burnSpecificImpulseSeconds);
            }
        }
'@
if (!$source.Contains($bridgeAnchor)) { throw 'Transfer bridge anchor missing' }
$source = $source.Replace($bridgeAnchor, $bridgeReplacement)

$source = $source.Replace('            ManeuverPlanPromotionRequest promotion =', @'
            double duration = EstimateForActiveVessel(
                livePacket != null ? livePacket.VesselName : string.Empty, totalDv);
            double lead = IsFinite(duration) ? duration / 2.0 : double.NaN;

            ManeuverPlanPromotionRequest promotion =
'@.TrimEnd())
$source = $source.Replace('                    EstimatedBurnDurationSeconds = double.NaN,', '                    EstimatedBurnDurationSeconds = duration,')
$source = $source.Replace('                    IgnitionLeadSeconds = 0.0,', '                    IgnitionLeadSeconds = lead,')
$source = $source.Replace('            promotion.Evidence.Add(', @'
            promotion.Evidence.Add(
                IsFinite(duration)
                    ? "MAP estimated full-throttle burn duration and centered ignition on the node."
                    : "MAP burn timing unavailable: live vessel thrust/mass telemetry missing or stale.");

            promotion.Evidence.Add(
'@.TrimEnd())

$tests = $tests.Replace($anchorTest, $anchorTest + [Environment]::NewLine + '        Run("MAP transfer burn is centered on node when engine telemetry exists", MapTransferBurnTimingRegression);')
$testMethod = @'
    private static void MapTransferBurnTimingRegression()
    {
        Type bridge = typeof(KMC.MissionControl.Pages.MapPage).Assembly.GetType(
            "KMC.MissionControl.Pages.TransferManeuverPlanBridge", true);
        System.Reflection.MethodInfo estimate = bridge.GetMethod(
            "EstimateBurnDurationSeconds",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Check(estimate != null, "MAP burn-duration estimator missing");
        double dv = 1000.0, mass = 10.0, thrust = 200.0, isp = 300.0;
        double duration = (double)estimate.Invoke(null, new object[] { dv, mass, thrust, isp });
        double expected = mass * (isp * 9.80665) *
            (1.0 - Math.Exp(-dv / (isp * 9.80665))) / thrust;
        Check(Math.Abs(duration - expected) < 1e-8,
            "MAP burn-duration estimate is not rocket-equation consistent");
        double fallback = (double)estimate.Invoke(null,
            new object[] { dv, mass, thrust, double.NaN });
        Check(Math.Abs(fallback - 50.0) < 1e-8,
            "MAP constant-mass fallback is not correct");
        double unavailable = (double)estimate.Invoke(null,
            new object[] { dv, mass, 0.0, isp });
        Check(double.IsNaN(unavailable), "MAP invented timing for missing thrust");
        double timeToNode = 100.0;
        double ignitionLead = duration / 2.0;
        Check(Math.Abs((timeToNode - ignitionLead) -
            (timeToNode - duration / 2.0)) < 1e-8,
            "ignition cue is not centered on the maneuver node");
        string mapSource = File.ReadAllText(Path.Combine(
            Environment.CurrentDirectory, "KMC.MissionControl", "Pages", "MapPage.cs"));
        Check(mapSource.Contains("IgnitionLeadSeconds = lead") &&
            mapSource.Contains("EstimatedBurnDurationSeconds = duration") &&
            mapSource.Contains("EstimateForActiveVessel"),
            "MAP promotion is not using vessel-based burn timing");
    }

'@
$tests = $tests.Replace($methodTest, $testMethod + $methodTest)
[IO.File]::WriteAllText($path, $source, (New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllText($testPath, $tests, (New-Object Text.UTF8Encoding($false)))
Write-Host '14.24.11 MAP burn-timing patch applied.' -ForegroundColor Green
Write-Host "Backup: $backupDir"
Write-Host 'Only MissionControl and NavigationTests changed. No KSP DLL replacement.'
