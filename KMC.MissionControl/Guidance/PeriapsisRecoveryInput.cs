namespace KMC.MissionControl.Guidance
{
    public sealed class PeriapsisRecoveryInput
    {
        public double ActualApoapsisMeters { get; set; }
        public double ActualPeriapsisMeters { get; set; }
        public double PredictedApoapsisMeters { get; set; }
        public double PredictedPeriapsisMeters { get; set; }
        public bool GuidanceAvailable { get; set; }
        public bool ProducingThrust { get; set; }
        public double Throttle { get; set; }
    }
}
