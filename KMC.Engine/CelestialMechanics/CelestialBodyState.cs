namespace KMC.Engine.CelestialMechanics
{
    /// <summary>Generic body data; Orbit describes motion relative to ParentName.
    /// GravParameter belongs to this body, not its parent. No stock body constants.</summary>
    public sealed class CelestialBodyState
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
        public double RadiusMeters { get; set; }
        public double SoiRadiusMeters { get; set; }
        public double GravParameter { get; set; }
        public OrbitalElements Orbit { get; set; }
    }
}
