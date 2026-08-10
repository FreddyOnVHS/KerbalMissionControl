using System;

namespace KMC.Engine.Maneuver
{
    /// <summary>
    /// Latest genuine KSP maneuver epoch telemetry.
    /// This is intentionally separate from frozen ORBIT v1 telemetry.
    /// </summary>
    public sealed class ManeuverEpochTelemetryModel
    {
        public ManeuverEpochTelemetryModel()
        {
            VesselId = string.Empty;
            VesselName = string.Empty;
            UniversalTimeSeconds = double.NaN;
            MissionTimeSeconds = double.NaN;
        }

        public bool Available { get; set; }
        public DateTime SourceTimestampUtc { get; set; }
        public DateTime ReceivedUtc { get; set; }
        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public double MissionTimeSeconds { get; set; }

        internal static ManeuverEpochTelemetryModel Clone(
            ManeuverEpochTelemetryModel source)
        {
            if (source == null)
            {
                return new ManeuverEpochTelemetryModel();
            }

            return new ManeuverEpochTelemetryModel
            {
                Available = source.Available,
                SourceTimestampUtc = source.SourceTimestampUtc,
                ReceivedUtc = source.ReceivedUtc,
                VesselId = source.VesselId ?? string.Empty,
                VesselName = source.VesselName ?? string.Empty,
                UniversalTimeSeconds = source.UniversalTimeSeconds,
                MissionTimeSeconds = source.MissionTimeSeconds
            };
        }
    }
}
