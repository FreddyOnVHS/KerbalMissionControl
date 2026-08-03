namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Result produced by the powered-ascent burnout predictor.
    /// </summary>
    public sealed class BurnoutPrediction
    {
        public bool IsAvailable { get; set; }

        public bool HasFuelTrend { get; set; }

        public double TimeRemainingSeconds { get; set; }

        public double BurnoutVelocityMetersPerSecond { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double ConfidencePercent { get; set; }

        public string Status { get; set; }
    }
}
