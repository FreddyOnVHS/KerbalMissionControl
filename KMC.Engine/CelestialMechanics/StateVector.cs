namespace KMC.Engine.CelestialMechanics
{
    /// <summary>Position (m), velocity (m/s), UT (s), and central mu (m³/s²).
    /// XYZ is inertial/right-handed, XY the reference plane, +Z its normal.
    /// ReferenceBodyName identifies the frame origin, not a rotating body-fixed frame.</summary>
    public sealed class StateVector
    {
        public Vector3d Position { get; private set; }
        public Vector3d Velocity { get; private set; }
        public double UniversalTimeSeconds { get; private set; }
        public double GravParameter { get; private set; }
        public string ReferenceBodyName { get; private set; }
        public StateVector(Vector3d position, Vector3d velocity, double ut, double mu, string referenceBodyName)
        {
            Position = position; Velocity = velocity; UniversalTimeSeconds = ut;
            GravParameter = mu; ReferenceBodyName = referenceBodyName ?? string.Empty;
        }
    }
}
