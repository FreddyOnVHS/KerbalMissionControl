param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

$path = Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSystem.cs"

if (-not (Test-Path $path)) {
    throw "GuidanceSystem.cs not found at $path"
}

$text = [System.IO.File]::ReadAllText($path)

if ($text.Contains("TRUE COMBINED MANEUVER VECTOR") -and
    $text.Contains("IsMixedManeuver(")) {
    Write-Host "14.22.70 true mixed maneuver-vector guidance is already applied."
    exit 0
}

$oldCall = @'
            PopulateAttitudeGuidance(
                solution,
                orbit,
                attitudeMode,
                receivedUtc);
'@

$newCall = @'
            PopulateAttitudeGuidance(
                solution,
                orbit,
                plan,
                attitudeMode,
                receivedUtc);
'@

if (-not $text.Contains($oldCall)) {
    throw "Could not locate PopulateAttitudeGuidance call. Source does not match expected 14.22.69b baseline."
}
$text = $text.Replace($oldCall, $newCall)

$oldSignature = @'
        private static void PopulateAttitudeGuidance(
            GuidanceSolutionModel solution,
            OrbitModel orbit,
            ManeuverAttitudeMode attitudeMode,
            DateTime receivedUtc)
'@

$newSignature = @'
        private static void PopulateAttitudeGuidance(
            GuidanceSolutionModel solution,
            OrbitModel orbit,
            ManeuverPlanModel plan,
            ManeuverAttitudeMode attitudeMode,
            DateTime receivedUtc)
'@

if (-not $text.Contains($oldSignature)) {
    throw "Could not locate PopulateAttitudeGuidance signature."
}
$text = $text.Replace($oldSignature, $newSignature)

$oldBranch = @'
            double right;
            double nose;
            double forward;

            if (attitudeMode ==
                    ManeuverAttitudeMode.RadialOut ||
                attitudeMode ==
                    ManeuverAttitudeMode.RadialIn)
'@

$newBranch = @'
            double right;
            double nose;
            double forward;

            /*
             * 14.22.70:
             * A stock KSP maneuver node is a vector in the orbital P/N/R
             * basis. Mixed transfer burns must point along the normalized
             * resultant, not along whichever principal axis happens to win
             * ResolveAttitudeMode().
             *
             * Pure-axis maneuvers deliberately retain the proven legacy path
             * below. Only 2- or 3-axis nodes enter this branch.
             */
            if (IsMixedManeuver(plan))
            {
                bool usePrograde =
                    IsFinite(plan.ProgradeDeltaVMetersPerSecond) &&
                    Math.Abs(plan.ProgradeDeltaVMetersPerSecond) >
                        NodeDeltaVToleranceMetersPerSecond;

                bool useNormal =
                    IsFinite(plan.NormalDeltaVMetersPerSecond) &&
                    Math.Abs(plan.NormalDeltaVMetersPerSecond) >
                        NodeDeltaVToleranceMetersPerSecond;

                bool useRadial =
                    IsFinite(plan.RadialDeltaVMetersPerSecond) &&
                    Math.Abs(plan.RadialDeltaVMetersPerSecond) >
                        NodeDeltaVToleranceMetersPerSecond;

                VelocityVectorTelemetryModel vector =
                    orbit.VelocityVector;

                double velocityMagnitude =
                    vector.OrbitalMagnitudeMetersPerSecond;

                if (usePrograde &&
                    (!IsFinite(velocityMagnitude) ||
                     velocityMagnitude < 1.0))
                {
                    solution.Evidence =
                        "Combined maneuver vector unavailable: orbital velocity vector invalid.";

                    return;
                }

                OrbitNormalTelemetryModel normal =
                    null;

                if (useNormal || useRadial)
                {
                    normal =
                        OrbitNormalTelemetryStore.GetLatest();

                    normal.EvaluateAgainstVelocity(
                        vector,
                        receivedUtc);

                    if (!normal.Available)
                    {
                        solution.Evidence =
                            "Combined maneuver vector unavailable: " +
                            normal.Status;

                        return;
                    }
                }

                RadialTelemetryModel radial =
                    null;

                if (useRadial)
                {
                    radial =
                        RadialTelemetryStore.GetLatest();

                    radial.EvaluateAgainstNormal(
                        normal,
                        receivedUtc);

                    if (!radial.Available)
                    {
                        solution.Evidence =
                            "Combined maneuver vector unavailable: " +
                            radial.Status;

                        return;
                    }
                }

                double p =
                    usePrograde
                        ? plan.ProgradeDeltaVMetersPerSecond
                        : 0.0;

                double n =
                    useNormal
                        ? plan.NormalDeltaVMetersPerSecond
                        : 0.0;

                double r =
                    useRadial
                        ? plan.RadialDeltaVMetersPerSecond
                        : 0.0;

                right = 0.0;
                nose = 0.0;
                forward = 0.0;

                if (usePrograde)
                {
                    right +=
                        p *
                        vector.OrbitalRightMetersPerSecond /
                        velocityMagnitude;

                    nose +=
                        p *
                        vector.OrbitalNoseMetersPerSecond /
                        velocityMagnitude;

                    forward +=
                        p *
                        vector.OrbitalReferenceForwardMetersPerSecond /
                        velocityMagnitude;
                }

                if (useNormal)
                {
                    right +=
                        n *
                        normal.RightComponent /
                        normal.Magnitude;

                    nose +=
                        n *
                        normal.NoseComponent /
                        normal.Magnitude;

                    forward +=
                        n *
                        normal.ReferenceForwardComponent /
                        normal.Magnitude;
                }

                if (useRadial)
                {
                    right +=
                        r *
                        radial.RightComponent /
                        radial.Magnitude;

                    nose +=
                        r *
                        radial.NoseComponent /
                        radial.Magnitude;

                    forward +=
                        r *
                        radial.ReferenceForwardComponent /
                        radial.Magnitude;
                }

                double resultantMagnitude =
                    Math.Sqrt(
                        right * right +
                        nose * nose +
                        forward * forward);

                if (!IsFinite(resultantMagnitude) ||
                    resultantMagnitude <=
                        NodeDeltaVToleranceMetersPerSecond)
                {
                    solution.Evidence =
                        "Combined maneuver vector unavailable: resultant magnitude invalid.";

                    return;
                }

                right /= resultantMagnitude;
                nose /= resultantMagnitude;
                forward /= resultantMagnitude;

                solution.CommandedRetrograde =
                    false;

                solution.AttitudeReference =
                    "TRUE COMBINED MANEUVER VECTOR";

                solution.Evidence =
                    "True mixed P/N/R maneuver attitude reference from the normalized stock-node resultant using verified prograde, normal, and radial orbital basis vectors; maneuver execution remains crew advisory.";
            }
            else if (attitudeMode ==
                    ManeuverAttitudeMode.RadialOut ||
                attitudeMode ==
                    ManeuverAttitudeMode.RadialIn)
'@

if (-not $text.Contains($oldBranch)) {
    throw "Could not locate maneuver attitude branch."
}
$text = $text.Replace($oldBranch, $newBranch)

$resolveMarker = @'
        private static ManeuverAttitudeMode ResolveAttitudeMode(
            ManeuverPlanModel plan)
'@

$helper = @'
        private static bool IsMixedManeuver(
            ManeuverPlanModel plan)
        {
            if (plan == null)
            {
                return false;
            }

            int activeAxes = 0;

            if (IsFinite(
                    plan.ProgradeDeltaVMetersPerSecond) &&
                Math.Abs(
                    plan.ProgradeDeltaVMetersPerSecond) >
                    NodeDeltaVToleranceMetersPerSecond)
            {
                activeAxes++;
            }

            if (IsFinite(
                    plan.NormalDeltaVMetersPerSecond) &&
                Math.Abs(
                    plan.NormalDeltaVMetersPerSecond) >
                    NodeDeltaVToleranceMetersPerSecond)
            {
                activeAxes++;
            }

            if (IsFinite(
                    plan.RadialDeltaVMetersPerSecond) &&
                Math.Abs(
                    plan.RadialDeltaVMetersPerSecond) >
                    NodeDeltaVToleranceMetersPerSecond)
            {
                activeAxes++;
            }

            return
                activeAxes > 1;
        }

'@

if (-not $text.Contains($resolveMarker)) {
    throw "Could not locate ResolveAttitudeMode insertion point."
}
$text = $text.Replace($resolveMarker, $helper + $resolveMarker)

[System.IO.File]::WriteAllText(
    $path,
    $text,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Applied KMC 14.22.70 true mixed maneuver-vector guidance."
