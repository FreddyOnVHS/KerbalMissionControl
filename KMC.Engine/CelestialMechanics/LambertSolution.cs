namespace KMC.Engine.CelestialMechanics
{
    /// <summary>
    /// Zero-revolution Lambert boundary-value solution in one declared inertial
    /// reference frame. Velocities are relative to the same central body as the
    /// supplied endpoint position vectors.
    /// </summary>
    public sealed class LambertSolution
    {
        public Vector3d DepartureVelocity { get; private set; }
        public Vector3d ArrivalVelocity { get; private set; }
        public double TimeOfFlightSeconds { get; private set; }
        public double GravParameter { get; private set; }
        public LambertTransferPath Path { get; private set; }

        internal LambertSolution(
            Vector3d departureVelocity,
            Vector3d arrivalVelocity,
            double timeOfFlightSeconds,
            double gravParameter,
            LambertTransferPath path)
        {
            DepartureVelocity = departureVelocity;
            ArrivalVelocity = arrivalVelocity;
            TimeOfFlightSeconds = timeOfFlightSeconds;
            GravParameter = gravParameter;
            Path = path;
        }
    }
}
