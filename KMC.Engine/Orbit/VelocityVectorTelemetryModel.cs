using System;
using System.Collections.Generic;

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

    /// <summary>
    /// Build 13.4 true orbital-plane normal direction resolved into the same
    /// vessel ReferenceTransform frame used by KMC-VEL1.
    /// Components are unit-vector components, not velocities.
    /// </summary>
    public sealed class OrbitNormalTelemetryModel
    {
        public bool TelemetryAvailable { get; set; }

        public DateTime SourceTimestampUtc { get; set; }

        public DateTime ReceivedUtc { get; set; }

        public string VesselName { get; set; } =
            string.Empty;

        public double RightComponent { get; set; }

        public double NoseComponent { get; set; }

        public double ReferenceForwardComponent { get; set; }

        public double Magnitude { get; internal set; }

        public double VelocityOrthogonalityDot { get; internal set; }

        public bool Fresh { get; internal set; }

        public bool VesselMatchesVelocityVector { get; internal set; }

        public bool Available { get; internal set; }

        public string Status { get; internal set; } =
            "NO ORBIT NORMAL TELEMETRY";

        internal void EvaluateAgainstVelocity(
            VelocityVectorTelemetryModel velocity,
            DateTime analysisUtc)
        {
            Magnitude =
                Math.Sqrt(
                    RightComponent * RightComponent +
                    NoseComponent * NoseComponent +
                    ReferenceForwardComponent *
                    ReferenceForwardComponent);

            double ageSeconds =
                Math.Abs(
                    (analysisUtc -
                     ReceivedUtc)
                    .TotalSeconds);

            Fresh =
                TelemetryAvailable &&
                ageSeconds <=
                    0.75;

            VesselMatchesVelocityVector =
                velocity != null &&
                (string.IsNullOrEmpty(VesselName) ||
                 string.IsNullOrEmpty(velocity.VesselName) ||
                 string.Equals(
                     VesselName,
                     velocity.VesselName,
                     StringComparison.Ordinal));

            VelocityOrthogonalityDot =
                double.NaN;

            if (velocity != null &&
                velocity.OrbitalMagnitudeMetersPerSecond > 1.0 &&
                Magnitude > 0.5)
            {
                VelocityOrthogonalityDot =
                    (RightComponent *
                         velocity.OrbitalRightMetersPerSecond +
                     NoseComponent *
                         velocity.OrbitalNoseMetersPerSecond +
                     ReferenceForwardComponent *
                         velocity.OrbitalReferenceForwardMetersPerSecond) /
                    (Magnitude *
                     velocity.OrbitalMagnitudeMetersPerSecond);
            }

            bool magnitudeValid =
                Magnitude >= 0.95 &&
                Magnitude <= 1.05;

            bool orthogonal =
                !double.IsNaN(
                    VelocityOrthogonalityDot) &&
                !double.IsInfinity(
                    VelocityOrthogonalityDot) &&
                Math.Abs(
                    VelocityOrthogonalityDot) <=
                    0.02;

            Available =
                TelemetryAvailable &&
                Fresh &&
                VesselMatchesVelocityVector &&
                magnitudeValid &&
                orthogonal;

            if (!TelemetryAvailable)
            {
                Status =
                    "NO ORBIT NORMAL TELEMETRY";
            }
            else if (!Fresh)
            {
                Status =
                    "ORBIT NORMAL TELEMETRY STALE";
            }
            else if (!VesselMatchesVelocityVector)
            {
                Status =
                    "ORBIT NORMAL VESSEL MISMATCH";
            }
            else if (!magnitudeValid)
            {
                Status =
                    "ORBIT NORMAL MAGNITUDE INVALID";
            }
            else if (!orthogonal)
            {
                Status =
                    "ORBIT NORMAL NOT ORTHOGONAL";
            }
            else
            {
                Status =
                    "ORBIT NORMAL VERIFIED";
            }
        }

        internal static OrbitNormalTelemetryModel Clone(
            OrbitNormalTelemetryModel source)
        {
            if (source == null)
            {
                return
                    new OrbitNormalTelemetryModel();
            }

            return
                new OrbitNormalTelemetryModel
                {
                    TelemetryAvailable =
                        source.TelemetryAvailable,
                    SourceTimestampUtc =
                        source.SourceTimestampUtc,
                    ReceivedUtc =
                        source.ReceivedUtc,
                    VesselName =
                        source.VesselName,
                    RightComponent =
                        source.RightComponent,
                    NoseComponent =
                        source.NoseComponent,
                    ReferenceForwardComponent =
                        source.ReferenceForwardComponent,
                    Magnitude =
                        source.Magnitude,
                    VelocityOrthogonalityDot =
                        source.VelocityOrthogonalityDot,
                    Fresh =
                        source.Fresh,
                    VesselMatchesVelocityVector =
                        source.VesselMatchesVelocityVector,
                    Available =
                        source.Available,
                    Status =
                        source.Status
                };
        }
    }

    /// <summary>
    /// Process-local MissionControl-to-Engine bridge for KMC-NORM1.
    /// Socket ownership remains in MissionControl.
    /// </summary>
    /// <summary>
    /// Build 13.5 true radial-out direction resolved into the vessel
    /// ReferenceTransform frame. Components are a unit vector.
    /// </summary>
    public sealed class RadialTelemetryModel
    {
        public bool TelemetryAvailable { get; set; }
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselName { get; set; } = string.Empty;
        public double RightComponent { get; set; }
        public double NoseComponent { get; set; }
        public double ReferenceForwardComponent { get; set; }
        public double Magnitude { get; internal set; }
        public double NormalOrthogonalityDot { get; internal set; }
        public bool Fresh { get; internal set; }
        public bool VesselMatchesNormal { get; internal set; }
        public bool Available { get; internal set; }
        public string Status { get; internal set; } = "NO RADIAL TELEMETRY";

        internal void EvaluateAgainstNormal(
            OrbitNormalTelemetryModel normal,
            DateTime analysisUtc)
        {
            Magnitude = Math.Sqrt(
                RightComponent * RightComponent +
                NoseComponent * NoseComponent +
                ReferenceForwardComponent * ReferenceForwardComponent);

            Fresh = TelemetryAvailable &&
                Math.Abs((analysisUtc - ReceivedUtc).TotalSeconds) <= 0.75;

            VesselMatchesNormal = normal != null &&
                (string.IsNullOrEmpty(VesselName) ||
                 string.IsNullOrEmpty(normal.VesselName) ||
                 string.Equals(VesselName, normal.VesselName, StringComparison.Ordinal));

            NormalOrthogonalityDot = double.NaN;
            if (normal != null && normal.Magnitude > 0.5 && Magnitude > 0.5)
            {
                NormalOrthogonalityDot =
                    (RightComponent * normal.RightComponent +
                     NoseComponent * normal.NoseComponent +
                     ReferenceForwardComponent * normal.ReferenceForwardComponent) /
                    (Magnitude * normal.Magnitude);
            }

            bool magnitudeValid = Magnitude >= 0.95 && Magnitude <= 1.05;
            bool orthogonal = !double.IsNaN(NormalOrthogonalityDot) &&
                !double.IsInfinity(NormalOrthogonalityDot) &&
                Math.Abs(NormalOrthogonalityDot) <= 0.02;

            Available = TelemetryAvailable && Fresh && VesselMatchesNormal &&
                magnitudeValid && orthogonal;

            if (!TelemetryAvailable) Status = "NO RADIAL TELEMETRY";
            else if (!Fresh) Status = "RADIAL TELEMETRY STALE";
            else if (!VesselMatchesNormal) Status = "RADIAL VESSEL MISMATCH";
            else if (!magnitudeValid) Status = "RADIAL MAGNITUDE INVALID";
            else if (!orthogonal) Status = "RADIAL NOT ORTHOGONAL";
            else Status = "RADIAL VERIFIED";
        }

        internal static RadialTelemetryModel Clone(RadialTelemetryModel source)
        {
            if (source == null) return new RadialTelemetryModel();
            return new RadialTelemetryModel
            {
                TelemetryAvailable = source.TelemetryAvailable,
                SourceTimestampUtc = source.SourceTimestampUtc,
                ReceivedUtc = source.ReceivedUtc,
                VesselName = source.VesselName,
                RightComponent = source.RightComponent,
                NoseComponent = source.NoseComponent,
                ReferenceForwardComponent = source.ReferenceForwardComponent,
                Magnitude = source.Magnitude,
                NormalOrthogonalityDot = source.NormalOrthogonalityDot,
                Fresh = source.Fresh,
                VesselMatchesNormal = source.VesselMatchesNormal,
                Available = source.Available,
                Status = source.Status
            };
        }
    }

    public static class RadialTelemetryStore
    {
        private static readonly object SyncRoot = new object();
        private static RadialTelemetryModel _latest = new RadialTelemetryModel();

        public static void Publish(RadialTelemetryModel sample)
        {
            lock (SyncRoot) _latest = RadialTelemetryModel.Clone(sample);
        }

        public static RadialTelemetryModel GetLatest()
        {
            lock (SyncRoot) return RadialTelemetryModel.Clone(_latest);
        }

        public static void Clear()
        {
            lock (SyncRoot) _latest = new RadialTelemetryModel();
        }
    }

    public static class OrbitNormalTelemetryStore
    {
        private static readonly object SyncRoot =
            new object();

        private static OrbitNormalTelemetryModel _latest =
            new OrbitNormalTelemetryModel();

        public static void Publish(
            OrbitNormalTelemetryModel sample)
        {
            lock (SyncRoot)
            {
                _latest =
                    OrbitNormalTelemetryModel.Clone(
                        sample);
            }
        }

        public static OrbitNormalTelemetryModel GetLatest()
        {
            lock (SyncRoot)
            {
                return
                    OrbitNormalTelemetryModel.Clone(
                        _latest);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new OrbitNormalTelemetryModel();
            }
        }
    }

    /// <summary>
    /// 14.22.70b KSP-authoritative maneuver-node burn direction.
    /// Components are a unit vector resolved in vessel ReferenceTransform.
    /// </summary>
    public sealed class ManeuverVectorTelemetryModel
    {
        public bool TelemetryAvailable { get; set; }
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselId { get; set; } = string.Empty;
        public double NodeUniversalTimeSeconds { get; set; } = double.NaN;
        public double RightComponent { get; set; }
        public double NoseComponent { get; set; }
        public double ReferenceForwardComponent { get; set; }
        public double BurnMagnitudeMetersPerSecond { get; set; } = double.NaN;

        public bool IsUsableFor(
            string vesselId,
            double nodeUt,
            DateTime analysisUtc)
        {
            if (!TelemetryAvailable ||
                ReceivedUtc == DateTime.MinValue ||
                Math.Abs((analysisUtc - ReceivedUtc).TotalSeconds) > 0.75 ||
                string.IsNullOrWhiteSpace(vesselId) ||
                !string.Equals(
                    VesselId ?? string.Empty,
                    vesselId ?? string.Empty,
                    StringComparison.Ordinal) ||
                double.IsNaN(nodeUt) ||
                double.IsInfinity(nodeUt) ||
                double.IsNaN(NodeUniversalTimeSeconds) ||
                double.IsInfinity(NodeUniversalTimeSeconds) ||
                Math.Abs(NodeUniversalTimeSeconds - nodeUt) > 0.25)
            {
                return false;
            }

            double magnitude =
                Math.Sqrt(
                    RightComponent * RightComponent +
                    NoseComponent * NoseComponent +
                    ReferenceForwardComponent *
                    ReferenceForwardComponent);

            return
                magnitude >= 0.95 &&
                magnitude <= 1.05;
        }

        internal static ManeuverVectorTelemetryModel Clone(
            ManeuverVectorTelemetryModel source)
        {
            if (source == null)
            {
                return new ManeuverVectorTelemetryModel();
            }

            return new ManeuverVectorTelemetryModel
            {
                TelemetryAvailable = source.TelemetryAvailable,
                SourceTimestampUtc = source.SourceTimestampUtc,
                ReceivedUtc = source.ReceivedUtc,
                VesselId = source.VesselId,
                NodeUniversalTimeSeconds = source.NodeUniversalTimeSeconds,
                RightComponent = source.RightComponent,
                NoseComponent = source.NoseComponent,
                ReferenceForwardComponent = source.ReferenceForwardComponent,
                BurnMagnitudeMetersPerSecond =
                    source.BurnMagnitudeMetersPerSecond
            };
        }
    }

    public static class ManeuverVectorTelemetryStore
    {
        private const double NodeUtMatchToleranceSeconds = 0.25;
        private const int MaxSamples = 16;

        private static readonly object SyncRoot =
            new object();

        private static readonly List<ManeuverVectorTelemetryModel> Samples =
            new List<ManeuverVectorTelemetryModel>();

        private static ManeuverVectorTelemetryModel _latest =
            new ManeuverVectorTelemetryModel();

        public static void Publish(
            ManeuverVectorTelemetryModel sample)
        {
            if (sample == null)
            {
                return;
            }

            ManeuverVectorTelemetryModel clone =
                ManeuverVectorTelemetryModel.Clone(
                    sample);

            lock (SyncRoot)
            {
                _latest =
                    ManeuverVectorTelemetryModel.Clone(
                        clone);

                int replaceIndex = -1;

                for (int index = 0;
                     index < Samples.Count;
                     index++)
                {
                    ManeuverVectorTelemetryModel existing =
                        Samples[index];

                    if (string.Equals(
                            existing.VesselId ?? string.Empty,
                            clone.VesselId ?? string.Empty,
                            StringComparison.Ordinal) &&
                        !double.IsNaN(
                            existing.NodeUniversalTimeSeconds) &&
                        !double.IsNaN(
                            clone.NodeUniversalTimeSeconds) &&
                        Math.Abs(
                            existing.NodeUniversalTimeSeconds -
                            clone.NodeUniversalTimeSeconds) <=
                            NodeUtMatchToleranceSeconds)
                    {
                        replaceIndex = index;
                        break;
                    }
                }

                if (replaceIndex >= 0)
                {
                    Samples[replaceIndex] = clone;
                }
                else
                {
                    Samples.Add(clone);
                }

                Samples.Sort(
                    delegate(
                        ManeuverVectorTelemetryModel left,
                        ManeuverVectorTelemetryModel right)
                    {
                        return
                            right.ReceivedUtc.CompareTo(
                                left.ReceivedUtc);
                    });

                while (Samples.Count > MaxSamples)
                {
                    Samples.RemoveAt(
                        Samples.Count - 1);
                }
            }
        }

        public static ManeuverVectorTelemetryModel FindForPlan(
            string vesselId,
            double nodeUniversalTimeSeconds,
            DateTime analysisUtc)
        {
            lock (SyncRoot)
            {
                for (int index = 0;
                     index < Samples.Count;
                     index++)
                {
                    ManeuverVectorTelemetryModel sample =
                        Samples[index];

                    if (sample != null &&
                        sample.IsUsableFor(
                            vesselId,
                            nodeUniversalTimeSeconds,
                            analysisUtc))
                    {
                        return
                            ManeuverVectorTelemetryModel.Clone(
                                sample);
                    }
                }
            }

            return
                new ManeuverVectorTelemetryModel();
        }

        public static ManeuverVectorTelemetryModel GetLatest()
        {
            lock (SyncRoot)
            {
                return
                    ManeuverVectorTelemetryModel.Clone(
                        _latest);
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverVectorTelemetryModel();

                Samples.Clear();
            }
        }
    }
}
