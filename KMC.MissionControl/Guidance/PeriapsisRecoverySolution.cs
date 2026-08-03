namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Throttle and cutoff recommendation for periapsis recovery.
    /// </summary>
    public sealed class PeriapsisRecoverySolution
    {
        public double PeriapsisErrorMeters { get; set; }

        public double ThrottlePercent { get; set; }

        public double DesiredThrottlePercent { get; set; }

        public double CommandAgeSeconds { get; set; }

        public bool CommandHeldByHysteresis { get; set; }

        public bool CutoffRequired { get; set; }

        public bool ActualPeriapsisSafe { get; set; }

        public bool PredictedPeriapsisSafe { get; set; }

        public bool ProducingThrust { get; set; }

        public string Reason { get; set; }
    }
}
