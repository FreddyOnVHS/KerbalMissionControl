using System;

namespace KMC.Shared
{
    public enum RcsAuthorityOperation
    {
        Inhibit = 0,
        Restore = 1
    }

    /// <summary>
    /// KMC Build 14.18.7
    ///
    /// Vessel-wide RCS authority lease from Mission Control to the KSP plugin.
    /// The receiver treats INHIBIT as a short lease; if refreshes stop, RCS
    /// authority restores automatically.
    /// </summary>
    public sealed class RcsAuthorityPacket
    {
        public const string ProtocolId = "KMC-RCSAUTH1";
        public const int CommandPort = 5108;

        public RcsAuthorityPacket()
        {
            VesselId = string.Empty;
            CommandId = string.Empty;
            Operation = RcsAuthorityOperation.Restore;
        }

        public string VesselId { get; set; }
        public string CommandId { get; set; }
        public RcsAuthorityOperation Operation { get; set; }

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
                    Operation.ToString()
                });
        }

        public static bool TryParse(
            string message,
            out RcsAuthorityPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string[] fields =
                message.Split('|');

            RcsAuthorityOperation operation;

            if (fields.Length != 4 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal) ||
                !Enum.TryParse(
                    fields[3],
                    true,
                    out operation))
            {
                return false;
            }

            packet =
                new RcsAuthorityPacket
                {
                    VesselId =
                        Uri.UnescapeDataString(fields[1]),
                    CommandId =
                        Uri.UnescapeDataString(fields[2]),
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
