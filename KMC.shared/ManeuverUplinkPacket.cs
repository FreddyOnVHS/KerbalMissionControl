using System;
using System.Globalization;

namespace KMC.Shared
{
    /// <summary>
    /// Build 11.2 Mission Control to KSP maneuver-node uplink.
    /// Axis order is explicitly named in the protocol; the Plugin is solely
    /// responsible for converting these components to KSP's ManeuverNode vector.
    /// </summary>
    public sealed class ManeuverUplinkPacket
    {
        public const string ProtocolId = "KMC-MNV1";
        public const string AckProtocolId = "KMC-MNV1-ACK";
        public const int CommandPort = 5095;
        public const int AckPort = 5096;

        public string VesselId { get; set; }
        public string PlanId { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }

        public ManeuverUplinkPacket()
        {
            VesselId = string.Empty;
            PlanId = string.Empty;
        }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(PlanId ?? string.Empty),
                    Format(NodeUniversalTimeSeconds),
                    Format(ProgradeDeltaVMetersPerSecond),
                    Format(NormalDeltaVMetersPerSecond),
                    Format(RadialDeltaVMetersPerSecond)
                });
        }

        public static bool TryParse(
            string message,
            out ManeuverUplinkPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields = message.Split('|');

            if (fields.Length != 7 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal))
            {
                return false;
            }

            double nodeUt;
            double prograde;
            double normal;
            double radial;

            if (!TryDouble(fields[3], out nodeUt) ||
                !TryDouble(fields[4], out prograde) ||
                !TryDouble(fields[5], out normal) ||
                !TryDouble(fields[6], out radial))
            {
                return false;
            }

            packet =
                new ManeuverUplinkPacket
                {
                    VesselId = Uri.UnescapeDataString(fields[1]),
                    PlanId = Uri.UnescapeDataString(fields[2]),
                    NodeUniversalTimeSeconds = nodeUt,
                    ProgradeDeltaVMetersPerSecond = prograde,
                    NormalDeltaVMetersPerSecond = normal,
                    RadialDeltaVMetersPerSecond = radial
                };

            return
                !string.IsNullOrWhiteSpace(packet.VesselId) &&
                !string.IsNullOrWhiteSpace(packet.PlanId);
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryDouble(
            string value,
            out double result)
        {
            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
            {
                return false;
            }

            return
                !double.IsNaN(result) &&
                !double.IsInfinity(result);
        }
    }

    public sealed class ManeuverUplinkAck
    {
        public string VesselId { get; set; }
        public string PlanId { get; set; }
        public string Status { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public string Detail { get; set; }

        public ManeuverUplinkAck()
        {
            VesselId = string.Empty;
            PlanId = string.Empty;
            Status = string.Empty;
            Detail = string.Empty;
            NodeUniversalTimeSeconds = double.NaN;
        }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ManeuverUplinkPacket.AckProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(PlanId ?? string.Empty),
                    Uri.EscapeDataString(Status ?? string.Empty),
                    NodeUniversalTimeSeconds.ToString("R", CultureInfo.InvariantCulture),
                    Uri.EscapeDataString(Detail ?? string.Empty)
                });
        }

        public static bool TryParse(
            string message,
            out ManeuverUplinkAck ack)
        {
            ack = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields = message.Split('|');

            if (fields.Length != 6 ||
                !string.Equals(
                    fields[0],
                    ManeuverUplinkPacket.AckProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            double nodeUt;

            if (!double.TryParse(
                    fields[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out nodeUt))
            {
                return false;
            }

            ack =
                new ManeuverUplinkAck
                {
                    VesselId = Uri.UnescapeDataString(fields[1]),
                    PlanId = Uri.UnescapeDataString(fields[2]),
                    Status = Uri.UnescapeDataString(fields[3]),
                    NodeUniversalTimeSeconds = nodeUt,
                    Detail = Uri.UnescapeDataString(fields[5])
                };

            return true;
        }
    }
}
