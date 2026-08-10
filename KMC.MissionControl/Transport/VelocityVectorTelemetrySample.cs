using System;

namespace KMC.MissionControl.Transport
{
    public sealed class VelocityVectorTelemetrySample
    {
        public DateTime SourceTimestampUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public string VesselName { get; set; } =
            string.Empty;

        public double SurfaceRightMetersPerSecond { get; set; }

        public double SurfaceNoseMetersPerSecond { get; set; }

        public double SurfaceReferenceForwardMetersPerSecond
        {
            get;
            set;
        }

        public double OrbitalRightMetersPerSecond { get; set; }

        public double OrbitalNoseMetersPerSecond { get; set; }

        public double OrbitalReferenceForwardMetersPerSecond
        {
            get;
            set;
        }
    }
}
