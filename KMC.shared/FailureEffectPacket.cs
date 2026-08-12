using System;
using System.Globalization;

namespace KMC.Shared
{
    public enum FailureEffectType
    {
        EngineDerate = 0,
        EngineShutdown = 1,
        ReactionWheelAuthority = 2,
        ElectricChargeDrain = 3,
        ElectricChargeLeak = 4
    }

    public enum FailureEffectOperation
    {
        Apply = 0,
        Restore = 1,
        Pulse = 2
    }

    /// <summary>
    /// Build 14.4 Mission Control / instructor to KSP real-effect command.
    ///
    /// Every command carries the active vessel identity, a unique CommandId,
    /// an optional exact Part persistent ID, a bounded effect type/operation,
    /// and one numeric magnitude.
    /// </summary>
    public sealed class FailureEffectPacket
    {
        public const string ProtocolId = "KMC-FAILFX1";
        public const string AckProtocolId = "KMC-FAILFX1-ACK";
        public const int CommandPort = 5104;
        public const int AckPort = 5105;

        public FailureEffectPacket()
        {
            VesselId = string.Empty;
            CommandId = string.Empty;
            PartPersistentId = 0;
            EffectType = FailureEffectType.EngineDerate;
            Operation = FailureEffectOperation.Apply;
            Magnitude = 1.0;
        }

        public string VesselId { get; set; }
        public string CommandId { get; set; }
        public uint PartPersistentId { get; set; }
        public FailureEffectType EffectType { get; set; }
        public FailureEffectOperation Operation { get; set; }
        public double Magnitude { get; set; }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(CommandId ?? string.Empty),
                    PartPersistentId.ToString(CultureInfo.InvariantCulture),
                    EffectType.ToString(),
                    Operation.ToString(),
                    Magnitude.ToString("R", CultureInfo.InvariantCulture)
                });
        }

        public static bool TryParse(
            string message,
            out FailureEffectPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields = message.Split('|');

            if (fields.Length != 7 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            uint partId;
            FailureEffectType effectType;
            FailureEffectOperation operation;
            double magnitude;

            if (!uint.TryParse(
                    fields[3],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out partId) ||
                !Enum.TryParse(
                    fields[4],
                    true,
                    out effectType) ||
                !Enum.TryParse(
                    fields[5],
                    true,
                    out operation) ||
                !double.TryParse(
                    fields[6],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out magnitude) ||
                double.IsNaN(magnitude) ||
                double.IsInfinity(magnitude))
            {
                return false;
            }

            packet =
                new FailureEffectPacket
                {
                    VesselId =
                        Uri.UnescapeDataString(fields[1]),
                    CommandId =
                        Uri.UnescapeDataString(fields[2]),
                    PartPersistentId =
                        partId,
                    EffectType =
                        effectType,
                    Operation =
                        operation,
                    Magnitude =
                        magnitude
                };

            return
                !string.IsNullOrWhiteSpace(packet.VesselId) &&
                !string.IsNullOrWhiteSpace(packet.CommandId);
        }
    }

    public sealed class FailureEffectAck
    {
        public FailureEffectAck()
        {
            VesselId = string.Empty;
            CommandId = string.Empty;
            Status = string.Empty;
            Detail = string.Empty;
            EffectType = FailureEffectType.EngineDerate;
            PartPersistentId = 0;
            ObservedValue = double.NaN;
        }

        public string VesselId { get; set; }
        public string CommandId { get; set; }
        public string Status { get; set; }
        public FailureEffectType EffectType { get; set; }
        public uint PartPersistentId { get; set; }
        public double ObservedValue { get; set; }
        public string Detail { get; set; }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    FailureEffectPacket.AckProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(CommandId ?? string.Empty),
                    Uri.EscapeDataString(Status ?? string.Empty),
                    EffectType.ToString(),
                    PartPersistentId.ToString(CultureInfo.InvariantCulture),
                    FormatOptional(ObservedValue),
                    Uri.EscapeDataString(Detail ?? string.Empty)
                });
        }

        public static bool TryParse(
            string message,
            out FailureEffectAck ack)
        {
            ack = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields = message.Split('|');

            if (fields.Length != 8 ||
                !string.Equals(
                    fields[0],
                    FailureEffectPacket.AckProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            FailureEffectType effectType;
            uint partId;
            double observed;

            if (!Enum.TryParse(
                    fields[4],
                    true,
                    out effectType) ||
                !uint.TryParse(
                    fields[5],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out partId) ||
                !TryOptionalDouble(
                    fields[6],
                    out observed))
            {
                return false;
            }

            ack =
                new FailureEffectAck
                {
                    VesselId =
                        Uri.UnescapeDataString(fields[1]),
                    CommandId =
                        Uri.UnescapeDataString(fields[2]),
                    Status =
                        Uri.UnescapeDataString(fields[3]),
                    EffectType =
                        effectType,
                    PartPersistentId =
                        partId,
                    ObservedValue =
                        observed,
                    Detail =
                        Uri.UnescapeDataString(fields[7])
                };

            return
                !string.IsNullOrWhiteSpace(ack.VesselId) &&
                !string.IsNullOrWhiteSpace(ack.CommandId);
        }

        private static string FormatOptional(double value)
        {
            return
                double.IsNaN(value) ||
                double.IsInfinity(value)
                    ? "N/A"
                    : value.ToString(
                        "R",
                        CultureInfo.InvariantCulture);
        }

        private static bool TryOptionalDouble(
            string value,
            out double result)
        {
            if (string.Equals(
                    value,
                    "N/A",
                    StringComparison.OrdinalIgnoreCase))
            {
                result = double.NaN;
                return true;
            }

            if (!double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out result))
            {
                return false;
            }

            return !double.IsInfinity(result);
        }
    }
}
