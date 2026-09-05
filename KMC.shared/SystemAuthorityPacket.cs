using System;

namespace KMC.Shared
{
    public enum SystemAuthorityKind
    {
        Sas = 0,
        Gear = 1,
        Brakes = 2,
        Lights = 3,
        ReactionWheels = 4
    }

    public enum SystemAuthorityOperation
    {
        Inhibit = 0,
        Restore = 1
    }

    /// <summary>
    /// KMC Build 14.19.1
    ///
    /// Vessel-wide system authority lease from Mission Control to KSP.
    /// INHIBIT is a short lease. If Mission Control stops refreshing, the
    /// plugin restores the affected KSP modules automatically.
    /// </summary>
    public sealed class SystemAuthorityPacket
    {
        public const string ProtocolId =
            "KMC-SYSAUTH1";

        public const int CommandPort =
            5109;

        public SystemAuthorityPacket()
        {
            VesselId = string.Empty;
            CommandId = string.Empty;
            Authority =
                SystemAuthorityKind.Sas;
            Operation =
                SystemAuthorityOperation.Restore;
        }

        public string VesselId { get; set; }
        public string CommandId { get; set; }
        public SystemAuthorityKind Authority { get; set; }
        public SystemAuthorityOperation Operation { get; set; }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ProtocolId,
                    Uri.EscapeDataString(
                        VesselId ?? string.Empty),
                    Uri.EscapeDataString(
                        CommandId ?? string.Empty),
                    Authority.ToString(),
                    Operation.ToString()
                });
        }

        public static bool TryParse(
            string message,
            out SystemAuthorityPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string[] fields =
                message.Split('|');

            SystemAuthorityKind authority;
            SystemAuthorityOperation operation;

            if (fields.Length != 5 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal) ||
                !Enum.TryParse(
                    fields[3],
                    true,
                    out authority) ||
                !Enum.TryParse(
                    fields[4],
                    true,
                    out operation))
            {
                return false;
            }

            packet =
                new SystemAuthorityPacket
                {
                    VesselId =
                        Uri.UnescapeDataString(fields[1]),
                    CommandId =
                        Uri.UnescapeDataString(fields[2]),
                    Authority = authority,
                    Operation = operation
                };

            return
                !string.IsNullOrWhiteSpace(
                    packet.VesselId) &&
                !string.IsNullOrWhiteSpace(
                    packet.CommandId);
        }
    }
}
