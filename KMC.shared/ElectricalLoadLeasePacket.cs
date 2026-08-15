using System;
using System.Globalization;

namespace KMC.Shared
{
    /// <summary>
    /// Build 14.13.4 Mission Control -> KSP lease for KMC-owned normal
    /// spacecraft electrical consumption.
    ///
    /// This protocol is intentionally separate from failure effects. The rate
    /// represents ordinary simulated avionics/systems load, not a failure leak.
    /// </summary>
    public sealed class ElectricalLoadLeasePacket
    {
        public const string ProtocolId = "KMC-LOAD1";
        public const int CommandPort = 5103;

        public ElectricalLoadLeasePacket()
        {
            VesselId = string.Empty;
        }

        public string VesselId { get; set; }
        public double EcPerSecond { get; set; }

        public string Serialize()
        {
            return
                string.Join(
                    "|",
                    new[]
                    {
                        ProtocolId,
                        Uri.EscapeDataString(
                            VesselId ?? string.Empty),
                        EcPerSecond.ToString(
                            "R",
                            CultureInfo.InvariantCulture)
                    });
        }

        public static bool TryParse(
            string text,
            out ElectricalLoadLeasePacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] fields =
                text.Split('|');

            if (fields.Length != 3 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                string vesselId =
                    Uri.UnescapeDataString(
                        fields[1] ?? string.Empty);

                double rate;

                if (string.IsNullOrWhiteSpace(vesselId) ||
                    !double.TryParse(
                        fields[2],
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out rate) ||
                    double.IsNaN(rate) ||
                    double.IsInfinity(rate) ||
                    rate < 0.0)
                {
                    return false;
                }

                packet =
                    new ElectricalLoadLeasePacket
                    {
                        VesselId = vesselId,
                        EcPerSecond = rate
                    };

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
