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
        private const double NodeStateFreshnessSeconds = 2.0;
        private const double DiagnosticIntervalSeconds = 1.0;

        private GuidanceSolutionModel _latest = new GuidanceSolutionModel();
        private DateTime _lastDiagnosticUtc = DateTime.MinValue;

        public void Update(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            DateTime receivedUtc)
        {
            _latest = BuildSolution(
                orbit,
                plan,
                GuidanceNodeStateStore.GetLatest(),
                receivedUtc);

            if (receivedUtc - _lastDiagnosticUtc >=
                TimeSpan.FromSeconds(DiagnosticIntervalSeconds))
            {
                _lastDiagnosticUtc = receivedUtc;
                WriteDiagnostic(_latest);
            }
        }

        public GuidanceSolutionModel GetLatest()
        {
            return _latest != null
                ? _latest.Clone()
                : new GuidanceSolutionModel();
        }

        private static GuidanceSolutionModel BuildSolution(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            GuidanceNodeStateModel nodeState,
            DateTime receivedUtc)
        {
            GuidanceSolutionModel solution = new GuidanceSolutionModel();

            if (plan == null || !plan.Available)
            {
                solution.Status = "MANEUVER PLAN UNAVAILABLE";
                return solution;
            }

            solution.PlanId = plan.PlanId ?? string.Empty;
            solution.TimeToNodeSeconds = plan.TimeToNodeSeconds;
            solution.TimeToIgnitionSeconds =
                IsFinite(plan.TimeToNodeSeconds) &&
                IsFinite(plan.IgnitionLeadSeconds)
                    ? plan.TimeToNodeSeconds - plan.IgnitionLeadSeconds
                    : double.NaN;
            solution.PlannedDeltaVMetersPerSecond =
                plan.TotalDeltaVMetersPerSecond;
            solution.BurnDurationSeconds =
                plan.EstimatedBurnDurationSeconds;

            PopulateNodeVerification(
                solution,
                plan,
                nodeState,
                receivedUtc);

            PopulateAttitudeGuidance(
                solution,
                orbit);

            if (!solution.ExecutionAuthorized)
            {
                ApplyNodeInterlockCommand(solution);
                return solution;
            }

            bool aligned =
                IsFinite(solution.AlignmentErrorDegrees) &&
                solution.AlignmentErrorDegrees <=
                    AlignmentHoldToleranceDegrees;

            if (IsFinite(solution.TimeToNodeSeconds) &&
                solution.TimeToNodeSeconds < 0.0)
            {
                solution.Command = "MANEUVER WINDOW PASSED";
                solution.ThrottleAdvisory = "THROTTLE 0%";
                solution.Status = "REPLAN REQUIRED";
                solution.ExecutionAuthorized = false;
                return solution;
            }

            if (!solution.ManeuverVectorAvailable)
            {
                solution.Command = "AWAIT TRUE ORBITAL VECTOR";
                solution.ThrottleAdvisory = "THROTTLE 0%";
                solution.Status = "VECTOR UNAVAILABLE";
                solution.ExecutionAuthorized = false;
                return solution;
            }

            if (!aligned)
            {
                solution.Command = "ALIGN TO MANEUVER VECTOR";
                solution.ThrottleAdvisory = "THROTTLE 0%";
                solution.Status = "GUIDANCE VALID";
                return solution;
            }

            if (IsFinite(solution.TimeToIgnitionSeconds) &&
                solution.TimeToIgnitionSeconds <= 0.0)
            {
                solution.Command = "IGNITE / HOLD MANEUVER VECTOR";
                solution.ThrottleAdvisory = "THROTTLE 100%";
                solution.Status = "IGNITION DUE";
                return solution;
            }

            if (IsFinite(solution.TimeToIgnitionSeconds) &&
                solution.TimeToIgnitionSeconds <= IgnitionStandbySeconds)
            {
                solution.Command = "IGNITION STANDBY";
                solution.ThrottleAdvisory = "THROTTLE 0% / READY 100%";
                solution.Status = "FINAL COUNT";
                return solution;
            }

            solution.Command = "HOLD MANEUVER VECTOR";
            solution.ThrottleAdvisory = "THROTTLE 0%";
            solution.Status = "GUIDANCE VALID";
            return solution;
        }

        private static void PopulateNodeVerification(
            GuidanceSolutionModel solution,
            ManeuverPlanModel plan,
            GuidanceNodeStateModel nodeState,
            DateTime receivedUtc)
        {
            if (nodeState == null || !nodeState.Available)
            {
                solution.NodeState = "NOT LOADED";
                solution.NodeDetail = "NO KSP NODE VERIFICATION TELEMETRY";
                return;
            }

            solution.NodeVerificationAvailable = true;
            solution.NodeState =
                string.IsNullOrWhiteSpace(nodeState.State)
                    ? "UNKNOWN"
                    : nodeState.State.Trim().ToUpperInvariant();
            solution.NodeDetail = nodeState.Detail ?? string.Empty;
            solution.NodeExists = nodeState.NodeExists;

            if (IsFinite(nodeState.ProgradeDeltaVMetersPerSecond) &&
                IsFinite(nodeState.NormalDeltaVMetersPerSecond) &&
                IsFinite(nodeState.RadialDeltaVMetersPerSecond))
            {
                solution.ActualNodeDeltaVMetersPerSecond =
                    Math.Sqrt(
                        nodeState.ProgradeDeltaVMetersPerSecond *
                        nodeState.ProgradeDeltaVMetersPerSecond +
                        nodeState.NormalDeltaVMetersPerSecond *
                        nodeState.NormalDeltaVMetersPerSecond +
                        nodeState.RadialDeltaVMetersPerSecond *
                        nodeState.RadialDeltaVMetersPerSecond);
            }

            bool planMatches =
                string.Equals(
                    nodeState.PlanId ?? string.Empty,
                    plan.PlanId ?? string.Empty,
                    StringComparison.Ordinal);

            bool fresh =
                nodeState.ReceivedUtc != DateTime.MinValue &&
                receivedUtc - nodeState.ReceivedUtc <=
                    TimeSpan.FromSeconds(NodeStateFreshnessSeconds);

            solution.NodeVerified =
                planMatches &&
                fresh &&
                nodeState.NodeExists &&
                string.Equals(
                    solution.NodeState,
                    "NODE VERIFIED",
                    StringComparison.Ordinal);

            solution.ExecutionAuthorized = solution.NodeVerified;
        }

        private static void PopulateAttitudeGuidance(
            GuidanceSolutionModel solution,
            OrbitModel orbit)
        {
            solution.Mode = "MANEUVER GUIDANCE";
            solution.AttitudeReference = "TRUE ORBITAL PROGRADE";

            if (orbit == null ||
                !orbit.Available ||
                orbit.VelocityVector == null ||
                !orbit.VelocityVector.Available)
            {
                solution.Evidence =
                    "Maneuver plan available; true orbital velocity vector unavailable.";
                return;
            }

            VelocityVectorTelemetryModel vector = orbit.VelocityVector;
            double magnitude = vector.OrbitalMagnitudeMetersPerSecond;

            if (!IsFinite(magnitude) || magnitude < 1.0)
            {
                solution.Evidence =
                    "ORBIT velocity vector magnitude invalid.";
                return;
            }

            double right =
                vector.OrbitalRightMetersPerSecond / magnitude;
            double nose =
                vector.OrbitalNoseMetersPerSecond / magnitude;
            double forward =
                vector.OrbitalReferenceForwardMetersPerSecond / magnitude;

            nose = Clamp(nose, -1.0, 1.0);

            solution.ManeuverVectorAvailable = true;
            solution.ManeuverRightComponent = right;
            solution.ManeuverNoseComponent = nose;
            solution.ManeuverReferenceForwardComponent = forward;

            solution.AlignmentErrorDegrees =
                RadiansToDegrees(Math.Acos(nose));

            solution.LateralErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        right,
                        Math.Max(0.000001, nose)));

            solution.VerticalErrorDegrees =
                RadiansToDegrees(
                    Math.Atan2(
                        forward,
                        Math.Sqrt(
                            nose * nose +
                            right * right)));

            solution.Available = true;
            solution.Evidence =
                "Maneuver vector from Engine plan; attitude reference from verified ORBIT velocity vector; execution gated by verified KSP node.";
        }

        private static void ApplyNodeInterlockCommand(
            GuidanceSolutionModel solution)
        {
            solution.ThrottleAdvisory = "THROTTLE 0%";
            string state = solution.NodeState ?? string.Empty;

            if (string.Equals(state, "CREW MODIFIED", StringComparison.Ordinal))
            {
                solution.Command = "REVIEW CREW-MODIFIED NODE";
                solution.Status = "GUIDANCE INHIBITED";
                return;
            }

            if (string.Equals(state, "NODE REMOVED", StringComparison.Ordinal))
            {
                solution.Command = "UPLOAD MANEUVER NODE";
                solution.Status = "NODE REMOVED";
                return;
            }

            if (string.Equals(state, "VESSEL NOT ACTIVE", StringComparison.Ordinal))
            {
                solution.Command = "SELECT MANEUVER VESSEL";
                solution.Status = "GUIDANCE INHIBITED";
                return;
            }

            if (string.Equals(state, "NODE LOADED", StringComparison.Ordinal) ||
                string.Equals(state, "AWAITING ACK", StringComparison.Ordinal))
            {
                solution.Command = "WAIT FOR NODE VERIFICATION";
                solution.Status = "VERIFYING NODE";
                return;
            }

            solution.Command = "UPLOAD / VERIFY MANEUVER NODE";
            solution.Status = "NODE NOT VERIFIED";
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
                " | NodeState=" + guidance.NodeState +
                " | NodeVerified=" + guidance.NodeVerified +
                " | ExecAuthorized=" + guidance.ExecutionAuthorized +
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
                " | KspNodeDV=" + Format(guidance.ActualNodeDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | Burn=" + Format(guidance.BurnDurationSeconds, "0.00") + "s" +
                " | Status=" + guidance.Status);
        }

        private static string Format(double value, string format)
        {
            return IsFinite(value)
                ? value.ToString(format)
                : "--";
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double RadiansToDegrees(double radians)
        {
            return radians * (180.0 / Math.PI);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                   !double.IsInfinity(value);
        }
    }
}
