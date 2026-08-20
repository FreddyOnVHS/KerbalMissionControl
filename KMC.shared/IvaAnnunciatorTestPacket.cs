using System;

namespace KMC.Shared
{
    public enum IvaAnnunciatorTestId
    {
        Warp = 0,
        Meco = 1,
        EngineFailure = 2,
        EngineOverheat = 3,

        // Build 14.18.3 — append only; preserve existing wire values.
        LowTwr = 4,
        HighSlope = 5,
        GroundProximity = 6,
        LandingGear = 7
    }

    public enum IvaAnnunciatorTestOperation
    {
        On = 0,
        Off = 1,
        ClearAll = 2
    }

    public sealed class IvaAnnunciatorTestPacket
    {
        public const string ProtocolId = "KMC-IVATEST1";
        public const int CommandPort = 5107;

        public IvaAnnunciatorTestPacket()
        {
            VesselId = string.Empty;
            CommandId = string.Empty;
            TestId = IvaAnnunciatorTestId.Warp;
            Operation = IvaAnnunciatorTestOperation.Off;
        }

        public string VesselId { get; set; }
        public string CommandId { get; set; }
        public IvaAnnunciatorTestId TestId { get; set; }
        public IvaAnnunciatorTestOperation Operation { get; set; }

        public string Serialize()
        {
            return string.Join(
                "|",
                new[]
                {
                    ProtocolId,
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(CommandId ?? string.Empty),
                    TestId.ToString(),
                    Operation.ToString()
                });
        }

        public static bool TryParse(
            string message,
            out IvaAnnunciatorTestPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string[] fields = message.Split('|');

            if (fields.Length != 5 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal))
                return false;

            IvaAnnunciatorTestId testId;
            IvaAnnunciatorTestOperation operation;

            if (!Enum.TryParse(fields[3], true, out testId) ||
                !Enum.TryParse(fields[4], true, out operation))
                return false;

            try
            {
                packet = new IvaAnnunciatorTestPacket
                {
                    VesselId = Uri.UnescapeDataString(fields[1]),
                    CommandId = Uri.UnescapeDataString(fields[2]),
                    TestId = testId,
                    Operation = operation
                };
            }
            catch
            {
                packet = null;
                return false;
            }

            return
                !string.IsNullOrWhiteSpace(packet.VesselId) &&
                !string.IsNullOrWhiteSpace(packet.CommandId);
        }
    }

    // Build 14.18.7 — compiled through this existing linked shared file.
    public enum RcsAuthorityOperation
    {
        Inhibit = 0,
        Restore = 1
    }

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
                    Uri.EscapeDataString(VesselId ?? string.Empty),
                    Uri.EscapeDataString(CommandId ?? string.Empty),
                    Operation.ToString()
                });
        }

        public static bool TryParse(
            string message,
            out RcsAuthorityPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(message))
                return false;

            string[] fields = message.Split('|');
            RcsAuthorityOperation operation;

            if (fields.Length != 4 ||
                !string.Equals(fields[0], ProtocolId, StringComparison.Ordinal) ||
                !Enum.TryParse(fields[3], true, out operation))
                return false;

            try
            {
                packet = new RcsAuthorityPacket
                {
                    VesselId = Uri.UnescapeDataString(fields[1]),
                    CommandId = Uri.UnescapeDataString(fields[2]),
                    Operation = operation
                };
            }
            catch
            {
                packet = null;
                return false;
            }

            return
                !string.IsNullOrWhiteSpace(packet.VesselId) &&
                !string.IsNullOrWhiteSpace(packet.CommandId);
        }
    }
}
