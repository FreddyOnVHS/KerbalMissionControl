namespace KMC.Engine.Navigation
{
    public sealed class ParkingOrbitEjectionSolution
    {
        public double ParkingAltitudeMeters;
        public double ParkingSpeedMetersPerSecond;
        public double HyperbolicPeriapsisSpeedMetersPerSecond;
        public double HyperbolicExcessSpeedMetersPerSecond;
        public double EjectionDeltaVMetersPerSecond;
        public double AsymptoteAngleDegrees;
        public double BurnUniversalTimeSeconds;
        public double WindowOffsetSeconds;
        public string ExcessDirection;
    }
}
