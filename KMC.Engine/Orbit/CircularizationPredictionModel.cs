namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Evidence source used by the Build 10.1 circularization predictor.
    /// </summary>
    public enum OrbitPredictionThrustEvidence
    {
        Unknown = 0,

        /// <summary>
        /// Vessel-wide CurrentThrust / MaximumThrust values from the KMC6
        /// flight telemetry packet.
        /// </summary>
        FlightPacketVesselThrustSummary
    }

    /// <summary>
    /// Engine-owned circularization prediction migrated from the legacy
    /// MissionControl MissionPlanner.
    /// </summary>
    public sealed class CircularizationPredictionModel
    {
        public bool Available { get; internal set; }

        public OrbitPredictionThrustEvidence ThrustEvidence
        {
            get;
            internal set;
        }

        public double TargetOrbitMeters { get; internal set; }

        public double CurrentRadiusMeters { get; internal set; }

        public double TargetRadiusMeters { get; internal set; }

        public double CurrentOrbitalSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double RadialSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double TangentialSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double CurrentSpecificEnergyJoulesPerKilogram
        {
            get;
            internal set;
        }

        public double TargetSpecificEnergyJoulesPerKilogram
        {
            get;
            internal set;
        }

        public double EnergyErrorJoulesPerKilogram
        {
            get;
            internal set;
        }

        public double PredictedEnergyErrorJoulesPerKilogram
        {
            get;
            internal set;
        }

        public double TargetSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double RemainingDeltaVMetersPerSecond
        {
            get;
            internal set;
        }

        public double BurnTimeSeconds { get; internal set; }

        public double IgnitionInSeconds { get; internal set; }

        public double RecommendedThrottleFraction
        {
            get;
            internal set;
        }

        public double ShutdownResponseDeltaVMetersPerSecond
        {
            get;
            internal set;
        }

        public double PredictedApoapsisMeters
        {
            get;
            internal set;
        }

        public double PredictedPeriapsisMeters
        {
            get;
            internal set;
        }

        public double PredictedOrbitErrorMeters
        {
            get;
            internal set;
        }

        public double InitialDeltaVMetersPerSecond
        {
            get;
            internal set;
        }

        public double BurnCompletionPercent
        {
            get;
            internal set;
        }

        public string Status { get; internal set; } =
            string.Empty;

        internal static CircularizationPredictionModel Clone(
            CircularizationPredictionModel source)
        {
            if (source == null)
            {
                return new CircularizationPredictionModel();
            }

            return new CircularizationPredictionModel
            {
                Available =
                    source.Available,

                ThrustEvidence =
                    source.ThrustEvidence,

                TargetOrbitMeters =
                    source.TargetOrbitMeters,

                CurrentRadiusMeters =
                    source.CurrentRadiusMeters,

                TargetRadiusMeters =
                    source.TargetRadiusMeters,

                CurrentOrbitalSpeedMetersPerSecond =
                    source.CurrentOrbitalSpeedMetersPerSecond,

                RadialSpeedMetersPerSecond =
                    source.RadialSpeedMetersPerSecond,

                TangentialSpeedMetersPerSecond =
                    source.TangentialSpeedMetersPerSecond,

                CurrentSpecificEnergyJoulesPerKilogram =
                    source.CurrentSpecificEnergyJoulesPerKilogram,

                TargetSpecificEnergyJoulesPerKilogram =
                    source.TargetSpecificEnergyJoulesPerKilogram,

                EnergyErrorJoulesPerKilogram =
                    source.EnergyErrorJoulesPerKilogram,

                PredictedEnergyErrorJoulesPerKilogram =
                    source.PredictedEnergyErrorJoulesPerKilogram,

                TargetSpeedMetersPerSecond =
                    source.TargetSpeedMetersPerSecond,

                RemainingDeltaVMetersPerSecond =
                    source.RemainingDeltaVMetersPerSecond,

                BurnTimeSeconds =
                    source.BurnTimeSeconds,

                IgnitionInSeconds =
                    source.IgnitionInSeconds,

                RecommendedThrottleFraction =
                    source.RecommendedThrottleFraction,

                ShutdownResponseDeltaVMetersPerSecond =
                    source.ShutdownResponseDeltaVMetersPerSecond,

                PredictedApoapsisMeters =
                    source.PredictedApoapsisMeters,

                PredictedPeriapsisMeters =
                    source.PredictedPeriapsisMeters,

                PredictedOrbitErrorMeters =
                    source.PredictedOrbitErrorMeters,

                InitialDeltaVMetersPerSecond =
                    source.InitialDeltaVMetersPerSecond,

                BurnCompletionPercent =
                    source.BurnCompletionPercent,

                Status =
                    source.Status
            };
        }
    }
}
