param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$path = Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSystem.cs"

if (-not (Test-Path $path)) {
    throw "GuidanceSystem.cs not found at $path"
}

$text = [System.IO.File]::ReadAllText($path)

if ($text.Contains("_burnPlanSnapshot") -and
    $text.Contains("ManeuverPlanModel.Clone(")) {
    Write-Host "14.22.70a burn-vector latch fix is already applied."
    exit 0
}

$fieldNeedle = @'
        private ManeuverAttitudeMode _burnAttitudeMode =
            ManeuverAttitudeMode.Prograde;

        private bool _burnRequiresOrbitTargetVerification;
'@

$fieldReplacement = @'
        private ManeuverAttitudeMode _burnAttitudeMode =
            ManeuverAttitudeMode.Prograde;

        /*
         * 14.22.70a:
         * Preserve the exact maneuver vector through burn execution. Without
         * this snapshot, mixed P/N/R guidance would be correct pre-burn but
         * would lose the vector when BuildBurnExecutionSolution no longer had
         * the live plan object.
         */
        private ManeuverPlanModel _burnPlanSnapshot;

        private bool _burnRequiresOrbitTargetVerification;
'@

if (-not $text.Contains($fieldNeedle)) {
    throw "Could not locate burn attitude field insertion point."
}
$text = $text.Replace($fieldNeedle, $fieldReplacement)

$burnCallOld = @'
            PopulateAttitudeGuidance(
                solution,
                orbit,
                _burnAttitudeMode,
                receivedUtc);
'@

$burnCallNew = @'
            PopulateAttitudeGuidance(
                solution,
                orbit,
                _burnPlanSnapshot,
                _burnAttitudeMode,
                receivedUtc);
'@

if (-not $text.Contains($burnCallOld)) {
    throw "Could not locate burn-execution PopulateAttitudeGuidance call."
}
$text = $text.Replace($burnCallOld, $burnCallNew)

$beginNeedle = @'
            _burnAttitudeMode =
                ResolveAttitudeMode(
                    plan);

            _burnRequiresOrbitTargetVerification =
                plan.OrbitTargetVerificationRequired;
'@

$beginReplacement = @'
            _burnAttitudeMode =
                ResolveAttitudeMode(
                    plan);

            _burnPlanSnapshot =
                ManeuverPlanModel.Clone(
                    plan);

            _burnRequiresOrbitTargetVerification =
                plan.OrbitTargetVerificationRequired;
'@

if (-not $text.Contains($beginNeedle)) {
    throw "Could not locate BeginBurn maneuver latch point."
}
$text = $text.Replace($beginNeedle, $beginReplacement)

$resetNeedle = @'
            _burnAttitudeMode =
                ManeuverAttitudeMode.Prograde;

            _burnRequiresOrbitTargetVerification =
                false;
'@

$resetReplacement = @'
            _burnAttitudeMode =
                ManeuverAttitudeMode.Prograde;

            _burnPlanSnapshot =
                null;

            _burnRequiresOrbitTargetVerification =
                false;
'@

if (-not $text.Contains($resetNeedle)) {
    throw "Could not locate ResetCompletedBurn maneuver latch point."
}
$text = $text.Replace($resetNeedle, $resetReplacement)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.22.70a mixed-vector burn latch fix."
