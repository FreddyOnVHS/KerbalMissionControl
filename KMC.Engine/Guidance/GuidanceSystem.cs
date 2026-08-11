using System;
using System.Diagnostics;
using KMC.Engine.Maneuver;
using KMC.Engine.Orbit;
using KMC.Shared;

namespace KMC.Engine.Guidance
{
    /// <summary>
    /// Build 12.2 advisory GNC with verified-node interlocks and
    /// live maneuver-burn execution tracking.
    ///
    /// The system never commands the spacecraft. Attitude and throttle
    /// values are crew advisories only.
    /// </summary>
    public sealed class GuidanceSystem
    {
        private const double AlignmentHoldToleranceDegrees = 2.0;
        private const double BurnAlignmentLimitDegrees = 5.0;
        private const double IgnitionStandbySeconds = 10.0;
        private const double BurnStartWindowSeconds = 2.5;
        private const double NodeStateFreshnessSeconds = 2.0;
        private const double MinimumThrustKilonewtons = 0.10;
        private const double CutoffDeltaVMetersPerSecond = 0.15;
        private const double FineThrottleDeltaVMetersPerSecond = 0.50;
        private const double TaperDeltaVMetersPerSecond = 2.00;
        private const double MaximumIntegrationStepSeconds = 0.50;
        private const double DiagnosticIntervalSeconds = 1.0;

        private GuidanceSolutionModel _latest =
            new GuidanceSolutionModel();

        private DateTime _lastDiagnosticUtc =
            DateTime.MinValue;

        private string _burnPlanId =
            string.Empty;

        private double _burnPlannedDeltaV =
            double.NaN;

        private double _burnPlannedDuration =
            double.NaN;

        private double _deliveredDeltaV;

        private DateTime _lastBurnUpdateUtc =
            DateTime.MinValue;

        private bool _burnActive;
        private bool _burnComplete;

        public void Update(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            GuidanceNodeStateModel nodeState =
                GuidanceNodeStateStore.GetLatest();

            if (_burnActive ||
                _burnComplete)
            {
                _latest =
                    BuildBurnExecutionSolution(
                        orbit,
                        nodeState,
                        flight,
                        receivedUtc);
            }
            else
            {
                _latest =
                    BuildPreBurnSolution(
                        orbit,
                        plan,
                        nodeState,
                        flight,
                        receivedUtc);

                if (ShouldStartBurn(
                        _latest,
                        flight))
                {
                    BeginBurn(
                        plan,
                        receivedUtc);

                    _latest =
                        BuildBurnExecutionSolution(
                            orbit,
                            nodeState,
                            flight,
                            receivedUtc);
                }
            }

            if (receivedUtc - _lastDiagnosticUtc >=
                TimeSpan.FromSeconds(
                    DiagnosticIntervalSeconds))
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

        private GuidanceSolutionModel BuildPreBurnSolution(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            GuidanceNodeStateModel nodeState,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            GuidanceSolutionModel solution =
                CreateBaseSolution(
                    orbit,
                    plan,
                    nodeState,
                    receivedUtc);

            PopulateLivePropulsion(
                solution,
                flight);

            if (plan == null ||
                !plan.Available)
            {
                solution.Status =
                    "MANEUVER PLAN UNAVAILABLE";

                return solution;
            }

            if (!solution.ExecutionAuthorized)
            {
                ApplyNodeInterlockCommand(
                    solution);

                return solution;
            }

            if (IsFinite(solution.TimeToNodeSeconds) &&
                solution.TimeToNodeSeconds < 0.0)
            {
                solution.Command =
                    "MANEUVER WINDOW PASSED";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "REPLAN REQUIRED";

                solution.ExecutionAuthorized =
                    false;

                return solution;
            }

            if (!solution.ManeuverVectorAvailable)
            {
                solution.Command =
                    "AWAIT TRUE ORBITAL VECTOR";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "VECTOR UNAVAILABLE";

                solution.ExecutionAuthorized =
                    false;

                return solution;
            }

            if (!IsAligned(
                    solution,
                    AlignmentHoldToleranceDegrees))
            {
                solution.Command =
                    "ALIGN TO MANEUVER VECTOR";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "GUIDANCE VALID";

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

            solution.Status =
                "GUIDANCE VALID";

            return solution;
        }

        private GuidanceSolutionModel BuildBurnExecutionSolution(
            OrbitModel orbit,
            GuidanceNodeStateModel nodeState,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            GuidanceSolutionModel solution =
                new GuidanceSolutionModel();

            solution.Available =
                true;

            solution.PlanId =
                _burnPlanId;

            solution.Mode =
                "MANEUVER EXECUTION";

            solution.AttitudeReference =
                "TRUE ORBITAL PROGRADE";

            solution.PlannedDeltaVMetersPerSecond =
                _burnPlannedDeltaV;

            solution.BurnDurationSeconds =
                _burnPlannedDuration;

            solution.TimeToNodeSeconds =
                0.0;

            solution.TimeToIgnitionSeconds =
                0.0;

            PopulateNodeVerificationForPlan(
                solution,
                _burnPlanId,
                nodeState,
                receivedUtc);

            PopulateAttitudeGuidance(
                solution,
                orbit);

            PopulateLivePropulsion(
                solution,
                flight);

            IntegrateDeliveredDeltaV(
                solution,
                flight,
                receivedUtc);

            solution.BurnActive =
                _burnActive;

            solution.BurnComplete =
                _burnComplete;

            solution.DeliveredDeltaVMetersPerSecond =
                _deliveredDeltaV;

            solution.RemainingDeltaVMetersPerSecond =
                IsFinite(_burnPlannedDeltaV)
                    ? Math.Max(
                        0.0,
                        _burnPlannedDeltaV -
                        _deliveredDeltaV)
                    : double.NaN;

            solution.BurnProgressPercent =
                IsFinite(_burnPlannedDeltaV) &&
                _burnPlannedDeltaV > 0.0
                    ? Clamp(
                        (_deliveredDeltaV /
                         _burnPlannedDeltaV) *
                        100.0,
                        0.0,
                        100.0)
                    : double.NaN;

            if (!solution.ExecutionAuthorized)
            {
                solution.Command =
                    "CUTOFF - MANEUVER INTERLOCK LOST";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "BURN INHIBITED";

                return solution;
            }

            if (!solution.ManeuverVectorAvailable)
            {
                solution.Command =
                    "CUTOFF - ATTITUDE VECTOR LOST";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "BURN INHIBITED";

                return solution;
            }

            if (IsFinite(
                    solution.AlignmentErrorDegrees) &&
                solution.AlignmentErrorDegrees >
                    BurnAlignmentLimitDegrees)
            {
                solution.Command =
                    "CORRECT ATTITUDE / REDUCE THROTTLE";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "ATTITUDE ERROR";

                return solution;
            }

            double remaining =
                solution.RemainingDeltaVMetersPerSecond;

            if (_burnComplete ||
                (IsFinite(remaining) &&
                 remaining <=
                    CutoffDeltaVMetersPerSecond))
            {
                _burnComplete =
                    true;

                _burnActive =
                    false;

                solution.BurnActive =
                    false;

                solution.BurnComplete =
                    true;

                solution.Command =
                    "CUTOFF / HOLD MANEUVER VECTOR";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    solution.ProducingThrust
                        ? "CUTOFF NOW"
                        : "MANEUVER COMPLETE";

                return solution;
            }

            if (!solution.ProducingThrust)
            {
                solution.Command =
                    "BURN PAUSED - RESTORE THRUST";

                solution.ThrottleAdvisory =
                    "THROTTLE 100%";

                solution.Status =
                    "BURN PAUSED";

                return solution;
            }

            if (IsFinite(remaining) &&
                remaining <=
                    FineThrottleDeltaVMetersPerSecond)
            {
                solution.Command =
                    "FINAL TRIM / PREP CUTOFF";

                solution.ThrottleAdvisory =
                    "THROTTLE 10%";

                solution.Status =
                    "FINAL TRIM";

                return solution;
            }

            if (IsFinite(remaining) &&
                remaining <=
                    TaperDeltaVMetersPerSecond)
            {
                solution.Command =
                    "THROTTLE DOWN / HOLD VECTOR";

                solution.ThrottleAdvisory =
                    "THROTTLE 50%";

                solution.Status =
                    "BURN TAPER";

                return solution;
            }

            solution.Command =
                "CONTINUE BURN / HOLD VECTOR";

            solution.ThrottleAdvisory =
                "THROTTLE 100%";

            solution.Status =
                "BURN IN PROGRESS";

            return solution;
        }

        private static GuidanceSolutionModel CreateBaseSolution(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            GuidanceNodeStateModel nodeState,
            DateTime receivedUtc)
        {
            GuidanceSolutionModel solution =
                new GuidanceSolutionModel();

            if (plan == null ||
                !plan.Available)
            {
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

            solution.RemainingDeltaVMetersPerSecond =
                plan.TotalDeltaVMetersPerSecond;

            solution.BurnDurationSeconds =
                plan.EstimatedBurnDurationSeconds;

            PopulateNodeVerificationForPlan(
                solution,
                plan.PlanId,
                nodeState,
                receivedUtc);

            PopulateAttitudeGuidance(
                solution,
                orbit);

            return solution;
        }

        private static void PopulateNodeVerificationForPlan(
            GuidanceSolutionModel solution,
            string planId,
            GuidanceNodeStateModel nodeState,
            DateTime receivedUtc)
        {
            if (nodeState == null ||
                !nodeState.Available)
            {
                solution.NodeState =
                    "NOT LOADED";

                solution.NodeDetail =
                    "NO KSP NODE VERIFICATION TELEMETRY";

                return;
            }

            solution.NodeVerificationAvailable =
                true;

            solution.NodeState =
                string.IsNullOrWhiteSpace(
                    nodeState.State)
                    ? "UNKNOWN"
                    : nodeState.State
                        .Trim()
                        .ToUpperInvariant();

            solution.NodeDetail =
                nodeState.Detail ?? string.Empty;

            solution.NodeExists =
                nodeState.NodeExists;

            if (IsFinite(
                    nodeState.ProgradeDeltaVMetersPerSecond) &&
                IsFinite(
                    nodeState.NormalDeltaVMetersPerSecond) &&
                IsFinite(
                    nodeState.RadialDeltaVMetersPerSecond))
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
                    planId ?? string.Empty,
                    StringComparison.Ordinal);

            bool fresh =
                nodeState.ReceivedUtc !=
                    DateTime.MinValue &&
                receivedUtc -
                    nodeState.ReceivedUtc <=
                TimeSpan.FromSeconds(
                    NodeStateFreshnessSeconds);

            solution.NodeVerified =
                planMatches &&
                fresh &&
                nodeState.NodeExists &&
                string.Equals(
                    solution.NodeState,
                    "NODE VERIFIED",
                    StringComparison.Ordinal);

            solution.ExecutionAuthorized =
                solution.NodeVerified;
        }

        private static void PopulateAttitudeGuidance(
            GuidanceSolutionModel solution,
            OrbitModel orbit)
        {
            if (solution.Mode ==
                "GUIDANCE WAITING")
            {
                solution.Mode =
                    "MANEUVER GUIDANCE";
            }

            solution.AttitudeReference =
                "TRUE ORBITAL PROGRADE";

            if (orbit == null ||
                !orbit.Available ||
                orbit.VelocityVector == null ||
                !orbit.VelocityVector.Available)
            {
                solution.Evidence =
                    "True orbital velocity vector unavailable.";

                return;
            }

            VelocityVectorTelemetryModel vector =
                orbit.VelocityVector;

            double magnitude =
                vector.OrbitalMagnitudeMetersPerSecond;

            if (!IsFinite(magnitude) ||
                magnitude < 1.0)
            {
                solution.Evidence =
                    "ORBIT velocity vector magnitude invalid.";

                return;
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
                    Math.Acos(
                        nose));

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

            solution.Evidence =
                "True orbital prograde attitude reference; maneuver execution remains crew advisory.";
        }

        private static void PopulateLivePropulsion(
            GuidanceSolutionModel solution,
            TelemetryPacket flight)
        {
            if (flight == null)
            {
                return;
            }

            solution.LiveThrustKilonewtons =
                flight.CurrentThrust;

            solution.ProducingThrust =
                IsFinite(
                    flight.CurrentThrust) &&
                flight.CurrentThrust >=
                    MinimumThrustKilonewtons;

            if (IsFinite(
                    flight.CurrentThrust) &&
                IsFinite(
                    flight.VesselMass) &&
                flight.VesselMass > 0.0)
            {
                /*
                 * KSP reports thrust in kN and mass in metric tonnes.
                 * kN / tonne is numerically equal to m/s^2.
                 */
                solution.LiveAccelerationMetersPerSecondSquared =
                    flight.CurrentThrust /
                    flight.VesselMass;
            }
        }

        private void IntegrateDeliveredDeltaV(
            GuidanceSolutionModel solution,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            if (!_burnActive ||
                flight == null)
            {
                _lastBurnUpdateUtc =
                    receivedUtc;

                return;
            }

            if (_lastBurnUpdateUtc ==
                DateTime.MinValue)
            {
                _lastBurnUpdateUtc =
                    receivedUtc;

                return;
            }

            double dt =
                (receivedUtc -
                 _lastBurnUpdateUtc)
                .TotalSeconds;

            _lastBurnUpdateUtc =
                receivedUtc;

            if (!IsFinite(dt) ||
                dt <= 0.0)
            {
                return;
            }

            dt =
                Math.Min(
                    dt,
                    MaximumIntegrationStepSeconds);

            if (!IsFinite(
                    flight.CurrentThrust) ||
                flight.CurrentThrust <
                    MinimumThrustKilonewtons ||
                !IsFinite(
                    flight.VesselMass) ||
                flight.VesselMass <= 0.0)
            {
                return;
            }

            double acceleration =
                flight.CurrentThrust /
                flight.VesselMass;

            double alignmentFactor =
                1.0;

            if (IsFinite(
                    solution.AlignmentErrorDegrees))
            {
                alignmentFactor =
                    Math.Max(
                        0.0,
                        Math.Cos(
                            DegreesToRadians(
                                solution.AlignmentErrorDegrees)));
            }

            _deliveredDeltaV +=
                acceleration *
                alignmentFactor *
                dt;

            if (IsFinite(_burnPlannedDeltaV))
            {
                _deliveredDeltaV =
                    Math.Min(
                        _deliveredDeltaV,
                        _burnPlannedDeltaV);
            }
        }

        private static bool ShouldStartBurn(
            GuidanceSolutionModel solution,
            TelemetryPacket flight)
        {
            if (solution == null ||
                flight == null ||
                !solution.ExecutionAuthorized ||
                !solution.ManeuverVectorAvailable ||
                !IsFinite(
                    solution.TimeToIgnitionSeconds) ||
                solution.TimeToIgnitionSeconds >
                    BurnStartWindowSeconds ||
                !IsAligned(
                    solution,
                    BurnAlignmentLimitDegrees))
            {
                return false;
            }

            return
                IsFinite(
                    flight.CurrentThrust) &&
                flight.CurrentThrust >=
                    MinimumThrustKilonewtons;
        }

        private void BeginBurn(
            ManeuverPlanModel plan,
            DateTime receivedUtc)
        {
            if (plan == null)
            {
                return;
            }

            _burnPlanId =
                plan.PlanId ?? string.Empty;

            _burnPlannedDeltaV =
                plan.TotalDeltaVMetersPerSecond;

            _burnPlannedDuration =
                plan.EstimatedBurnDurationSeconds;

            _deliveredDeltaV =
                0.0;

            _lastBurnUpdateUtc =
                receivedUtc;

            _burnActive =
                true;

            _burnComplete =
                false;
        }

        private static bool IsAligned(
            GuidanceSolutionModel solution,
            double toleranceDegrees)
        {
            return
                solution != null &&
                IsFinite(
                    solution.AlignmentErrorDegrees) &&
                solution.AlignmentErrorDegrees <=
                    toleranceDegrees;
        }

        private static void ApplyNodeInterlockCommand(
            GuidanceSolutionModel solution)
        {
            solution.ThrottleAdvisory =
                "THROTTLE 0%";

            string state =
                solution.NodeState ?? string.Empty;

            if (string.Equals(
                    state,
                    "CREW MODIFIED",
                    StringComparison.Ordinal))
            {
                solution.Command =
                    "REVIEW CREW-MODIFIED NODE";

                solution.Status =
                    "GUIDANCE INHIBITED";

                return;
            }

            if (string.Equals(
                    state,
                    "NODE REMOVED",
                    StringComparison.Ordinal))
            {
                solution.Command =
                    "UPLOAD MANEUVER NODE";

                solution.Status =
                    "NODE REMOVED";

                return;
            }

            if (string.Equals(
                    state,
                    "VESSEL NOT ACTIVE",
                    StringComparison.Ordinal))
            {
                solution.Command =
                    "SELECT MANEUVER VESSEL";

                solution.Status =
                    "GUIDANCE INHIBITED";

                return;
            }

            if (string.Equals(
                    state,
                    "NODE LOADED",
                    StringComparison.Ordinal) ||
                string.Equals(
                    state,
                    "AWAITING ACK",
                    StringComparison.Ordinal))
            {
                solution.Command =
                    "WAIT FOR NODE VERIFICATION";

                solution.Status =
                    "VERIFYING NODE";

                return;
            }

            solution.Command =
                "UPLOAD / VERIFY MANEUVER NODE";

            solution.Status =
                "NODE NOT VERIFIED";
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
                " | TNode=" + Format(guidance.TimeToNodeSeconds, "0.0") + "s" +
                " | TIgn=" + Format(guidance.TimeToIgnitionSeconds, "0.0") + "s" +
                " | PlanDV=" + Format(guidance.PlannedDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | DeliveredDV=" + Format(guidance.DeliveredDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | RemDV=" + Format(guidance.RemainingDeltaVMetersPerSecond, "0.00") + "m/s" +
                " | BurnPct=" + Format(guidance.BurnProgressPercent, "0.0") + "%" +
                " | Thrust=" + Format(guidance.LiveThrustKilonewtons, "0.00") + "kN" +
                " | Accel=" + Format(guidance.LiveAccelerationMetersPerSecondSquared, "0.00") + "m/s2" +
                " | BurnActive=" + guidance.BurnActive +
                " | BurnComplete=" + guidance.BurnComplete +
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

        private static double DegreesToRadians(
            double degrees)
        {
            return
                degrees *
                (Math.PI / 180.0);
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
