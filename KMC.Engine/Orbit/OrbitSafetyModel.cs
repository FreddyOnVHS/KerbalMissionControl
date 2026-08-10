namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned ORBIT completion/protective-stop decision.
    ///
    /// Migrated from the legacy MissionControl OrbitSafetyDecision without
    /// changing thresholds or decision semantics.
    /// </summary>
    public sealed class OrbitSafetyModel
    {
        public bool Available { get; internal set; }

        public bool CircularizationStarted { get; internal set; }

        public bool OrbitAchieved { get; internal set; }

        public bool CutoffRequired { get; internal set; }

        public bool CutoffLatched { get; internal set; }

        public bool PauseBurn { get; internal set; }

        public bool ActualPeriapsisSafe { get; internal set; }

        public bool PredictedPeriapsisSafe { get; internal set; }

        public bool EnergySatisfied { get; internal set; }

        public bool DeltaVSatisfied { get; internal set; }

        public bool PredictedOrbitNominal { get; internal set; }

        public bool PredictedApoapsisTooHigh { get; internal set; }

        public bool ActualApoapsisTooHigh { get; internal set; }

        public bool PredictedOrbitTooHigh { get; internal set; }

        public double TargetOrbitMeters { get; internal set; }

        public double ActualApoapsisMeters { get; internal set; }

        public double ActualPeriapsisMeters { get; internal set; }

        public double PredictedApoapsisMeters { get; internal set; }

        public double PredictedPeriapsisMeters { get; internal set; }

        public double PredictedOrbitErrorMeters { get; internal set; }

        public double PredictedEnergyErrorJoulesPerKilogram
        {
            get;
            internal set;
        }

        public double RemainingDeltaVMetersPerSecond
        {
            get;
            internal set;
        }

        public string Reason { get; internal set; } =
            "SAFETY WAITING";

        internal static OrbitSafetyModel Clone(
            OrbitSafetyModel source)
        {
            if (source == null)
            {
                return new OrbitSafetyModel();
            }

            return new OrbitSafetyModel
            {
                Available =
                    source.Available,

                CircularizationStarted =
                    source.CircularizationStarted,

                OrbitAchieved =
                    source.OrbitAchieved,

                CutoffRequired =
                    source.CutoffRequired,

                CutoffLatched =
                    source.CutoffLatched,

                PauseBurn =
                    source.PauseBurn,

                ActualPeriapsisSafe =
                    source.ActualPeriapsisSafe,

                PredictedPeriapsisSafe =
                    source.PredictedPeriapsisSafe,

                EnergySatisfied =
                    source.EnergySatisfied,

                DeltaVSatisfied =
                    source.DeltaVSatisfied,

                PredictedOrbitNominal =
                    source.PredictedOrbitNominal,

                PredictedApoapsisTooHigh =
                    source.PredictedApoapsisTooHigh,

                ActualApoapsisTooHigh =
                    source.ActualApoapsisTooHigh,

                PredictedOrbitTooHigh =
                    source.PredictedOrbitTooHigh,

                TargetOrbitMeters =
                    source.TargetOrbitMeters,

                ActualApoapsisMeters =
                    source.ActualApoapsisMeters,

                ActualPeriapsisMeters =
                    source.ActualPeriapsisMeters,

                PredictedApoapsisMeters =
                    source.PredictedApoapsisMeters,

                PredictedPeriapsisMeters =
                    source.PredictedPeriapsisMeters,

                PredictedOrbitErrorMeters =
                    source.PredictedOrbitErrorMeters,

                PredictedEnergyErrorJoulesPerKilogram =
                    source.PredictedEnergyErrorJoulesPerKilogram,

                RemainingDeltaVMetersPerSecond =
                    source.RemainingDeltaVMetersPerSecond,

                Reason =
                    source.Reason
            };
        }
    }
}
