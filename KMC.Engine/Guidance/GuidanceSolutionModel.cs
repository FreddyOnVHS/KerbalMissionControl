using System;

namespace KMC.Engine.Guidance
{
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
            AlignmentErrorDegrees = double.NaN;
            LateralErrorDegrees = double.NaN;
            VerticalErrorDegrees = double.NaN;
            TimeToNodeSeconds = double.NaN;
            TimeToIgnitionSeconds = double.NaN;
            PlannedDeltaVMetersPerSecond = double.NaN;
            BurnDurationSeconds = double.NaN;
        }

        public bool Available { get; internal set; }
        public string PlanId { get; internal set; }
        public string Mode { get; internal set; }
        public string Command { get; internal set; }
        public string AttitudeReference { get; internal set; }
        public string ThrottleAdvisory { get; internal set; }
        public string Status { get; internal set; }
        public string Evidence { get; internal set; }

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
                ManeuverVectorAvailable = ManeuverVectorAvailable,
                ManeuverRightComponent = ManeuverRightComponent,
                ManeuverNoseComponent = ManeuverNoseComponent,
                ManeuverReferenceForwardComponent = ManeuverReferenceForwardComponent,
                AlignmentErrorDegrees = AlignmentErrorDegrees,
                LateralErrorDegrees = LateralErrorDegrees,
                VerticalErrorDegrees = VerticalErrorDegrees,
                TimeToNodeSeconds = TimeToNodeSeconds,
                TimeToIgnitionSeconds = TimeToIgnitionSeconds,
                PlannedDeltaVMetersPerSecond = PlannedDeltaVMetersPerSecond,
                BurnDurationSeconds = BurnDurationSeconds
            };
        }
    }
}
