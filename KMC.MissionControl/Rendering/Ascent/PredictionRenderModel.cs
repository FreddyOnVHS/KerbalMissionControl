namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Render-only projection of both Engine-owned ascent predictors.
    /// </summary>
    public sealed class PredictionRenderModel
    {
        public bool BurnoutAvailable { get; set; }
        public double BurnTimeRemainingSeconds { get; set; }
        public double BurnoutVelocityMetersPerSecond { get; set; }
        public double BurnoutPredictedApoapsisMeters { get; set; }
        public double BurnoutTargetErrorMeters { get; set; }
        public double BurnoutConfidencePercent { get; set; }
        public string BurnoutStatus { get; set; }
        public string BurnoutEvidence { get; set; }

        public bool PoweredAvailable { get; set; }
        public string PoweredMode { get; set; }
        public string PoweredInactiveReason { get; set; }
        public double PoweredPredictedApoapsisMeters { get; set; }
        public double PoweredPredictedPeriapsisMeters { get; set; }
        public double PoweredOrbitErrorMeters { get; set; }
        public double PoweredRecommendedPitchDegrees { get; set; }
        public double PoweredConfidencePercent { get; set; }
        public double PoweredFlightSeconds { get; set; }
        public double CoastFlightSeconds { get; set; }
        public bool ConvergenceKnown { get; set; }
        public double ConvergenceMeters { get; set; }
        public string ThrustEvidence { get; set; }
        public bool TargetCutoffReached { get; set; }
    }
}
