namespace KMC.Engine.Navigation
{
    public sealed class TransferWindowSolution
    {
        public string ParentName;
        public double CurrentPhaseDegrees;
        public double RequiredPhaseDegrees;
        public double WaitSeconds;
        public double DepartureUniversalTimeSeconds;
        public double TransferTimeSeconds;
        public double ParentFrameDeltaVMetersPerSecond;
    }
}
