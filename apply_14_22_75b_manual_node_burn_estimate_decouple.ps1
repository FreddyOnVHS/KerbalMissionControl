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

$plannerPath =
    Join-Path $RepoRoot "KMC.Engine\Maneuver\ApsisManeuverPlanner.cs"

$text =
    ReadText $plannerPath

function PatchManualMethod(
    [string]$text,
    [string]$methodName,
    [string]$nextMethodName)
{
    $startMarker =
        "        private ManeuverPlanModel " +
        $methodName +
        "("

    $nextMarker =
        "        private ManeuverPlanModel " +
        $nextMethodName +
        "("

    $start =
        $text.IndexOf(
            $startMarker,
            [StringComparison]::Ordinal)

    if ($start -lt 0) {
        throw "Could not locate $methodName"
    }

    $end =
        $text.IndexOf(
            $nextMarker,
            $start + $startMarker.Length,
            [StringComparison]::Ordinal)

    if ($end -lt 0) {
        throw "Could not locate end of $methodName"
    }

    $method =
        $text.Substring(
            $start,
            $end - $start)

    # Remove the hard rejection on unavailable burn estimate.
    $method =
        [regex]::Replace(
            $method,
            '(?ms)\r?\n\s*if\s*\(\s*!IsFinite\(\s*burnDuration\s*\)\s*\)\s*\{\s*plan\.Status\s*=\s*"BURN ESTIMATE UNAVAILABLE";\s*plan\.Evidence\.Add\(\s*"Mass/thrust telemetry cannot produce a burn-duration estimate\."\s*\);\s*return plan;\s*\}',
            '')

    # Compute ignition lead only when a real estimate exists.
    $method =
        [regex]::Replace(
            $method,
            'double ignitionLead\s*=\s*burnDuration\s*/\s*2\.0\s*;',
            @'
double ignitionLead =
                IsFinite(
                    burnDuration)
                    ? burnDuration / 2.0
                    : double.NaN;
'@)

    # Preserve NaN timing instead of fabricating a midpoint.
    $method =
        [regex]::Replace(
            $method,
            'plan\.IgnitionMissionTimeSeconds\s*=\s*nodeMissionTime\s*-\s*ignitionLead\s*;',
            @'
plan.IgnitionMissionTimeSeconds =
                IsFinite(
                    ignitionLead)
                    ? nodeMissionTime -
                      ignitionLead
                    : double.NaN;
'@)

    # Add explicit evidence before return if not already present.
    if ($method.IndexOf(
            "Manual node remains valid even when burn-duration telemetry is unavailable.",
            [StringComparison]::Ordinal) -lt 0) {

        $returnMarker =
            "            return plan;"

        $lastReturn =
            $method.LastIndexOf(
                $returnMarker,
                [StringComparison]::Ordinal)

        if ($lastReturn -lt 0) {
            throw "Could not locate final return in $methodName"
        }

        $evidence = @'
            if (!IsFinite(
                    burnDuration))
            {
                plan.Evidence.Add(
                    "Manual node remains valid even when burn-duration telemetry is unavailable.");

                plan.Evidence.Add(
                    "Burn duration and ignition lead remain unavailable; KSP node epoch and maneuver vector are still valid.");
            }

'@

        $method =
            $method.Substring(0, $lastReturn) +
            $evidence +
            $method.Substring($lastReturn)
    }

    return
        $text.Substring(0, $start) +
        $method +
        $text.Substring($end)
}

$text =
    PatchManualMethod `
        $text `
        "CalculateManualProgradeRetrograde" `
        "CalculateManualNormalAntiNormal"

$text =
    PatchManualMethod `
        $text `
        "CalculateManualNormalAntiNormal" `
        "CalculateManualRadialInOut"

# Radial is followed by ValidateTargetPeriapsis, not another ManeuverPlan method.
$startMarker =
    "        private ManeuverPlanModel CalculateManualRadialInOut("

$nextMarker =
    "        private static bool ValidateTargetPeriapsis("

$start =
    $text.IndexOf(
        $startMarker,
        [StringComparison]::Ordinal)

$end =
    $text.IndexOf(
        $nextMarker,
        $start + $startMarker.Length,
        [StringComparison]::Ordinal)

if ($start -lt 0 -or $end -lt 0) {
    throw "Could not locate radial manual method."
}

$radial =
    $text.Substring(
        $start,
        $end - $start)

$radial =
    [regex]::Replace(
        $radial,
        '(?ms)\r?\n\s*if\s*\(\s*!IsFinite\(burnDuration\)\s*\)\s*\{\s*plan\.Status\s*=\s*"BURN ESTIMATE UNAVAILABLE";\s*plan\.Evidence\.Add\(\s*"Mass/thrust telemetry cannot produce a burn-duration estimate\."\s*\);\s*return plan;\s*\}',
        '')

$radial =
    [regex]::Replace(
        $radial,
        'double ignitionLead\s*=\s*burnDuration\s*/\s*2\.0\s*;',
        @'
double ignitionLead =
                IsFinite(burnDuration)
                    ? burnDuration / 2.0
                    : double.NaN;
'@)

$radial =
    [regex]::Replace(
        $radial,
        'plan\.IgnitionMissionTimeSeconds\s*=\s*nodeMissionTime\s*-\s*ignitionLead\s*;',
        @'
plan.IgnitionMissionTimeSeconds =
                IsFinite(ignitionLead)
                    ? nodeMissionTime - ignitionLead
                    : double.NaN;
'@)

if ($radial.IndexOf(
        "Manual node remains valid even when burn-duration telemetry is unavailable.",
        [StringComparison]::Ordinal) -lt 0) {

    $returnMarker =
        "            return plan;"

    $lastReturn =
        $radial.LastIndexOf(
            $returnMarker,
            [StringComparison]::Ordinal)

    if ($lastReturn -lt 0) {
        throw "Could not locate radial final return."
    }

    $evidence = @'
            if (!IsFinite(burnDuration))
            {
                plan.Evidence.Add(
                    "Manual node remains valid even when burn-duration telemetry is unavailable.");

                plan.Evidence.Add(
                    "Burn duration and ignition lead remain unavailable; KSP node epoch and maneuver vector are still valid.");
            }

'@

    $radial =
        $radial.Substring(0, $lastReturn) +
        $evidence +
        $radial.Substring($lastReturn)
}

$text =
    $text.Substring(0, $start) +
    $radial +
    $text.Substring($end)

WriteText $plannerPath $text

# ===========================================================================
# Regression: no manual maneuver may be hard-rejected solely for burn estimate
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

if (-not $tests.Contains(
        'Run("manual maneuver nodes survive unavailable burn estimate"')) {

    $anchor =
        '        Run("MNV compute and upload actions expose operator feedback", ManeuverOperatorFeedbackRegression);'

    if (-not $tests.Contains($anchor)) {
        $anchor =
            '        Run("GUID final count and ignition cues are explicit", GuidanceFinalCountCueRegression);'
    }

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("manual maneuver nodes survive unavailable burn estimate", ManualManeuverBurnEstimateDecouplingRegression);')
}

if (-not $tests.Contains(
        "private static void ManualManeuverBurnEstimateDecouplingRegression()")) {

    $methodAnchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($methodAnchor)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void ManualManeuverBurnEstimateDecouplingRegression()
    {
        string plannerPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Maneuver",
                "ApsisManeuverPlanner.cs");

        Check(
            File.Exists(plannerPath),
            "manual maneuver planner source unavailable");

        string planner =
            File.ReadAllText(
                plannerPath);

        Check(
            planner.IndexOf(
                "Manual node remains valid even when burn-duration telemetry is unavailable.",
                StringComparison.Ordinal) >= 0,
            "manual-node burn-estimate decoupling evidence missing");

        int firstManual =
            planner.IndexOf(
                "CalculateManualProgradeRetrograde",
                StringComparison.Ordinal);

        int validateTarget =
            planner.IndexOf(
                "ValidateTargetPeriapsis",
                firstManual,
                StringComparison.Ordinal);

        Check(
            firstManual >= 0 &&
            validateTarget > firstManual,
            "manual maneuver planner section unavailable");

        string manualSection =
            planner.Substring(
                firstManual,
                validateTarget - firstManual);

        Check(
            manualSection.IndexOf(
                "plan.Status = \"BURN ESTIMATE UNAVAILABLE\"",
                StringComparison.Ordinal) < 0,
            "manual maneuver still hard-rejects unavailable burn estimate");

        Check(
            manualSection.IndexOf(
                "? burnDuration / 2.0",
                StringComparison.Ordinal) >= 0 &&
            manualSection.IndexOf(
                ": double.NaN",
                StringComparison.Ordinal) >= 0,
            "manual maneuver does not preserve unknown burn timing explicitly");
    }


'@

    $tests =
        $tests.Replace(
            $methodAnchor,
            $method + $methodAnchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.75b manual-node burn-estimate decoupling."
Write-Host "Manual maneuver nodes remain valid/uploadable when burn duration is unavailable."
Write-Host "Burn duration and ignition lead remain NaN; no timing value is fabricated."
