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

function PatchManualBody([string]$body)
{
    # Remove the manual-only hard rejection.
    $body =
        [regex]::Replace(
            $body,
            '(?ms)\r?\n\s*if\s*\(\s*!IsFinite\s*\(\s*burnDuration\s*\)\s*\)\s*\{\s*plan\.Status\s*=\s*"BURN ESTIMATE UNAVAILABLE";\s*plan\.Evidence\.Add\s*\(\s*"Mass/thrust telemetry cannot produce a burn-duration estimate\."\s*\);\s*return plan;\s*\}',
            '')

    # Some older manual method formatting has no spaces inside IsFinite().
    $body =
        [regex]::Replace(
            $body,
            '(?ms)\r?\n\s*if\s*\(\s*!IsFinite\(burnDuration\)\s*\)\s*\{\s*plan\.Status\s*=\s*"BURN ESTIMATE UNAVAILABLE";\s*plan\.Evidence\.Add\(\s*"Mass/thrust telemetry cannot produce a burn-duration estimate\."\s*\);\s*return plan;\s*\}',
            '')

    # Unknown duration -> unknown ignition lead, not plan invalid.
    $body =
        [regex]::Replace(
            $body,
            'double ignitionLead\s*=\s*burnDuration\s*/\s*2\.0\s*;',
            @'
double ignitionLead =
                IsFinite(
                    burnDuration)
                    ? burnDuration / 2.0
                    : double.NaN;
'@)

    $body =
        [regex]::Replace(
            $body,
            'plan\.IgnitionMissionTimeSeconds\s*=\s*nodeMissionTime\s*-\s*ignitionLead\s*;',
            @'
plan.IgnitionMissionTimeSeconds =
                IsFinite(
                    ignitionLead)
                    ? nodeMissionTime -
                      ignitionLead
                    : double.NaN;
'@)

    if ($body.IndexOf(
            "Manual node remains valid even when burn-duration telemetry is unavailable.",
            [StringComparison]::Ordinal) -lt 0) {

        $returnMarker =
            "            return plan;"

        $lastReturn =
            $body.LastIndexOf(
                $returnMarker,
                [StringComparison]::Ordinal)

        if ($lastReturn -lt 0) {
            throw "Could not locate final manual-plan return."
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

        $body =
            $body.Substring(0, $lastReturn) +
            $evidence +
            $body.Substring($lastReturn)
    }

    return $body
}

$plannerPath =
    Join-Path $RepoRoot "KMC.Engine\Maneuver\ApsisManeuverPlanner.cs"

$text =
    ReadText $plannerPath

# Locate every boundary BEFORE modifying anything.
$proStartMarker =
    "        private ManeuverPlanModel CalculateManualProgradeRetrograde("

$normalStartMarker =
    "        private ManeuverPlanModel CalculateManualNormalAntiNormal("

$radialStartMarker =
    "        private ManeuverPlanModel CalculateManualRadialInOut("

$afterRadialMarker =
    "        private static bool ValidateTargetPeriapsis("

$proStart =
    $text.IndexOf(
        $proStartMarker,
        [StringComparison]::Ordinal)

$normalStart =
    $text.IndexOf(
        $normalStartMarker,
        [StringComparison]::Ordinal)

$radialStart =
    $text.IndexOf(
        $radialStartMarker,
        [StringComparison]::Ordinal)

$afterRadial =
    $text.IndexOf(
        $afterRadialMarker,
        [StringComparison]::Ordinal)

if ($proStart -lt 0) {
    throw "Could not locate CalculateManualProgradeRetrograde."
}

if ($normalStart -lt 0) {
    throw "Could not locate CalculateManualNormalAntiNormal."
}

if ($radialStart -lt 0) {
    throw "Could not locate CalculateManualRadialInOut."
}

if ($afterRadial -lt 0) {
    throw "Could not locate ValidateTargetPeriapsis boundary."
}

if (-not (
    $proStart -lt $normalStart -and
    $normalStart -lt $radialStart -and
    $radialStart -lt $afterRadial)) {
    throw "Manual maneuver method order was not as expected."
}

# Patch from bottom to top so earlier offsets remain valid.
$radialBody =
    $text.Substring(
        $radialStart,
        $afterRadial - $radialStart)

$radialBody =
    PatchManualBody $radialBody

$text =
    $text.Substring(0, $radialStart) +
    $radialBody +
    $text.Substring($afterRadial)

# Re-find only the boundary following normal because radial length changed.
$radialStart =
    $text.IndexOf(
        $radialStartMarker,
        [StringComparison]::Ordinal)

$normalBody =
    $text.Substring(
        $normalStart,
        $radialStart - $normalStart)

$normalBody =
    PatchManualBody $normalBody

$text =
    $text.Substring(0, $normalStart) +
    $normalBody +
    $text.Substring($radialStart)

# Re-find normal after its body length changed.
$normalStart =
    $text.IndexOf(
        $normalStartMarker,
        [StringComparison]::Ordinal)

$proBody =
    $text.Substring(
        $proStart,
        $normalStart - $proStart)

$proBody =
    PatchManualBody $proBody

$text =
    $text.Substring(0, $proStart) +
    $proBody +
    $text.Substring($normalStart)

# Safety verification before touching disk.
$manualStart =
    $text.IndexOf(
        $proStartMarker,
        [StringComparison]::Ordinal)

$manualEnd =
    $text.IndexOf(
        $afterRadialMarker,
        $manualStart,
        [StringComparison]::Ordinal)

$manualSection =
    $text.Substring(
        $manualStart,
        $manualEnd - $manualStart)

if ($manualSection.IndexOf(
        'plan.Status = "BURN ESTIMATE UNAVAILABLE"',
        [StringComparison]::Ordinal) -ge 0) {
    throw "Manual planner still contains hard BURN ESTIMATE UNAVAILABLE rejection. No file written."
}

$evidenceCount =
    [regex]::Matches(
        $manualSection,
        [regex]::Escape(
            "Manual node remains valid even when burn-duration telemetry is unavailable.")).Count

if ($evidenceCount -ne 3) {
    throw "Expected burn-estimate decoupling in exactly 3 manual methods; found $evidenceCount. No file written."
}

WriteText $plannerPath $text

# ---------------------------------------------------------------------------
# Regression
# ---------------------------------------------------------------------------

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

        int manualStart =
            planner.IndexOf(
                "CalculateManualProgradeRetrograde",
                StringComparison.Ordinal);

        int manualEnd =
            planner.IndexOf(
                "ValidateTargetPeriapsis",
                manualStart,
                StringComparison.Ordinal);

        Check(
            manualStart >= 0 &&
            manualEnd > manualStart,
            "manual maneuver planner section unavailable");

        string manualSection =
            planner.Substring(
                manualStart,
                manualEnd - manualStart);

        Check(
            manualSection.IndexOf(
                "plan.Status = \"BURN ESTIMATE UNAVAILABLE\"",
                StringComparison.Ordinal) < 0,
            "manual maneuver still hard-rejects unavailable burn estimate");

        int evidenceCount =
            System.Text.RegularExpressions.Regex.Matches(
                manualSection,
                System.Text.RegularExpressions.Regex.Escape(
                    "Manual node remains valid even when burn-duration telemetry is unavailable.")).Count;

        Check(
            evidenceCount == 3,
            "all three manual maneuver axes must preserve valid node without burn estimate");

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

Write-Host "Applied KMC 14.22.75b1 manual-node burn-estimate decoupling."
Write-Host "Patched PRO/RETRO, NORMAL/ANTI-NORMAL, and RADIAL OUT/IN."
Write-Host "No burn duration or ignition timing is fabricated."
