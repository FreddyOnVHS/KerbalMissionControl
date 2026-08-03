namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Orbit state supplied to the orbital safety controller.
    /// </summary>
    public sealed class OrbitSafetyInput
    {
        public double TargetOrbitMeters { get; set; }

        public double ActualApoapsisMeters { get; set; }

        public double ActualPeriapsisMeters { get; set; }

        public bool GuidanceAvailable { get; set; }

        public double PredictedApoapsisMeters { get; set; }

        public double PredictedPeriapsisMeters { get; set; }

        public double PredictedOrbitErrorMeters { get; set; }

        public double PredictedEnergyError { get; set; }

        public double RemainingDeltaVMetersPerSecond { get; set; }
    }
}
