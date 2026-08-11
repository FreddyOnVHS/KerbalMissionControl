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
        private const double NodeUtToleranceSeconds = 0.25;
        private const double NodeDeltaVToleranceMetersPerSecond = 0.05;
        private const double OrbitWrapToleranceSeconds = 2.0;
        private const double MinimumThrustKilonewtons = 0.10;
        private const double CutoffDeltaVMetersPerSecond = 0.15;
        private const double FineThrottleDeltaVMetersPerSecond = 0.50;
        private const double TaperDeltaVMetersPerSecond = 2.00;

        /*
         * Build 13.2.2 predictive cutoff.
         *
         * First burn uses a conservative crew/engine shutdown-response
         * allowance. After a real cutoff, KMC learns the observed time from
         * CUTOFF NOW to thrust zero and reuses that response on later burns.
         */
        private const double DefaultCutoffResponseSeconds = 0.60;
        private const double MinimumCutoffResponseSeconds = 0.20;
        private const double MaximumCutoffResponseSeconds = 1.50;
        private const double PredictiveCutoffMarginMetersPerSecond = 0.15;
        private const double MaximumIntegrationStepSeconds = 0.50;
        private const double PostBurnSettleSeconds = 1.0;
        private const double ReacquisitionDelaySeconds = 1.0;
        private const double NominalOrbitErrorFraction = 0.01;
        private const double NominalOrbitErrorFloorMeters = 2000.0;
        private const double AcceptableOrbitErrorFraction = 0.03;
        private const double AcceptableOrbitErrorFloorMeters = 5000.0;
        private const double NominalEccentricityLimit = 0.010;
        private const double AcceptableEccentricityLimit = 0.030;
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
        private bool _burnRetrograde;
        private bool _burnRequiresOrbitTargetVerification;

        private bool _cutoffCommanded;
        private DateTime _cutoffCommandedUtc =
            DateTime.MinValue;

        private double _observedCutoffResponseSeconds =
            double.NaN;

        private double _burnPredictedApoapsisMeters =
            double.NaN;

        private double _burnPredictedPeriapsisMeters =
            double.NaN;

        private double _burnPredictedInclinationDegrees =
            double.NaN;

        private double _burnPredictedEccentricity =
            double.NaN;

        private DateTime _postBurnZeroThrustSinceUtc =
            DateTime.MinValue;

        private DateTime _completedNodeRemovedSinceUtc =
            DateTime.MinValue;

        public void Update(
            OrbitModel orbit,
            ManeuverPlanModel plan,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            GuidanceNodeStateModel nodeState =
                GuidanceNodeStateStore.GetLatest();

            if (_burnComplete &&
                ShouldReacquireNextPlan(
                    plan,
                    nodeState,
                    flight,
                    receivedUtc))
            {
                ResetCompletedBurn();
            }

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

            bool maneuverWindowMissed =
                plan != null &&
                string.Equals(
                    plan.Status,
                    "MANEUVER WINDOW MISSED",
                    StringComparison.OrdinalIgnoreCase);

            /*
             * Build 13.2.1:
             * A missed reviewed epoch outranks stale/wrapped KSP node state.
             * Crew must explicitly COMPUTE a new plan before execution can
             * become authorized again.
             */
            if (maneuverWindowMissed)
            {
                solution.Available =
                    true;

                solution.Command =
                    "MANEUVER WINDOW MISSED / REPLAN";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "REPLAN REQUIRED";

                solution.ExecutionAuthorized =
                    false;

                return solution;
            }

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
                _burnRetrograde
                    ? "TRUE ORBITAL RETROGRADE"
                    : "TRUE ORBITAL PROGRADE";

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
                orbit,
                _burnRetrograde);

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

            double remaining =
                solution.RemainingDeltaVMetersPerSecond;

            /*
             * Build 13.2.2:
             *
             * Do NOT declare the maneuver complete merely because integrated
             * remaining Delta-V reached the old 0.15 m/s threshold while the
             * engine is still producing thrust.
             *
             * Instead:
             * 1. command cutoff early using live acceleration and a learned
             *    shutdown-response allowance;
             * 2. keep integrating delivered Delta-V while thrust decays;
             * 3. declare completion only after thrust actually reaches zero.
             */
            double predictiveCutoffDeltaV =
                CalculatePredictiveCutoffDeltaV(
                    solution);

            bool cutoffDue =
                IsFinite(remaining) &&
                remaining <=
                    predictiveCutoffDeltaV;

            if (!_cutoffCommanded &&
                cutoffDue)
            {
                _cutoffCommanded =
                    true;

                _cutoffCommandedUtc =
                    receivedUtc;
            }

            /*
             * Build 13.2.3:
             * Latch the transition to BurnComplete only once.
             *
             * Without the !_burnComplete guard, every zero-thrust update
             * re-entered this block and reset _postBurnZeroThrustSinceUtc.
             * That prevented the existing post-burn settle timer from ever
             * reaching PostBurnSettleSeconds.
             */
            if (!_burnComplete &&
                _cutoffCommanded &&
                !solution.ProducingThrust)
            {
                ObserveCutoffResponse(
                    receivedUtc);

                _burnComplete =
                    true;

                _burnActive =
                    false;

                _postBurnZeroThrustSinceUtc =
                    DateTime.MinValue;
            }

            solution.BurnActive =
                _burnActive;

            solution.BurnComplete =
                _burnComplete;

            if (_burnComplete)
            {
                ApplyPostBurnVerification(
                    solution,
                    orbit,
                    nodeState,
                    flight,
                    receivedUtc);

                return solution;
            }

            if (_cutoffCommanded)
            {
                solution.Command =
                    solution.ProducingThrust
                        ? "CUTOFF NOW"
                        : "HOLD CUTOFF / VERIFY THRUST ZERO";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    solution.ProducingThrust
                        ? "PREDICTIVE CUTOFF"
                        : "CUTOFF CONFIRMATION";

                return solution;
            }

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

            if (plan == null)
            {
                return solution;
            }

            bool maneuverWindowMissed =
                string.Equals(
                    plan.Status,
                    "MANEUVER WINDOW MISSED",
                    StringComparison.OrdinalIgnoreCase);

            if (!plan.Available &&
                !maneuverWindowMissed)
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

            ApplyTimeWarpNodeClassification(
                solution,
                plan,
                nodeState);

            bool retrograde =
                IsFinite(
                    plan.ProgradeDeltaVMetersPerSecond) &&
                plan.ProgradeDeltaVMetersPerSecond < 0.0;

            PopulateAttitudeGuidance(
                solution,
                orbit,
                retrograde);

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
            OrbitModel orbit,
            bool retrograde)
        {
            if (solution.Mode ==
                "GUIDANCE WAITING")
            {
                solution.Mode =
                    "MANEUVER GUIDANCE";
            }

            solution.CommandedRetrograde =
                retrograde;

            solution.AttitudeReference =
                retrograde
                    ? "TRUE ORBITAL RETROGRADE"
                    : "TRUE ORBITAL PROGRADE";

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

            /*
             * Build 13.2:
             * Prograde uses the verified orbital velocity unit vector.
             * Retrograde uses its exact inverse. No new spacecraft telemetry
             * or coordinate convention is introduced.
             */
            double direction =
                retrograde
                    ? -1.0
                    : 1.0;

            double right =
                direction *
                vector.OrbitalRightMetersPerSecond /
                magnitude;

            double nose =
                direction *
                vector.OrbitalNoseMetersPerSecond /
                magnitude;

            double forward =
                direction *
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
                retrograde
                    ? "True orbital retrograde attitude reference from inverted verified orbital velocity vector; maneuver execution remains crew advisory."
                    : "True orbital prograde attitude reference from verified orbital velocity vector; maneuver execution remains crew advisory.";
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

        private double CalculatePredictiveCutoffDeltaV(
            GuidanceSolutionModel solution)
        {
            double responseSeconds =
                IsFinite(
                    _observedCutoffResponseSeconds)
                    ? Clamp(
                        _observedCutoffResponseSeconds,
                        MinimumCutoffResponseSeconds,
                        MaximumCutoffResponseSeconds)
                    : DefaultCutoffResponseSeconds;

            double acceleration =
                solution != null &&
                IsFinite(
                    solution.LiveAccelerationMetersPerSecondSquared)
                    ? Math.Max(
                        0.0,
                        solution.LiveAccelerationMetersPerSecondSquared)
                    : 0.0;

            double predictedShutdownDeltaV =
                acceleration *
                responseSeconds;

            return
                Math.Max(
                    CutoffDeltaVMetersPerSecond,
                    predictedShutdownDeltaV +
                    PredictiveCutoffMarginMetersPerSecond);
        }

        private void ObserveCutoffResponse(
            DateTime receivedUtc)
        {
            if (_cutoffCommandedUtc ==
                DateTime.MinValue)
            {
                return;
            }

            double observed =
                (receivedUtc -
                 _cutoffCommandedUtc)
                .TotalSeconds;

            if (!IsFinite(observed) ||
                observed <= 0.0)
            {
                return;
            }

            observed =
                Clamp(
                    observed,
                    MinimumCutoffResponseSeconds,
                    MaximumCutoffResponseSeconds);

            if (!IsFinite(
                    _observedCutoffResponseSeconds))
            {
                _observedCutoffResponseSeconds =
                    observed;
            }
            else
            {
                /*
                 * Gentle exponential learning so one unusual crew reaction
                 * does not completely redefine the next maneuver.
                 */
                _observedCutoffResponseSeconds =
                    (_observedCutoffResponseSeconds *
                     0.70) +
                    (observed *
                     0.30);
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

            _burnRetrograde =
                IsFinite(
                    plan.ProgradeDeltaVMetersPerSecond) &&
                plan.ProgradeDeltaVMetersPerSecond < 0.0;

            _burnRequiresOrbitTargetVerification =
                plan.OrbitTargetVerificationRequired;

            _cutoffCommanded =
                false;

            _cutoffCommandedUtc =
                DateTime.MinValue;

            _burnPredictedApoapsisMeters =
                plan.PredictedApoapsisMeters;

            _burnPredictedPeriapsisMeters =
                plan.PredictedPeriapsisMeters;

            _burnPredictedInclinationDegrees =
                plan.PredictedInclinationDegrees;

            _burnPredictedEccentricity =
                plan.PredictedEccentricity;

            _postBurnZeroThrustSinceUtc =
                DateTime.MinValue;

            _completedNodeRemovedSinceUtc =
                DateTime.MinValue;

            _deliveredDeltaV =
                0.0;

            _lastBurnUpdateUtc =
                receivedUtc;

            _burnActive =
                true;

            _burnComplete =
                false;
        }

        private void ApplyPostBurnVerification(
            GuidanceSolutionModel solution,
            OrbitModel orbit,
            GuidanceNodeStateModel nodeState,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            solution.BurnActive =
                false;

            solution.BurnComplete =
                true;

            solution.Mode =
                "POST-BURN VERIFICATION";

            solution.ThrottleAdvisory =
                "THROTTLE 0%";

            solution.PlannedApoapsisMeters =
                _burnPredictedApoapsisMeters;

            solution.PlannedPeriapsisMeters =
                _burnPredictedPeriapsisMeters;

            if (solution.ProducingThrust)
            {
                _postBurnZeroThrustSinceUtc =
                    DateTime.MinValue;

                solution.Command =
                    "CUTOFF NOW";

                solution.Status =
                    "CUTOFF NOW";

                solution.PostBurnResult =
                    "WAITING FOR THRUST ZERO";

                return;
            }

            if (_postBurnZeroThrustSinceUtc ==
                DateTime.MinValue)
            {
                _postBurnZeroThrustSinceUtc =
                    receivedUtc;
            }

            if (receivedUtc -
                    _postBurnZeroThrustSinceUtc <
                TimeSpan.FromSeconds(
                    PostBurnSettleSeconds))
            {
                solution.Command =
                    "HOLD ATTITUDE / VERIFY ORBIT";

                solution.Status =
                    "POST-BURN CHECK";

                solution.PostBurnResult =
                    "ORBIT SETTLING";

                return;
            }

            bool completedNodeRemoved =
                nodeState != null &&
                nodeState.Available &&
                string.Equals(
                    nodeState.PlanId ?? string.Empty,
                    _burnPlanId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    nodeState.State ?? string.Empty,
                    "NODE REMOVED",
                    StringComparison.Ordinal);

            if (!_burnRequiresOrbitTargetVerification)
            {
                /*
                 * Build 13.3 manual maneuvers have no claimed target orbit.
                 * The verified result is successful delivery of the reviewed
                 * signed Delta-V followed by actual thrust zero.
                 */
                solution.PostBurnVerificationAvailable =
                    true;

                solution.ReacquisitionReady =
                    completedNodeRemoved;

                solution.PostBurnResult =
                    "DV COMPLETE";

                solution.Status =
                    "MANEUVER COMPLETE";

                solution.Command =
                    completedNodeRemoved
                        ? "REACQUIRING NEXT MANEUVER"
                        : "MANUAL MANEUVER COMPLETE / REMOVE NODE";

                return;
            }

            if (orbit == null ||
                !orbit.Available ||
                orbit.Current == null ||
                !orbit.Current.Available)
            {
                solution.Command =
                    "HOLD ATTITUDE / AWAIT ORBIT DATA";

                solution.Status =
                    "POST-BURN CHECK";

                solution.PostBurnResult =
                    "ORBIT DATA UNAVAILABLE";

                return;
            }

            OrbitTelemetryState current =
                orbit.Current;

            solution.PostBurnVerificationAvailable =
                true;

            solution.AchievedApoapsisMeters =
                current.ApoapsisMeters;

            solution.AchievedPeriapsisMeters =
                current.PeriapsisMeters;

            solution.AchievedEccentricity =
                current.Eccentricity;

            solution.AchievedInclinationDegrees =
                current.InclinationDegrees;

            solution.ApoapsisErrorMeters =
                IsFinite(_burnPredictedApoapsisMeters)
                    ? current.ApoapsisMeters -
                      _burnPredictedApoapsisMeters
                    : double.NaN;

            solution.PeriapsisErrorMeters =
                IsFinite(_burnPredictedPeriapsisMeters)
                    ? current.PeriapsisMeters -
                      _burnPredictedPeriapsisMeters
                    : double.NaN;

            double nominalApTolerance =
                ResolveOrbitTolerance(
                    _burnPredictedApoapsisMeters,
                    NominalOrbitErrorFraction,
                    NominalOrbitErrorFloorMeters);

            double nominalPeTolerance =
                ResolveOrbitTolerance(
                    _burnPredictedPeriapsisMeters,
                    NominalOrbitErrorFraction,
                    NominalOrbitErrorFloorMeters);

            double acceptableApTolerance =
                ResolveOrbitTolerance(
                    _burnPredictedApoapsisMeters,
                    AcceptableOrbitErrorFraction,
                    AcceptableOrbitErrorFloorMeters);

            double acceptablePeTolerance =
                ResolveOrbitTolerance(
                    _burnPredictedPeriapsisMeters,
                    AcceptableOrbitErrorFraction,
                    AcceptableOrbitErrorFloorMeters);

            double eccentricityError =
                IsFinite(
                    solution.AchievedEccentricity) &&
                IsFinite(
                    _burnPredictedEccentricity)
                    ? solution.AchievedEccentricity -
                      _burnPredictedEccentricity
                    : double.NaN;

            /*
             * Build 13.2:
             * General apsis maneuvers may intentionally target an elliptical
             * orbit. Verify achieved eccentricity against the planned
             * eccentricity instead of assuming every successful maneuver is
             * near-circular.
             */
            bool nominal =
                IsWithinTolerance(
                    solution.ApoapsisErrorMeters,
                    nominalApTolerance) &&
                IsWithinTolerance(
                    solution.PeriapsisErrorMeters,
                    nominalPeTolerance) &&
                IsWithinTolerance(
                    eccentricityError,
                    NominalEccentricityLimit);

            bool acceptable =
                IsWithinTolerance(
                    solution.ApoapsisErrorMeters,
                    acceptableApTolerance) &&
                IsWithinTolerance(
                    solution.PeriapsisErrorMeters,
                    acceptablePeTolerance) &&
                IsWithinTolerance(
                    eccentricityError,
                    AcceptableEccentricityLimit);

            solution.ReacquisitionReady =
                completedNodeRemoved;

            if (nominal)
            {
                solution.PostBurnResult =
                    "NOMINAL";

                solution.Status =
                    "MANEUVER VERIFIED";

                solution.Command =
                    completedNodeRemoved
                        ? "REACQUIRING NEXT MANEUVER"
                        : "MANEUVER VERIFIED / REMOVE NODE";
            }
            else if (acceptable)
            {
                solution.PostBurnResult =
                    "ACCEPTABLE";

                solution.Status =
                    "ORBIT ACCEPTABLE";

                solution.Command =
                    completedNodeRemoved
                        ? "REACQUIRING NEXT MANEUVER"
                        : "ORBIT ACCEPTABLE / REMOVE NODE";
            }
            else
            {
                solution.PostBurnResult =
                    "OFF-NOMINAL";

                solution.Status =
                    "POST-BURN OFF-NOMINAL";

                solution.Command =
                    completedNodeRemoved
                        ? "REVIEW ORBIT / NEXT PLAN"
                        : "REVIEW ORBIT / REMOVE NODE";
            }
        }

        private bool ShouldReacquireNextPlan(
            ManeuverPlanModel currentPlan,
            GuidanceNodeStateModel nodeState,
            TelemetryPacket flight,
            DateTime receivedUtc)
        {
            if (!_burnComplete ||
                currentPlan == null ||
                !currentPlan.Available ||
                string.IsNullOrWhiteSpace(
                    currentPlan.PlanId) ||
                string.Equals(
                    currentPlan.PlanId,
                    _burnPlanId,
                    StringComparison.Ordinal) ||
                flight == null ||
                (IsFinite(flight.CurrentThrust) &&
                 flight.CurrentThrust >=
                    MinimumThrustKilonewtons) ||
                nodeState == null ||
                !nodeState.Available ||
                !string.Equals(
                    nodeState.PlanId ?? string.Empty,
                    _burnPlanId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    nodeState.State ?? string.Empty,
                    "NODE REMOVED",
                    StringComparison.Ordinal))
            {
                _completedNodeRemovedSinceUtc =
                    DateTime.MinValue;

                return false;
            }

            if (_completedNodeRemovedSinceUtc ==
                DateTime.MinValue)
            {
                _completedNodeRemovedSinceUtc =
                    receivedUtc;

                return false;
            }

            return
                receivedUtc -
                    _completedNodeRemovedSinceUtc >=
                TimeSpan.FromSeconds(
                    ReacquisitionDelaySeconds);
        }

        private void ResetCompletedBurn()
        {
            _burnPlanId =
                string.Empty;

            _burnPlannedDeltaV =
                double.NaN;

            _burnPlannedDuration =
                double.NaN;

            _burnRetrograde =
                false;

            _burnRequiresOrbitTargetVerification =
                false;

            _cutoffCommanded =
                false;

            _cutoffCommandedUtc =
                DateTime.MinValue;

            _burnPredictedApoapsisMeters =
                double.NaN;

            _burnPredictedPeriapsisMeters =
                double.NaN;

            _burnPredictedInclinationDegrees =
                double.NaN;

            _burnPredictedEccentricity =
                double.NaN;

            _deliveredDeltaV =
                0.0;

            _lastBurnUpdateUtc =
                DateTime.MinValue;

            _postBurnZeroThrustSinceUtc =
                DateTime.MinValue;

            _completedNodeRemovedSinceUtc =
                DateTime.MinValue;

            _burnActive =
                false;

            _burnComplete =
                false;
        }

        private static double ResolveOrbitTolerance(
            double plannedMeters,
            double fraction,
            double floorMeters)
        {
            if (!IsFinite(plannedMeters))
            {
                return floorMeters;
            }

            return
                Math.Max(
                    floorMeters,
                    Math.Abs(plannedMeters) *
                    fraction);
        }

        private static bool IsWithinTolerance(
            double error,
            double tolerance)
        {
            return
                IsFinite(error) &&
                IsFinite(tolerance) &&
                Math.Abs(error) <=
                    tolerance;
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

        private static void ApplyTimeWarpNodeClassification(
            GuidanceSolutionModel solution,
            ManeuverPlanModel plan,
            GuidanceNodeStateModel nodeState)
        {
            if (solution == null ||
                plan == null ||
                nodeState == null ||
                !nodeState.Available ||
                !nodeState.NodeExists ||
                !string.Equals(
                    nodeState.PlanId ?? string.Empty,
                    plan.PlanId ?? string.Empty,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    nodeState.State ?? string.Empty,
                    "CREW MODIFIED",
                    StringComparison.Ordinal) ||
                !plan.NodeUniversalTimeAvailable ||
                !IsFinite(
                    plan.NodeUniversalTimeSeconds) ||
                !IsFinite(
                    nodeState.NodeUniversalTimeSeconds) ||
                !IsFinite(
                    plan.PredictedPeriodSeconds) ||
                plan.PredictedPeriodSeconds <= 0.0)
            {
                return;
            }

            bool deltaVMatches =
                IsWithin(
                    nodeState.ProgradeDeltaVMetersPerSecond,
                    plan.ProgradeDeltaVMetersPerSecond,
                    NodeDeltaVToleranceMetersPerSecond) &&
                IsWithin(
                    nodeState.NormalDeltaVMetersPerSecond,
                    plan.NormalDeltaVMetersPerSecond,
                    NodeDeltaVToleranceMetersPerSecond) &&
                IsWithin(
                    nodeState.RadialDeltaVMetersPerSecond,
                    plan.RadialDeltaVMetersPerSecond,
                    NodeDeltaVToleranceMetersPerSecond);

            if (!deltaVMatches)
            {
                return;
            }

            double utShift =
                nodeState.NodeUniversalTimeSeconds -
                plan.NodeUniversalTimeSeconds;

            if (Math.Abs(utShift) <=
                NodeUtToleranceSeconds)
            {
                return;
            }

            double wraps =
                utShift /
                plan.PredictedPeriodSeconds;

            double nearestWrap =
                Math.Round(wraps);

            if (Math.Abs(nearestWrap) < 1.0)
            {
                return;
            }

            double expectedShift =
                nearestWrap *
                plan.PredictedPeriodSeconds;

            if (Math.Abs(
                    utShift -
                    expectedShift) >
                OrbitWrapToleranceSeconds)
            {
                return;
            }

            /*
             * KSP can advance a maneuver node to the corresponding orbital
             * point on a later revolution when time warp passes the intended
             * node epoch. The DV vector is unchanged, but this is NOT the
             * reviewed maneuver anymore. Do not mislabel it as a crew edit,
             * and do not automatically authorize the next-orbit burn.
             */
            solution.NodeVerified =
                false;

            solution.ExecutionAuthorized =
                false;

            solution.NodeState =
                "MANEUVER WINDOW MISSED";

            solution.NodeDetail =
                "NODE UT SHIFTED BY " +
                nearestWrap.ToString("0") +
                " ORBIT(S) DURING TIME WARP; REPLAN REQUIRED";
        }

        private static bool IsWithin(
            double actual,
            double planned,
            double tolerance)
        {
            return
                IsFinite(actual) &&
                IsFinite(planned) &&
                Math.Abs(
                    actual -
                    planned) <=
                tolerance;
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
                    "MANEUVER WINDOW MISSED",
                    StringComparison.Ordinal))
            {
                solution.Command =
                    "MANEUVER WINDOW MISSED / REPLAN";

                solution.ThrottleAdvisory =
                    "THROTTLE 0%";

                solution.Status =
                    "REPLAN REQUIRED";

                return;
            }

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
                " | PostBurn=" + guidance.PostBurnResult +
                " | AchAp=" + Format(guidance.AchievedApoapsisMeters, "0") + "m" +
                " | AchPe=" + Format(guidance.AchievedPeriapsisMeters, "0") + "m" +
                " | ApErr=" + Format(guidance.ApoapsisErrorMeters, "0") + "m" +
                " | PeErr=" + Format(guidance.PeriapsisErrorMeters, "0") + "m" +
                " | AchEcc=" + Format(guidance.AchievedEccentricity, "0.000000") +
                " | ReacqReady=" + guidance.ReacquisitionReady +
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
