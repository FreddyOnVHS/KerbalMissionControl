using System;
using System.Globalization;

namespace KMC.Shared
{
    /// <summary>
    /// Mission Control to KSP maneuver-node uplink.
    /// 14.22.31 keeps KMC-MNV1 backward compatible while optionally carrying
    /// a destination body for KSP-authoritative transfer assessment.
    /// </summary>
    public sealed class ManeuverUplinkPacket
    {
        public const string ProtocolId = "KMC-MNV1";
        public const string AckProtocolId = "KMC-MNV1-ACK";
        public const string NodeStateProtocolId = "KMC-MNV1-STATE";
        public const int CommandPort = 5095;
        public const int AckPort = 5096;
        public const int NodeStatePort = 5097;

        public string VesselId { get; set; }
        public string PlanId { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public string TargetBodyName { get; set; }
        public string Operation { get; set; }

        public ManeuverUplinkPacket()
        {
            VesselId = string.Empty;
            PlanId = string.Empty;
            TargetBodyName = string.Empty;
            Operation = "CREATE";
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
                    Format(RadialDeltaVMetersPerSecond),
                    Uri.EscapeDataString(TargetBodyName ?? string.Empty),
                    Uri.EscapeDataString(string.IsNullOrWhiteSpace(Operation) ? "CREATE" : Operation)
                });
        }

        public static bool TryParse(string message, out ManeuverUplinkPacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(message)) return false;

            string[] fields = message.Split('|');
            if ((fields.Length != 7 && fields.Length != 8 && fields.Length != 9) ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal))
                return false;

            double nodeUt, prograde, normal, radial;
            if (!TryDouble(fields[3], out nodeUt) ||
                !TryDouble(fields[4], out prograde) ||
                !TryDouble(fields[5], out normal) ||
                !TryDouble(fields[6], out radial))
                return false;

            packet = new ManeuverUplinkPacket
            {
                VesselId = Uri.UnescapeDataString(fields[1]),
                PlanId = Uri.UnescapeDataString(fields[2]),
                NodeUniversalTimeSeconds = nodeUt,
                ProgradeDeltaVMetersPerSecond = prograde,
                NormalDeltaVMetersPerSecond = normal,
                RadialDeltaVMetersPerSecond = radial,
                TargetBodyName = fields.Length >= 8
                    ? Uri.UnescapeDataString(fields[7])
                    : string.Empty,
                Operation = fields.Length >= 9
                    ? Uri.UnescapeDataString(fields[8])
                    : "CREATE"
            };

            return !string.IsNullOrWhiteSpace(packet.VesselId) &&
                   !string.IsNullOrWhiteSpace(packet.PlanId);
        }

        private static string Format(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryDouble(string value, out double result)
        {
            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
                return false;

            return !double.IsNaN(result) && !double.IsInfinity(result);
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

        public static bool TryParse(string message, out ManeuverUplinkAck ack)
        {
            ack = null;
            if (string.IsNullOrWhiteSpace(message)) return false;

            string[] fields = message.Split('|');
            if (fields.Length != 6 ||
                !string.Equals(fields[0], ManeuverUplinkPacket.AckProtocolId, StringComparison.Ordinal))
                return false;

            double nodeUt;
            if (!double.TryParse(
                    fields[4],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out nodeUt))
                return false;

            ack = new ManeuverUplinkAck
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

    /// <summary>
    /// KSP-to-Mission-Control maneuver node synchronization.
    /// 14.22.31 appends optional KSP-authoritative transfer-assessment fields.
    /// Older 10-field packets remain accepted.
    /// </summary>
    public sealed class ManeuverNodeStatePacket
    {
        public string VesselId { get; set; }
        public string PlanId { get; set; }
        public string State { get; set; }
        public bool NodeExists { get; set; }
        public double NodeUniversalTimeSeconds { get; set; }
        public double ProgradeDeltaVMetersPerSecond { get; set; }
        public double NormalDeltaVMetersPerSecond { get; set; }
        public double RadialDeltaVMetersPerSecond { get; set; }
        public string Detail { get; set; }

        public string TargetBodyName { get; set; }
        public bool TransferAssessmentAvailable { get; set; }
        public bool TargetEncounter { get; set; }
        public double ClosestApproachMeters { get; set; }
        public double ClosestApproachUniversalTimeSeconds { get; set; }
        public double TargetSoiRadiusMeters { get; set; }

        public ManeuverNodeStatePacket()
        {
            VesselId = string.Empty;
            PlanId = string.Empty;
            State = string.Empty;
            Detail = string.Empty;
            TargetBodyName = string.Empty;
            NodeUniversalTimeSeconds = double.NaN;
            ProgradeDeltaVMetersPerSecond = double.NaN;
            NormalDeltaVMetersPerSecond = double.NaN;
            RadialDeltaVMetersPerSecond = double.NaN;
            ClosestApproachMeters = double.NaN;
            ClosestApproachUniversalTimeSeconds = double.NaN;
            TargetSoiRadiusMeters = double.NaN;
        }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ManeuverUplinkPacket.NodeStateProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(PlanId ?? string.Empty),
                    Uri.EscapeDataString(State ?? string.Empty),
                    NodeExists ? "1" : "0",
                    FormatOptional(NodeUniversalTimeSeconds),
                    FormatOptional(ProgradeDeltaVMetersPerSecond),
                    FormatOptional(NormalDeltaVMetersPerSecond),
                    FormatOptional(RadialDeltaVMetersPerSecond),
                    Uri.EscapeDataString(Detail ?? string.Empty),
                    Uri.EscapeDataString(TargetBodyName ?? string.Empty),
                    TransferAssessmentAvailable ? "1" : "0",
                    TargetEncounter ? "1" : "0",
                    FormatOptional(ClosestApproachMeters),
                    FormatOptional(ClosestApproachUniversalTimeSeconds),
                    FormatOptional(TargetSoiRadiusMeters)
                });
        }

        public static bool TryParse(string message, out ManeuverNodeStatePacket packet)
        {
            packet = null;
            if (string.IsNullOrWhiteSpace(message)) return false;

            string[] fields = message.Split('|');
            if ((fields.Length != 10 && fields.Length != 16) ||
                !string.Equals(fields[0], ManeuverUplinkPacket.NodeStateProtocolId, StringComparison.Ordinal))
                return false;

            double nodeUt, prograde, normal, radial;
            if (!TryOptionalDouble(fields[5], out nodeUt) ||
                !TryOptionalDouble(fields[6], out prograde) ||
                !TryOptionalDouble(fields[7], out normal) ||
                !TryOptionalDouble(fields[8], out radial))
                return false;

            ManeuverNodeStatePacket value = new ManeuverNodeStatePacket
            {
                VesselId = Uri.UnescapeDataString(fields[1]),
                PlanId = Uri.UnescapeDataString(fields[2]),
                State = Uri.UnescapeDataString(fields[3]),
                NodeExists = fields[4] == "1",
                NodeUniversalTimeSeconds = nodeUt,
                ProgradeDeltaVMetersPerSecond = prograde,
                NormalDeltaVMetersPerSecond = normal,
                RadialDeltaVMetersPerSecond = radial,
                Detail = Uri.UnescapeDataString(fields[9])
            };

            if (fields.Length == 16)
            {
                double closest, closestUt, soi;
                if (!TryOptionalDouble(fields[13], out closest) ||
                    !TryOptionalDouble(fields[14], out closestUt) ||
                    !TryOptionalDouble(fields[15], out soi))
                    return false;

                value.TargetBodyName = Uri.UnescapeDataString(fields[10]);
                value.TransferAssessmentAvailable = fields[11] == "1";
                value.TargetEncounter = fields[12] == "1";
                value.ClosestApproachMeters = closest;
                value.ClosestApproachUniversalTimeSeconds = closestUt;
                value.TargetSoiRadiusMeters = soi;
            }

            packet = value;
            return !string.IsNullOrWhiteSpace(packet.VesselId) &&
                   !string.IsNullOrWhiteSpace(packet.PlanId);
        }

        private static string FormatOptional(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? "N/A"
                : value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryOptionalDouble(string value, out double result)
        {
            if (string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                result = double.NaN;
                return true;
            }

            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
                return false;

            return !double.IsInfinity(result);
        }
    }
}
