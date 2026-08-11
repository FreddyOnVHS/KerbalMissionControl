using System;

namespace KMC.Engine.Guidance
{
    public sealed class GuidanceNodeStateModel
    {
        public GuidanceNodeStateModel()
        {
            PlanId = string.Empty;
            State = "UNAVAILABLE";
            Detail = string.Empty;
            NodeUniversalTimeSeconds = double.NaN;
            ProgradeDeltaVMetersPerSecond = double.NaN;
            NormalDeltaVMetersPerSecond = double.NaN;
            RadialDeltaVMetersPerSecond = double.NaN;
            ReceivedUtc = DateTime.MinValue;
        }

        public bool Available { get; set; }
        public string PlanId { get; set; }
        public string State { get; set; }
        public string Detail { get; set; }
        public bool NodeExists { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public DateTime ReceivedUtc { get; set; }

        internal GuidanceNodeStateModel Clone()
        {
            return new GuidanceNodeStateModel
            {
                Available = Available,
                PlanId = PlanId ?? string.Empty,
                State = State ?? string.Empty,
                Detail = Detail ?? string.Empty,
                NodeExists = NodeExists,
                NodeUniversalTimeSeconds = NodeUniversalTimeSeconds,
                ProgradeDeltaVMetersPerSecond =
                    ProgradeDeltaVMetersPerSecond,
                NormalDeltaVMetersPerSecond =
                    NormalDeltaVMetersPerSecond,
                RadialDeltaVMetersPerSecond =
                    RadialDeltaVMetersPerSecond,
                ReceivedUtc = ReceivedUtc
            };
        }
    }

    public static class GuidanceNodeStateStore
    {
        private static readonly object SyncRoot =
            new object();

        private static GuidanceNodeStateModel _latest =
            new GuidanceNodeStateModel();

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new GuidanceNodeStateModel();
            }
        }

        public static void Publish(
            GuidanceNodeStateModel state)
        {
            if (state == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _latest =
                    state.Clone();
            }
        }

        public static GuidanceNodeStateModel GetLatest()
        {
            lock (SyncRoot)
            {
                return
                    _latest != null
                        ? _latest.Clone()
                        : new GuidanceNodeStateModel();
            }
        }
    }

    public sealed class GuidanceSolutionModel
    {
        public GuidanceSolutionModel()
        {
            PlanId = string.Empty;
            Mode = "GUIDANCE WAITING";
            Command = "AWAIT MANEUVER PLAN";
            AttitudeReference = "NONE";
            ThrottleAdvisory = "THROTTLE 0%";
            Status = "UNAVAILABLE";
            Evidence = string.Empty;
            NodeState = "UNAVAILABLE";
            NodeDetail = string.Empty;

            AlignmentErrorDegrees = double.NaN;
            LateralErrorDegrees = double.NaN;
            VerticalErrorDegrees = double.NaN;
            TimeToNodeSeconds = double.NaN;
            TimeToIgnitionSeconds = double.NaN;
            PlannedDeltaVMetersPerSecond = double.NaN;
            ActualNodeDeltaVMetersPerSecond = double.NaN;
            BurnDurationSeconds = double.NaN;

            DeliveredDeltaVMetersPerSecond = double.NaN;
            RemainingDeltaVMetersPerSecond = double.NaN;
            BurnProgressPercent = double.NaN;
            LiveThrustKilonewtons = double.NaN;
            LiveAccelerationMetersPerSecondSquared = double.NaN;

            PostBurnResult = "NOT AVAILABLE";
            PlannedApoapsisMeters = double.NaN;
            PlannedPeriapsisMeters = double.NaN;
            AchievedApoapsisMeters = double.NaN;
            AchievedPeriapsisMeters = double.NaN;
            ApoapsisErrorMeters = double.NaN;
            PeriapsisErrorMeters = double.NaN;
            AchievedEccentricity = double.NaN;
            AchievedInclinationDegrees = double.NaN;
        }

        public bool Available { get; internal set; }
        public string PlanId { get; internal set; }
        public string Mode { get; internal set; }
        public string Command { get; internal set; }
        public string AttitudeReference { get; internal set; }
        public string ThrottleAdvisory { get; internal set; }
        public string Status { get; internal set; }
        public string Evidence { get; internal set; }

        public bool NodeVerificationAvailable { get; internal set; }
        public bool NodeVerified { get; internal set; }
        public bool ExecutionAuthorized { get; internal set; }
        public string NodeState { get; internal set; }
        public string NodeDetail { get; internal set; }
        public bool NodeExists { get; internal set; }
        public double ActualNodeDeltaVMetersPerSecond { get; internal set; }

        public bool ManeuverVectorAvailable { get; internal set; }
        public double ManeuverRightComponent { get; internal set; }
        public double ManeuverNoseComponent { get; internal set; }
        public double ManeuverReferenceForwardComponent { get; internal set; }

        public double AlignmentErrorDegrees { get; internal set; }
        public double LateralErrorDegrees { get; internal set; }
        public double VerticalErrorDegrees { get; internal set; }

        public double TimeToNodeSeconds { get; internal set; }
        public double TimeToIgnitionSeconds { get; internal set; }
        public double PlannedDeltaVMetersPerSecond { get; internal set; }
        public double BurnDurationSeconds { get; internal set; }

        public bool BurnActive { get; internal set; }
        public bool BurnComplete { get; internal set; }
        public bool ProducingThrust { get; internal set; }
        public double DeliveredDeltaVMetersPerSecond { get; internal set; }
        public double RemainingDeltaVMetersPerSecond { get; internal set; }
        public double BurnProgressPercent { get; internal set; }
        public double LiveThrustKilonewtons { get; internal set; }
        public double LiveAccelerationMetersPerSecondSquared { get; internal set; }

        public bool PostBurnVerificationAvailable { get; internal set; }
        public bool ReacquisitionReady { get; internal set; }
        public string PostBurnResult { get; internal set; }
        public double PlannedApoapsisMeters { get; internal set; }
        public double PlannedPeriapsisMeters { get; internal set; }
        public double AchievedApoapsisMeters { get; internal set; }
        public double AchievedPeriapsisMeters { get; internal set; }
        public double ApoapsisErrorMeters { get; internal set; }
        public double PeriapsisErrorMeters { get; internal set; }
        public double AchievedEccentricity { get; internal set; }
        public double AchievedInclinationDegrees { get; internal set; }

        internal GuidanceSolutionModel Clone()
        {
            return new GuidanceSolutionModel
            {
                Available = Available,
                PlanId = PlanId,
                Mode = Mode,
                Command = Command,
                AttitudeReference = AttitudeReference,
                ThrottleAdvisory = ThrottleAdvisory,
                Status = Status,
                Evidence = Evidence,

                NodeVerificationAvailable =
                    NodeVerificationAvailable,

                NodeVerified =
                    NodeVerified,

                ExecutionAuthorized =
                    ExecutionAuthorized,

                NodeState =
                    NodeState,

                NodeDetail =
                    NodeDetail,

                NodeExists =
                    NodeExists,

                ActualNodeDeltaVMetersPerSecond =
                    ActualNodeDeltaVMetersPerSecond,

                ManeuverVectorAvailable =
                    ManeuverVectorAvailable,

                ManeuverRightComponent =
                    ManeuverRightComponent,

                ManeuverNoseComponent =
                    ManeuverNoseComponent,

                ManeuverReferenceForwardComponent =
                    ManeuverReferenceForwardComponent,

                AlignmentErrorDegrees =
                    AlignmentErrorDegrees,

                LateralErrorDegrees =
                    LateralErrorDegrees,

                VerticalErrorDegrees =
                    VerticalErrorDegrees,

                TimeToNodeSeconds =
                    TimeToNodeSeconds,

                TimeToIgnitionSeconds =
                    TimeToIgnitionSeconds,

                PlannedDeltaVMetersPerSecond =
                    PlannedDeltaVMetersPerSecond,

                BurnDurationSeconds =
                    BurnDurationSeconds,

                BurnActive =
                    BurnActive,

                BurnComplete =
                    BurnComplete,

                ProducingThrust =
                    ProducingThrust,

                DeliveredDeltaVMetersPerSecond =
                    DeliveredDeltaVMetersPerSecond,

                RemainingDeltaVMetersPerSecond =
                    RemainingDeltaVMetersPerSecond,

                BurnProgressPercent =
                    BurnProgressPercent,

                LiveThrustKilonewtons =
                    LiveThrustKilonewtons,

                LiveAccelerationMetersPerSecondSquared =
                    LiveAccelerationMetersPerSecondSquared,

                PostBurnVerificationAvailable =
                    PostBurnVerificationAvailable,

                ReacquisitionReady =
                    ReacquisitionReady,

                PostBurnResult =
                    PostBurnResult,

                PlannedApoapsisMeters =
                    PlannedApoapsisMeters,

                PlannedPeriapsisMeters =
                    PlannedPeriapsisMeters,

                AchievedApoapsisMeters =
                    AchievedApoapsisMeters,

                AchievedPeriapsisMeters =
                    AchievedPeriapsisMeters,

                ApoapsisErrorMeters =
                    ApoapsisErrorMeters,

                PeriapsisErrorMeters =
                    PeriapsisErrorMeters,

                AchievedEccentricity =
                    AchievedEccentricity,

                AchievedInclinationDegrees =
                    AchievedInclinationDegrees
            };
        }
    }
}
