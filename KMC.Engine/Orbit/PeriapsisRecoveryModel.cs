namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned human-in-the-loop periapsis-recovery recommendation.
    ///
    /// This mirrors the legacy MissionControl PeriapsisRecoverySolution while
    /// adding explicit availability/active state for ORBIT ownership.
    /// </summary>
    public sealed class PeriapsisRecoveryModel
    {
        public bool Available { get; internal set; }

        public bool Active { get; internal set; }

        public double PeriapsisErrorMeters { get; internal set; }

        public double ThrottlePercent { get; internal set; }

        public double DesiredThrottlePercent { get; internal set; }

        public double CommandAgeSeconds { get; internal set; }

        public bool CommandHeldByHysteresis { get; internal set; }

        public bool CutoffRequired { get; internal set; }

        public bool ActualPeriapsisSafe { get; internal set; }

        public bool PredictedPeriapsisSafe { get; internal set; }

        public bool ProducingThrust { get; internal set; }

        public double ActualPeriapsisMeters { get; internal set; }

        public double PredictedPeriapsisMeters { get; internal set; }

        public string Reason { get; internal set; } =
            "RECOVERY WAITING";

        internal static PeriapsisRecoveryModel Clone(
            PeriapsisRecoveryModel source)
        {
            if (source == null)
            {
                return new PeriapsisRecoveryModel();
            }

            return new PeriapsisRecoveryModel
            {
                Available =
                    source.Available,

                Active =
                    source.Active,

                PeriapsisErrorMeters =
                    source.PeriapsisErrorMeters,

                ThrottlePercent =
                    source.ThrottlePercent,

                DesiredThrottlePercent =
                    source.DesiredThrottlePercent,

                CommandAgeSeconds =
                    source.CommandAgeSeconds,

                CommandHeldByHysteresis =
                    source.CommandHeldByHysteresis,

                CutoffRequired =
                    source.CutoffRequired,

                ActualPeriapsisSafe =
                    source.ActualPeriapsisSafe,

                PredictedPeriapsisSafe =
                    source.PredictedPeriapsisSafe,

                ProducingThrust =
                    source.ProducingThrust,

                ActualPeriapsisMeters =
                    source.ActualPeriapsisMeters,

                PredictedPeriapsisMeters =
                    source.PredictedPeriapsisMeters,

                Reason =
                    source.Reason
            };
        }
    }
}
