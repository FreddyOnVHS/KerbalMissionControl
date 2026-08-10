using System;
using System.Collections.Generic;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Engine-owned maneuver plan produced from validated engineering state.
    /// Build 11.0 supports CIRCULARIZE AT APOAPSIS only.
    /// </summary>
    public sealed class ManeuverPlanModel
    {
        public ManeuverPlanModel()
        {
            PlanId = string.Empty;
            Objective = string.Empty;
            Status = "PLAN UNAVAILABLE";
            Evidence = new List<string>();
            NodeUniversalTimeSeconds = double.NaN;
            NodeMissionTimeSeconds = double.NaN;
            TimeToNodeSeconds = double.NaN;
            EstimatedBurnDurationSeconds = double.NaN;
            IgnitionLeadSeconds = double.NaN;
            IgnitionMissionTimeSeconds = double.NaN;
            PredictedApoapsisMeters = double.NaN;
            PredictedPeriapsisMeters = double.NaN;
            PredictedInclinationDegrees = double.NaN;
            PredictedEccentricity = double.NaN;
            PredictedPeriodSeconds = double.NaN;
        }

        public bool Available { get; internal set; }

        public string PlanId { get; internal set; }

        public string Objective { get; internal set; }

        /// <summary>
        /// KSP Universal Time is deliberately unavailable in Build 11.0 because
        /// the current KMC6 telemetry protocol does not transport Planetarium UT.
        /// This must never be synthesized from wall-clock time or MET.
        /// </summary>
        public bool NodeUniversalTimeAvailable { get; internal set; }

        public double NodeUniversalTimeSeconds { get; internal set; }

        /// <summary>
        /// Maneuver epoch expressed in mission elapsed time. For the initial
        /// apoapsis circularization plan this is current MET + time-to-apoapsis.
        /// </summary>
        public double NodeMissionTimeSeconds { get; internal set; }

        public double TimeToNodeSeconds { get; internal set; }

        public double ProgradeDeltaVMetersPerSecond { get; internal set; }

        public double NormalDeltaVMetersPerSecond { get; internal set; }

        public double RadialDeltaVMetersPerSecond { get; internal set; }

        public double TotalDeltaVMetersPerSecond { get; internal set; }

        public double EstimatedBurnDurationSeconds { get; internal set; }

        public double IgnitionLeadSeconds { get; internal set; }

        public double IgnitionMissionTimeSeconds { get; internal set; }

        public double PredictedApoapsisMeters { get; internal set; }

        public double PredictedPeriapsisMeters { get; internal set; }

        public double PredictedInclinationDegrees { get; internal set; }

        public double PredictedEccentricity { get; internal set; }

        public double PredictedPeriodSeconds { get; internal set; }

        public string Status { get; internal set; }

        public IList<string> Evidence { get; private set; }

        internal static ManeuverPlanModel Clone(ManeuverPlanModel source)
        {
            ManeuverPlanModel clone = new ManeuverPlanModel();

            if (source == null)
            {
                return clone;
            }

            clone.Available = source.Available;
            clone.PlanId = source.PlanId;
            clone.Objective = source.Objective;
            clone.NodeUniversalTimeAvailable = source.NodeUniversalTimeAvailable;
            clone.NodeUniversalTimeSeconds = source.NodeUniversalTimeSeconds;
            clone.NodeMissionTimeSeconds = source.NodeMissionTimeSeconds;
            clone.TimeToNodeSeconds = source.TimeToNodeSeconds;
            clone.ProgradeDeltaVMetersPerSecond = source.ProgradeDeltaVMetersPerSecond;
            clone.NormalDeltaVMetersPerSecond = source.NormalDeltaVMetersPerSecond;
            clone.RadialDeltaVMetersPerSecond = source.RadialDeltaVMetersPerSecond;
            clone.TotalDeltaVMetersPerSecond = source.TotalDeltaVMetersPerSecond;
            clone.EstimatedBurnDurationSeconds = source.EstimatedBurnDurationSeconds;
            clone.IgnitionLeadSeconds = source.IgnitionLeadSeconds;
            clone.IgnitionMissionTimeSeconds = source.IgnitionMissionTimeSeconds;
            clone.PredictedApoapsisMeters = source.PredictedApoapsisMeters;
            clone.PredictedPeriapsisMeters = source.PredictedPeriapsisMeters;
            clone.PredictedInclinationDegrees = source.PredictedInclinationDegrees;
            clone.PredictedEccentricity = source.PredictedEccentricity;
            clone.PredictedPeriodSeconds = source.PredictedPeriodSeconds;
            clone.Status = source.Status;

            for (int index = 0; index < source.Evidence.Count; index++)
            {
                clone.Evidence.Add(source.Evidence[index]);
            }

            return clone;
        }
    }
}
