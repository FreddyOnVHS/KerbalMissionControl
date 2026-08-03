namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Current orbit and timing state supplied to the human-in-the-loop
    /// periapsis recovery controller.
    /// </summary>
    public sealed class PeriapsisRecoveryInput
    {
        public double MissionTimeSeconds { get; set; }

        public double ActualApoapsisMeters { get; set; }

        public double ActualPeriapsisMeters { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double PredictedPeriapsisMeters { get; set; }

        public bool GuidanceAvailable { get; set; }

        public bool ProducingThrust { get; set; }

        public double Throttle { get; set; }
    }
}
