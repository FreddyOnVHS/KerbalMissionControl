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
                        VesselId =
                            vesselId,
                        EcPerSecond =
                            rate
                    };

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Build 14.15.1 Mission Control -> KSP read-only IVA/MFD status packet.
    ///
    /// This transports only final controller-observable KMC electrical state.
    /// It contains no hidden failure identity, failure mode, procedure, actual
    /// switch position, or conduction truth.
    /// </summary>
    public sealed class KmcMfdStatusPacket
    {
        public const string ProtocolId = "KMC-MFD1";
        public const int StatusPort = 5106;

        public KmcMfdStatusPacket()
        {
            VesselId = string.Empty;
            MainAState = string.Empty;
            MainASource = string.Empty;
            MainBState = string.Empty;
            MainBSource = string.Empty;
            EssentialState = string.Empty;
            EssentialSource = string.Empty;
            BatteryAState = string.Empty;
            BatteryBState = string.Empty;
        }

        public string VesselId { get; set; }

        public double MainAVoltage { get; set; }
        public string MainAState { get; set; }
        public string MainASource { get; set; }

        public double MainBVoltage { get; set; }
        public string MainBState { get; set; }
        public string MainBSource { get; set; }

        public double EssentialVoltage { get; set; }
        public string EssentialState { get; set; }
        public string EssentialSource { get; set; }

        public string BatteryAState { get; set; }
        public string BatteryBState { get; set; }

        public string Serialize()
        {
            return
                string.Join(
                    "|",
                    new[]
                    {
                        ProtocolId,
                        Escape(VesselId),
                        Format(MainAVoltage),
                        Escape(MainAState),
                        Escape(MainASource),
                        Format(MainBVoltage),
                        Escape(MainBState),
                        Escape(MainBSource),
                        Format(EssentialVoltage),
                        Escape(EssentialState),
                        Escape(EssentialSource),
                        Escape(BatteryAState),
                        Escape(BatteryBState)
                    });
        }

        public static bool TryParse(
            string text,
            out KmcMfdStatusPacket packet)
        {
            packet = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] fields =
                text.Split('|');

            if (fields.Length != 13 ||
                !string.Equals(
                    fields[0],
                    ProtocolId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                double mainAVoltage;
                double mainBVoltage;
                double essentialVoltage;

                if (!TryParseVoltage(fields[2], out mainAVoltage) ||
                    !TryParseVoltage(fields[5], out mainBVoltage) ||
                    !TryParseVoltage(fields[8], out essentialVoltage))
                {
                    return false;
                }

                string vesselId = Unescape(fields[1]);

                if (string.IsNullOrWhiteSpace(vesselId))
                {
                    return false;
                }

                packet =
                    new KmcMfdStatusPacket
                    {
                        VesselId = vesselId,

                        MainAVoltage = mainAVoltage,
                        MainAState = Unescape(fields[3]),
                        MainASource = Unescape(fields[4]),

                        MainBVoltage = mainBVoltage,
                        MainBState = Unescape(fields[6]),
                        MainBSource = Unescape(fields[7]),

                        EssentialVoltage = essentialVoltage,
                        EssentialState = Unescape(fields[9]),
                        EssentialSource = Unescape(fields[10]),

                        BatteryAState = Unescape(fields[11]),
                        BatteryBState = Unescape(fields[12])
                    };

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string Escape(
            string value)
        {
            return
                Uri.EscapeDataString(
                    value ?? string.Empty);
        }

        private static string Unescape(
            string value)
        {
            return
                Uri.UnescapeDataString(
                    value ?? string.Empty);
        }

        private static string Format(
            double value)
        {
            return
                value.ToString(
                    "R",
                    CultureInfo.InvariantCulture);
        }

        private static bool TryParseVoltage(
            string text,
            out double value)
        {
            value = 0.0;

            if (!double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value) ||
                double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0.0 ||
                value > 1000.0)
            {
                value = 0.0;
                return false;
            }

            return true;
        }
    }
}
