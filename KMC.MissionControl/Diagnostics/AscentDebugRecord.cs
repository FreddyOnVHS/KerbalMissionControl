using KMC.MissionControl.Guidance;

namespace KMC.MissionControl.Diagnostics
{
    /// <summary>
    /// Flat snapshot prepared by AscentPage for one CSV diagnostics row.
    /// </summary>
    public sealed class AscentDebugRecord
    {
        public double MissionTimeSeconds { get; set; }

        public int Stage { get; set; }

        public int InitialStage { get; set; }

        public double AltitudeMeters { get; set; }

        public double DownrangeMeters { get; set; }

        public double LiveThrustToWeightRatio { get; set; }

        public double PlanningThrustToWeightRatio { get; set; }

        public double ProfileScaleMeters { get; set; }

        public double TargetAltitudeMeters { get; set; }

        public double TargetPitchDegrees { get; set; }

        public double ActualPitchDegrees { get; set; }

        public double ApoapsisMeters { get; set; }

        public bool PredictionAvailable { get; set; }

        public double BurnTimeRemainingSeconds { get; set; }

        public double PredictedBurnoutVelocityMetersPerSecond { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double PredictionTargetErrorMeters { get; set; }

        public double PredictionConfidencePercent { get; set; }

        public string PredictionStatus { get; set; }

        public double ActualApoapsisMeters { get; set; }

        public double ActualPeriapsisMeters { get; set; }

        public MissionPlannerResult MissionPlan { get; set; }
    }
}
