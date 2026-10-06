namespace KMC.Engine.Navigation
{
    public sealed class FiniteSoiDepartureAssessment
    {
        public double ExitUniversalTimeSeconds { get; internal set; }
        public double TimeFromBurnToExitSeconds { get; internal set; }

        public double PositionErrorMeters { get; internal set; }
        public double VelocityErrorMetersPerSecond { get; internal set; }

        public double PositionErrorFractionOfSoi { get; internal set; }
        public double VelocityErrorFractionOfDepartureExcess { get; internal set; }

        public double NormalizedStateError { get; internal set; }
    }
}
