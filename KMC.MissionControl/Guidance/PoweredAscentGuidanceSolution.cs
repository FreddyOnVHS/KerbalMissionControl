namespace KMC.MissionControl.Guidance
{
    public sealed class PoweredAscentGuidanceSolution
    {
        public bool IsAvailable { get; set; }

        public double RecommendedPitchDegrees { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double PredictedPeriapsisMeters { get; set; }

        public double OrbitErrorMeters { get; set; }

        public double ConfidencePercent { get; set; }

        public double PoweredFlightSeconds { get; set; }

        public double CoastFlightSeconds { get; set; }

        public double PredictionConvergenceMeters { get; set; }

        public bool TargetCutoffReached { get; set; }

        public string Mode { get; set; }
    }
}
