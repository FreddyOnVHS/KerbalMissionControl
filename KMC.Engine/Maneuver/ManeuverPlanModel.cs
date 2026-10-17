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
            VesselId = string.Empty;
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

        /// <summary>
        /// True when post-burn verification has a specific predicted orbit
        /// target to compare against. Manual signed-DV maneuvers set this false.
        /// </summary>
        public bool OrbitTargetVerificationRequired { get; internal set; }

        public string PlanId { get; internal set; }

        public string VesselId { get; internal set; }

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

        /*
         * 14.22.69b:
         * Mission Control may request promotion of an already-reviewed KMC
         * maneuver, but ManeuverPlanModel remains Engine-owned. Convert the
         * public promotion DTO inside the Engine assembly so callers never
         * need access to internal plan setters.
         */
        public static ManeuverPlanModel CreateFromPromotionRequest(
            ManeuverPlanPromotionRequest request)
        {
            ManeuverPlanModel plan =
                new ManeuverPlanModel();

            if (request == null)
            {
                return plan;
            }

            plan.Available =
                request.Available;
            plan.OrbitTargetVerificationRequired =
                request.OrbitTargetVerificationRequired;
            plan.PlanId =
                request.PlanId ?? string.Empty;
            plan.VesselId =
                request.VesselId ?? string.Empty;
            plan.Objective =
                request.Objective ?? string.Empty;
            plan.NodeUniversalTimeAvailable =
                request.NodeUniversalTimeAvailable;
            plan.NodeUniversalTimeSeconds =
                request.NodeUniversalTimeSeconds;
            plan.NodeMissionTimeSeconds =
                request.NodeMissionTimeSeconds;
            plan.TimeToNodeSeconds =
                request.TimeToNodeSeconds;
            plan.ProgradeDeltaVMetersPerSecond =
                request.ProgradeDeltaVMetersPerSecond;
            plan.NormalDeltaVMetersPerSecond =
                request.NormalDeltaVMetersPerSecond;
            plan.RadialDeltaVMetersPerSecond =
                request.RadialDeltaVMetersPerSecond;
            plan.TotalDeltaVMetersPerSecond =
                request.TotalDeltaVMetersPerSecond;
            plan.EstimatedBurnDurationSeconds =
                request.EstimatedBurnDurationSeconds;
            plan.IgnitionLeadSeconds =
                request.IgnitionLeadSeconds;
            plan.IgnitionMissionTimeSeconds =
                request.IgnitionMissionTimeSeconds;
            plan.PredictedApoapsisMeters =
                request.PredictedApoapsisMeters;
            plan.PredictedPeriapsisMeters =
                request.PredictedPeriapsisMeters;
            plan.PredictedInclinationDegrees =
                request.PredictedInclinationDegrees;
            plan.PredictedEccentricity =
                request.PredictedEccentricity;
            plan.PredictedPeriodSeconds =
                request.PredictedPeriodSeconds;
            plan.Status =
                request.Status ?? string.Empty;

            if (request.Evidence != null)
            {
                for (int index = 0;
                     index < request.Evidence.Count;
                     index++)
                {
                    plan.Evidence.Add(
                        request.Evidence[index]);
                }
            }

            return plan;
        }

        internal static ManeuverPlanModel Clone(ManeuverPlanModel source)
        {
            ManeuverPlanModel clone = new ManeuverPlanModel();

            if (source == null)
            {
                return clone;
            }

            clone.Available = source.Available;
            clone.OrbitTargetVerificationRequired =
                source.OrbitTargetVerificationRequired;
            clone.PlanId = source.PlanId;
            clone.VesselId = source.VesselId;
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
