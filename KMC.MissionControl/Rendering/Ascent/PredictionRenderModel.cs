namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Prepared display data for the Predicted Burnout panel.
    /// Prediction mathematics remain inside AscentPage for this step.
    /// </summary>
    public sealed class PredictionRenderModel
    {
        public bool IsAvailable { get; set; }

        public double TimeRemainingSeconds { get; set; }

        public double BurnoutVelocityMetersPerSecond { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double TargetApoapsisMeters { get; set; }

        public double ConfidencePercent { get; set; }

        public string Status { get; set; }
    }
}
