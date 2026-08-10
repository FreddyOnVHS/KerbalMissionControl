using System;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned true 3-D velocity-vector telemetry.
    ///
    /// Components are resolved in the KSP vessel ReferenceTransform frame:
    /// Right, Nose (ReferenceTransform.up), and ReferenceForward.
    /// Surface and orbital vectors are retained independently.
    /// </summary>
    public sealed class VelocityVectorTelemetryModel
    {
        public bool TelemetryAvailable { get; set; }

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

        public double SurfaceMagnitudeMetersPerSecond
        {
            get;
            internal set;
        }

        public double OrbitalMagnitudeMetersPerSecond
        {
            get;
            internal set;
        }

        public bool Fresh { get; internal set; }

        public bool VesselMatchesFlightPacket { get; internal set; }

        public bool Available { get; internal set; }

        public double FlightPacketSurfaceSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double FlightPacketOrbitalSpeedMetersPerSecond
        {
            get;
            internal set;
        }

        public double SurfaceSpeedDifferenceMetersPerSecond
        {
            get;
            internal set;
        }

        public double OrbitalSpeedDifferenceMetersPerSecond
        {
            get;
            internal set;
        }

        public bool SurfaceSpeedAgreement { get; internal set; }

        public bool OrbitalSpeedAgreement { get; internal set; }

        public string Status { get; internal set; } =
            "NO VELOCITY TELEMETRY";

        internal void EvaluateAgainstFlightPacket(
            KMC.Shared.TelemetryPacket packet,
            DateTime analysisUtc)
        {
            SurfaceMagnitudeMetersPerSecond =
                Magnitude(
                    SurfaceRightMetersPerSecond,
                    SurfaceNoseMetersPerSecond,
                    SurfaceReferenceForwardMetersPerSecond);

            OrbitalMagnitudeMetersPerSecond =
                Magnitude(
                    OrbitalRightMetersPerSecond,
                    OrbitalNoseMetersPerSecond,
                    OrbitalReferenceForwardMetersPerSecond);

            double ageSeconds =
                Math.Abs(
                    (analysisUtc -
                     ReceivedUtc)
                    .TotalSeconds);

            Fresh =
                TelemetryAvailable &&
                ageSeconds <=
                    0.75;

            VesselMatchesFlightPacket =
                packet != null &&
                (string.IsNullOrEmpty(
                     VesselName) ||
                 string.Equals(
                     VesselName,
                     packet.VesselName ??
                     string.Empty,
                     StringComparison.Ordinal));

            FlightPacketSurfaceSpeedMetersPerSecond =
                packet != null
                    ? Math.Max(
                        0.0,
                        packet.SurfaceSpeed)
                    : 0.0;

            FlightPacketOrbitalSpeedMetersPerSecond =
                packet != null
                    ? Math.Max(
                        0.0,
                        packet.OrbitalSpeed)
                    : 0.0;

            SurfaceSpeedDifferenceMetersPerSecond =
                Math.Abs(
                    SurfaceMagnitudeMetersPerSecond -
                    FlightPacketSurfaceSpeedMetersPerSecond);

            OrbitalSpeedDifferenceMetersPerSecond =
                Math.Abs(
                    OrbitalMagnitudeMetersPerSecond -
                    FlightPacketOrbitalSpeedMetersPerSecond);

            double surfaceTolerance =
                Math.Max(
                    1.0,
                    FlightPacketSurfaceSpeedMetersPerSecond *
                    0.005);

            double orbitalTolerance =
                Math.Max(
                    1.0,
                    FlightPacketOrbitalSpeedMetersPerSecond *
                    0.005);

            SurfaceSpeedAgreement =
                SurfaceSpeedDifferenceMetersPerSecond <=
                    surfaceTolerance;

            OrbitalSpeedAgreement =
                OrbitalSpeedDifferenceMetersPerSecond <=
                    orbitalTolerance;

            Available =
                TelemetryAvailable &&
                Fresh &&
                VesselMatchesFlightPacket;

            if (!TelemetryAvailable)
            {
                Status =
                    "NO VELOCITY TELEMETRY";
            }
            else if (!Fresh)
            {
                Status =
                    "VELOCITY TELEMETRY STALE";
            }
            else if (!VesselMatchesFlightPacket)
            {
                Status =
                    "VELOCITY VESSEL MISMATCH";
            }
            else if (!SurfaceSpeedAgreement ||
                     !OrbitalSpeedAgreement)
            {
                Status =
                    "VECTOR MAGNITUDE DISAGREEMENT";
            }
            else
            {
                Status =
                    "VECTOR VERIFIED";
            }
        }

        internal static VelocityVectorTelemetryModel Clone(
            VelocityVectorTelemetryModel source)
        {
            if (source == null)
            {
                return new VelocityVectorTelemetryModel();
            }

            return new VelocityVectorTelemetryModel
            {
                TelemetryAvailable =
                    source.TelemetryAvailable,

                SourceTimestampUtc =
                    source.SourceTimestampUtc,

                ReceivedUtc =
                    source.ReceivedUtc,

                VesselName =
                    source.VesselName,

                SurfaceRightMetersPerSecond =
                    source.SurfaceRightMetersPerSecond,

                SurfaceNoseMetersPerSecond =
                    source.SurfaceNoseMetersPerSecond,

                SurfaceReferenceForwardMetersPerSecond =
                    source.SurfaceReferenceForwardMetersPerSecond,

                OrbitalRightMetersPerSecond =
                    source.OrbitalRightMetersPerSecond,

                OrbitalNoseMetersPerSecond =
                    source.OrbitalNoseMetersPerSecond,

                OrbitalReferenceForwardMetersPerSecond =
                    source.OrbitalReferenceForwardMetersPerSecond,

                SurfaceMagnitudeMetersPerSecond =
                    source.SurfaceMagnitudeMetersPerSecond,

                OrbitalMagnitudeMetersPerSecond =
                    source.OrbitalMagnitudeMetersPerSecond,

                Fresh =
                    source.Fresh,

                VesselMatchesFlightPacket =
                    source.VesselMatchesFlightPacket,

                Available =
                    source.Available,

                FlightPacketSurfaceSpeedMetersPerSecond =
                    source.FlightPacketSurfaceSpeedMetersPerSecond,

                FlightPacketOrbitalSpeedMetersPerSecond =
                    source.FlightPacketOrbitalSpeedMetersPerSecond,

                SurfaceSpeedDifferenceMetersPerSecond =
                    source.SurfaceSpeedDifferenceMetersPerSecond,

                OrbitalSpeedDifferenceMetersPerSecond =
                    source.OrbitalSpeedDifferenceMetersPerSecond,

                SurfaceSpeedAgreement =
                    source.SurfaceSpeedAgreement,

                OrbitalSpeedAgreement =
                    source.OrbitalSpeedAgreement,

                Status =
                    source.Status
            };
        }

        private static double Magnitude(
            double x,
            double y,
            double z)
        {
            double squared =
                x * x +
                y * y +
                z * z;

            return
                squared > 0.0
                    ? Math.Sqrt(
                        squared)
                    : 0.0;
        }
    }
}
