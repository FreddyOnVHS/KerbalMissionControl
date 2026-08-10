namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned advisory ORBIT flight-director result.
    ///
    /// This model never commands the vehicle. It combines Engine-owned orbit
    /// prediction, safety, periapsis recovery, and verified orbital-prograde
    /// vector evidence into one human-readable guidance state.
    /// </summary>
    public sealed class OrbitFlightDirectorModel
    {
        public bool Available { get; internal set; }

        public string FlightPhase { get; internal set; } =
            "ORBIT WAITING";

        public string Command { get; internal set; } =
            "AWAIT ORBIT HANDOFF";

        public string AttitudeCommand { get; internal set; } =
            "HOLD ATTITUDE";

        public string ThrottleCommand { get; internal set; } =
            "THROTTLE 0%";

        public string Status { get; internal set; } =
            "GUIDANCE WAITING";

        public string NextEvent { get; internal set; } =
            "---";

        public double ThrottleCommandPercent { get; internal set; }

        public bool CutoffRequired { get; internal set; }

        public bool CoastLockoutActive { get; internal set; }

        public bool IgnitionDue { get; internal set; }

        public bool CircularizationStarted { get; internal set; }

        public bool PeriapsisRecoveryActive { get; internal set; }

        public bool OrbitAchieved { get; internal set; }

        public bool ProgradeAvailable { get; internal set; }

        public double OrbitalProgradeRightMetersPerSecond
        {
            get;
            internal set;
        }

        public double OrbitalProgradeNoseMetersPerSecond
        {
            get;
            internal set;
        }

        public double OrbitalProgradeReferenceForwardMetersPerSecond
        {
            get;
            internal set;
        }

        public double OrbitalProgradeMagnitudeMetersPerSecond
        {
            get;
            internal set;
        }

        public double IgnitionInSeconds { get; internal set; }

        public double BurnTimeSeconds { get; internal set; }

        public double RemainingDeltaVMetersPerSecond
        {
            get;
            internal set;
        }

        public double BurnCompletionPercent { get; internal set; }

        public double ActualApoapsisMeters { get; internal set; }

        public double ActualPeriapsisMeters { get; internal set; }

        public double PredictedApoapsisMeters { get; internal set; }

        public double PredictedPeriapsisMeters { get; internal set; }

        public string DecisionSource { get; internal set; } =
            "ORBIT FOUNDATION";

        internal static OrbitFlightDirectorModel Clone(
            OrbitFlightDirectorModel source)
        {
            if (source == null)
            {
                return new OrbitFlightDirectorModel();
            }

            return new OrbitFlightDirectorModel
            {
                Available =
                    source.Available,

                FlightPhase =
                    source.FlightPhase,

                Command =
                    source.Command,

                AttitudeCommand =
                    source.AttitudeCommand,

                ThrottleCommand =
                    source.ThrottleCommand,

                Status =
                    source.Status,

                NextEvent =
                    source.NextEvent,

                ThrottleCommandPercent =
                    source.ThrottleCommandPercent,

                CutoffRequired =
                    source.CutoffRequired,

                CoastLockoutActive =
                    source.CoastLockoutActive,

                IgnitionDue =
                    source.IgnitionDue,

                CircularizationStarted =
                    source.CircularizationStarted,

                PeriapsisRecoveryActive =
                    source.PeriapsisRecoveryActive,

                OrbitAchieved =
                    source.OrbitAchieved,

                ProgradeAvailable =
                    source.ProgradeAvailable,

                OrbitalProgradeRightMetersPerSecond =
                    source.OrbitalProgradeRightMetersPerSecond,

                OrbitalProgradeNoseMetersPerSecond =
                    source.OrbitalProgradeNoseMetersPerSecond,

                OrbitalProgradeReferenceForwardMetersPerSecond =
                    source.OrbitalProgradeReferenceForwardMetersPerSecond,

                OrbitalProgradeMagnitudeMetersPerSecond =
                    source.OrbitalProgradeMagnitudeMetersPerSecond,

                IgnitionInSeconds =
                    source.IgnitionInSeconds,

                BurnTimeSeconds =
                    source.BurnTimeSeconds,

                RemainingDeltaVMetersPerSecond =
                    source.RemainingDeltaVMetersPerSecond,

                BurnCompletionPercent =
                    source.BurnCompletionPercent,

                ActualApoapsisMeters =
                    source.ActualApoapsisMeters,

                ActualPeriapsisMeters =
                    source.ActualPeriapsisMeters,

                PredictedApoapsisMeters =
                    source.PredictedApoapsisMeters,

                PredictedPeriapsisMeters =
                    source.PredictedPeriapsisMeters,

                DecisionSource =
                    source.DecisionSource
            };
        }
    }
}
