namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Target-intercept assessment for a trial parking-orbit ejection.
    /// The spacecraft is propagated through the source SOI and then through the
    /// parent two-body frame to the selected arrival epoch.
    /// </summary>
    public sealed class TargetSoiShootingAssessment
    {
        public double ArrivalUniversalTimeSeconds { get; internal set; }

        public double SourceSoiExitUniversalTimeSeconds { get; internal set; }

        public double MissDistanceMeters { get; internal set; }

        public double MissFractionOfTargetSoi { get; internal set; }

        public double RelativeSpeedAtArrivalMetersPerSecond { get; internal set; }

        public double SourceLambertVelocityMismatchMetersPerSecond
        {
            get;
            internal set;
        }

        public bool PredictedEncounter { get; internal set; }
    }
}
