using System;
using System.Globalization;

namespace KMC.Shared
{
    /// <summary>
    /// Dedicated Build 11.2 epoch side channel.
    /// Carries genuine KSP Universal Time and the active vessel id without
    /// changing the frozen KMC6 flight-telemetry packet.
    /// </summary>
    public sealed class ManeuverEpochPacket
    {
        public const string ProtocolId = "KMC-EPOCH1";
        public const int TelemetryPort = 5094;

        public DateTime TimestampUtc { get; set; }
        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public double UniversalTimeSeconds { get; set; }
        public double MissionTimeSeconds { get; set; }

        public ManeuverEpochPacket()
        {
            TimestampUtc = DateTime.UtcNow;
            VesselId = string.Empty;
            VesselName = string.Empty;
        }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ProtocolId,
                    TimestampUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(VesselName ?? string.Empty),
                    UniversalTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    MissionTimeSeconds.ToString("R", CultureInfo.InvariantCulture)
                });
        }

        public static bool TryParse(
            string message,
            out ManeuverEpochPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields = message.Split('|');

            if (fields.Length != 6 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal))
            {
                return false;
            }

            long ticks;
            double universalTime;
            double missionTime;

            if (!long.TryParse(
                    fields[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out ticks) ||
                !double.TryParse(
                    fields[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out universalTime) ||
                !double.TryParse(
                    fields[5],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out missionTime))
            {
                return false;
            }

            if (!IsFinite(universalTime) ||
                !IsFinite(missionTime))
            {
                return false;
            }

            DateTime timestampUtc;

            try
            {
                timestampUtc =
                    new DateTime(
                        ticks,
                        DateTimeKind.Utc);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            packet =
                new ManeuverEpochPacket
                {
                    TimestampUtc = timestampUtc,
                    VesselId = Uri.UnescapeDataString(fields[2]),
                    VesselName = Uri.UnescapeDataString(fields[3]),
                    UniversalTimeSeconds = universalTime,
                    MissionTimeSeconds = missionTime
                };

            return true;
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
