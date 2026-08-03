namespace KMC.MissionControl.Guidance
{
    internal sealed class AscentTrajectoryPrediction
    {
        public bool IsValid { get; set; }

        public double PitchDegrees { get; set; }

        public double ApoapsisMeters { get; set; }

        public double PeriapsisMeters { get; set; }

        public double FinalVerticalSpeedMetersPerSecond { get; set; }

        public double Score { get; set; }
    }
}
