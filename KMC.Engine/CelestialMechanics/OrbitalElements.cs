namespace KMC.Engine.CelestialMechanics
{
    /// <summary>Osculating two-body elements. a is positive for ellipses, negative for hyperbolae.
    /// Orientation uses Rz(LAN) Rx(inclination) Rz(argument of periapsis).
    /// Mean anomaly is radians; orientation angles are degrees; lengths and times are SI.
    /// Period is optional telemetry for the legacy planner; propagation uses mu and a.</summary>
    public sealed class OrbitalElements
    {
        public string ReferenceBodyName { get; set; }
        public double SemiMajorAxisMeters { get; set; }
        public double Eccentricity { get; set; }
        public double InclinationDegrees { get; set; }
        public double LongitudeOfAscendingNodeDegrees { get; set; }
        public double ArgumentOfPeriapsisDegrees { get; set; }
        public double EpochUniversalTimeSeconds { get; set; }
        public double MeanAnomalyAtEpochRadians { get; set; }
        public double PeriodSeconds { get; set; }

        internal bool HasFiniteGeometry
        {
            get
            {
                return Vector3d.Finite(SemiMajorAxisMeters) && Vector3d.Finite(Eccentricity) &&
                    Vector3d.Finite(InclinationDegrees) && Vector3d.Finite(LongitudeOfAscendingNodeDegrees) &&
                    Vector3d.Finite(ArgumentOfPeriapsisDegrees) && Vector3d.Finite(EpochUniversalTimeSeconds) &&
                    Vector3d.Finite(MeanAnomalyAtEpochRadians);
            }
        }
    }
}
