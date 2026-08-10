using System;
using System.Diagnostics;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;

namespace KMC.Engine.Guidance
{
    public sealed class GuidanceSystem
    {
        private const double AlignmentHoldToleranceDegrees = 2.0;
        private const double IgnitionStandbySeconds = 10.0;
        private const double DiagnosticIntervalSeconds = 1.0;

        private GuidanceSolutionModel _latest =
            new GuidanceSolutionModel();

        private DateTime _lastDiagnosticUtc =
            DateTime.MinValue;

        public void Update(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            DateTime receivedUtc)
        {
            _latest =
                BuildSolution(
                    orbit,
                    plan);

            if (receivedUtc - _lastDiagnosticUtc >=
                TimeSpan.FromSeconds(DiagnosticIntervalSeconds))
            {
                _lastDiagnosticUtc =
                    receivedUtc;

                WriteDiagnostic(
                    _latest);
            }
        }

        public GuidanceSolutionModel GetLatest()
        {
            return
                _latest != null
                    ? _latest.Clone()
                    : new GuidanceSolutionModel();
        }

        private static GuidanceSolutionModel BuildSolution(
            OrbitModel orbit,
            ManeuverPlanModel plan)
        {
            GuidanceSolutionModel solution =
                new GuidanceSolutionModel();

            if (plan == null ||
                !plan.Available)
            {
                solution.Status =
                    "MANEUVER PLAN UNAVAILABLE";

                return solution;
            }

            solution.PlanId =
                plan.PlanId ?? string.Empty;

            solution.TimeToNodeSeconds =
                plan.TimeToNodeSeconds;

            solution.TimeToIgnitionSeconds =
                IsFinite(plan.TimeToNodeSeconds) &&
                IsFinite(plan.IgnitionLeadSeconds)
                    ? plan.TimeToNodeSeconds -
                      plan.IgnitionLeadSeconds
                    : double.NaN;

            solution.PlannedDeltaVMetersPerSecond =
                plan.TotalDeltaVMetersPerSecond;

            solution.BurnDurationSeconds =
                plan.EstimatedBurnDurationSeconds;

            if (orbit == null ||
                !orbit.Available ||
                orbit.VelocityVector == null ||
                !orbit.VelocityVector.Available)
            {
                solution.Mode =
                    "MANEUVER GUIDANCE";
                solution.Command =
                    "AWAIT TRUE ORBITAL VECTOR";
                solution.AttitudeReference =
                    "ORBITAL PROGRADE";
                solution.Status =
                    "VECTOR UNAVAILABLE";
                solution.Evidence =
                    "Engine maneuver plan available; true orbital velocity vector unavailable.";
                return solution;
            }

            VelocityVectorTelemetryModel vector =
                orbit.VelocityVector;

            double magnitude =
                vector.OrbitalMagnitudeMetersPerSecond;

            if (!IsFinite(magnitude) ||
                magnitude < 1.0)
            {
                solution.Status =
                    "VECTOR INVALID";
                solution.Command =
                    "HOLD ATTITUDE";
                return solution;
            }

            double right =
                vector.OrbitalRightMetersPerSecond /
                magnitude;

            double nose =
                vector.OrbitalNoseMetersPerSecond /
                magnitude;

            double forward =
                vector.OrbitalReferenceForwardMetersPerSecond /
                magnitude;

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
                    Math.Acos(nose));

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
            solution.Mode =
                "MANEUVER GUIDANCE";
            solution.AttitudeReference =
                "TRUE ORBITAL PROGRADE";
            solution.Status =
                "GUIDANCE VALID";
            solution.Evidence =
                "Maneuver vector from Engine plan; attitude reference from verified ORBIT velocity vector.";

            bool aligned =
                solution.AlignmentErrorDegrees <=
                AlignmentHoldToleranceDegrees;

            if (IsFinite(solution.TimeToNodeSeconds) &&
                solution.TimeToNodeSeconds < 0.0)
            {
                solution.Command =
                    "MANEUVER WINDOW PASSED";
                solution.ThrottleAdvisory =
                    "THROTTLE 0%";
                solution.Status =
                    "REPLAN REQUIRED";
                return solution;
            }

            if (!aligned)
            {
                solution.Command =
                    "ALIGN TO MANEUVER VECTOR";
                solution.ThrottleAdvisory =
                    "THROTTLE 0%";
                return solution;
            }

            if (IsFinite(solution.TimeToIgnitionSeconds) &&
                solution.TimeToIgnitionSeconds <= 0.0)
            {
                solution.Command =
                    "IGNITE / HOLD MANEUVER VECTOR";
                solution.ThrottleAdvisory =
                    "THROTTLE 100%";
                solution.Status =
                    "IGNITION DUE";
                return solution;
            }

            if (IsFinite(solution.TimeToIgnitionSeconds) &&
                solution.TimeToIgnitionSeconds <=
                    IgnitionStandbySeconds)
            {
                solution.Command =
                    "IGNITION STANDBY";
                solution.ThrottleAdvisory =
                    "THROTTLE 0% / READY 100%";
                solution.Status =
                    "FINAL COUNT";
                return solution;
            }

            solution.Command =
                "HOLD MANEUVER VECTOR";
            solution.ThrottleAdvisory =
                "THROTTLE 0%";

            return solution;
        }

        private static void WriteDiagnostic(
            GuidanceSolutionModel guidance)
        {
            if (guidance == null)
            {
                return;
            }

            Debug.WriteLine(
                "KMC.Engine GUIDANCE" +
                " | Available=" + guidance.Available +
                " | PlanId=" + guidance.PlanId +
                " | Mode=" + guidance.Mode +
                " | Command=" + guidance.Command +
                " | Attitude=" + guidance.AttitudeReference +
                " | Throttle=" + guidance.ThrottleAdvisory +
                " | AlignErr=" + Format(guidance.AlignmentErrorDegrees, "0.00") + "deg" +
                " | LatErr=" + Format(guidance.LateralErrorDegrees, "0.00") + "deg" +
                " | VertErr=" + Format(guidance.VerticalErrorDegrees, "0.00") + "deg" +
                " | TNode=" + Format(guidance.TimeToNodeSeconds, "0.0") + "s" +
                " | TIgn=" + Format(guidance.TimeToIgnitionSeconds, "0.0") + "s" +
                " | PlanDV=" + Format(guidance.PlannedDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | Burn=" + Format(guidance.BurnDurationSeconds, "0.00") + "s" +
                " | Status=" + guidance.Status);
        }

        private static string Format(
            double value,
            string format)
        {
            return
                IsFinite(value)
                    ? value.ToString(format)
                    : "--";
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value));
        }

        private static double RadiansToDegrees(
            double radians)
        {
            return
                radians *
                (180.0 / Math.PI);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
