namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Preview of the local parking-orbit impulse required to enter a hyperbolic
    /// departure whose outgoing asymptote matches a Lambert departure excess vector.
    ///
    /// Prograde/normal/radial components use an orthonormal local basis at burn UT:
    /// prograde = instantaneous parking-orbit velocity direction,
    /// normal = parking-orbit angular-momentum direction,
    /// radial = prograde x normal.
    /// </summary>
    public sealed class LambertParkingOrbitEjectionSolution
    {
        public double BurnUniversalTimeSeconds { get; internal set; }

        public double WindowOffsetSeconds { get; internal set; }

        public double ParkingRadiusMeters { get; internal set; }

        public double ParkingAltitudeMeters { get; internal set; }

        public double ParkingSpeedMetersPerSecond { get; internal set; }

        public double HyperbolicExcessSpeedMetersPerSecond { get; internal set; }

        public double HyperbolicPeriapsisSpeedMetersPerSecond { get; internal set; }

        public double HyperbolicEccentricity { get; internal set; }

        public double AsymptoteAngleDegrees { get; internal set; }

        public double ProgradeDeltaVMetersPerSecond { get; internal set; }

        public double NormalDeltaVMetersPerSecond { get; internal set; }

        public double RadialDeltaVMetersPerSecond { get; internal set; }

        public double TotalDeltaVMetersPerSecond { get; internal set; }

        /// <summary>
        /// Absolute difference between the achieved burn-radius/asymptote angle
        /// and the ideal hyperbolic asymptote angle. Small values indicate that
        /// the parking-orbit phase solution is internally consistent.
        /// </summary>
        public double GeometryResidualDegrees { get; internal set; }
    }
}
