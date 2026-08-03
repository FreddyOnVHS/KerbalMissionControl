namespace KMC.MissionControl.Guidance
{
    public sealed class PeriapsisRecoverySolution
    {
        public double PeriapsisErrorMeters { get; set; }
        public double ThrottlePercent { get; set; }
        public bool CutoffRequired { get; set; }
        public bool ActualPeriapsisSafe { get; set; }
        public bool PredictedPeriapsisSafe { get; set; }
        public bool ProducingThrust { get; set; }
        public string Reason { get; set; }
    }
}
