namespace KMC.Engine.Ascent
{
    public enum AscentPredictionFuelEvidence
    {
        Unknown = 0,

        /// <summary>
        /// Burn time is inferred from continuously changing current-stage
        /// LiquidFuel/Oxidizer quantities carried by the KMC6 flight packet.
        /// </summary>
        FlightPacketStageResourceTrend
    }

    /// <summary>
    /// Engine-owned powered-stage burnout prediction.
    ///
    /// Build 9.2 preserves the established MissionControl predictor math while
    /// making its evidence source and current prediction stage explicit.
    /// </summary>
    public sealed class AscentPredictionModel
    {
        public bool Available { get; internal set; }

        public bool HasFuelTrend { get; internal set; }

        public AscentPredictionFuelEvidence FuelEvidence
        {
            get;
            internal set;
        }

        public int PredictionStage { get; internal set; }

        public double StageAgeSeconds { get; internal set; }

        public int WindowSampleCount { get; internal set; }

        public double WindowDurationSeconds { get; internal set; }

        public double LiquidFuelConsumptionRatePerSecond
        {
            get;
            internal set;
        }

        public double OxidizerConsumptionRatePerSecond
        {
            get;
            internal set;
        }

        public double TimeRemainingSeconds { get; internal set; }

        public double BurnoutVelocityMetersPerSecond
        {
            get;
            internal set;
        }

        public double PredictedApoapsisMeters
        {
            get;
            internal set;
        }

        public double TargetApoapsisMeters { get; internal set; }

        public double TargetErrorMeters { get; internal set; }

        public double ConfidencePercent { get; internal set; }

        public string Status { get; internal set; } =
            string.Empty;
    }
}
