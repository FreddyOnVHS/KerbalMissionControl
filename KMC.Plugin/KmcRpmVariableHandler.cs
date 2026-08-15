using System;
using System.Globalization;
using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// Build 14.15.1 RasterPropMonitor external-variable handler.
    ///
    /// RPM discovers this ordinary KSP PartModule through an
    /// RPMCVARIABLEHANDLER config node. KMC therefore has no compile-time
    /// dependency on RasterPropMonitor.dll.
    ///
    /// This build is strictly read-only. No IVA control method exists here.
    /// </summary>
    public sealed class KmcRpmVariableHandler :
        PartModule
    {
        public object ProcessVariable(
            string variableName)
        {
            KmcMfdStatusPacket status;

            bool available =
                TryGetStatus(
                    out status);

            switch (variableName)
            {
                case "KMC_AVAILABLE":
                    return
                        available
                            ? 1.0
                            : 0.0;

                case "KMC_LINK":
                    return
                        available
                            ? "ONLINE"
                            : "NO KMC LINK";

                case "KMC_MAIN_A_V":
                    return
                        available
                            ? FormatVoltage(
                                status.MainAVoltage)
                            : "--";

                case "KMC_MAIN_A_STATE":
                    return
                        available
                            ? SafeText(
                                status.MainAState)
                            : "--";

                case "KMC_MAIN_A_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.MainASource)
                            : "--";

                case "KMC_MAIN_B_V":
                    return
                        available
                            ? FormatVoltage(
                                status.MainBVoltage)
                            : "--";

                case "KMC_MAIN_B_STATE":
                    return
                        available
                            ? SafeText(
                                status.MainBState)
                            : "--";

                case "KMC_MAIN_B_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.MainBSource)
                            : "--";

                case "KMC_ESS_V":
                    return
                        available
                            ? FormatVoltage(
                                status.EssentialVoltage)
                            : "--";

                case "KMC_ESS_STATE":
                    return
                        available
                            ? SafeText(
                                status.EssentialState)
                            : "--";

                case "KMC_ESS_SOURCE":
                    return
                        available
                            ? SafeText(
                                status.EssentialSource)
                            : "--";

                case "KMC_BAT_A_STATE":
                    return
                        available
                            ? SafeText(
                                status.BatteryAState)
                            : "--";

                case "KMC_BAT_B_STATE":
                    return
                        available
                            ? SafeText(
                                status.BatteryBState)
                            : "--";

                default:
                    return null;
            }
        }

        private bool TryGetStatus(
            out KmcMfdStatusPacket status)
        {
            status = null;

            if (part == null ||
                part.vessel == null)
            {
                return false;
            }

            return
                KmcMfdStatusReceiver.TryGetStatus(
                    part.vessel.id.ToString(),
                    out status);
        }

        private static string FormatVoltage(
            double voltage)
        {
            return
                voltage.ToString(
                    "0.0",
                    CultureInfo.InvariantCulture) +
                " V";
        }

        private static string SafeText(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
                    ? "--"
                    : value.Trim()
                        .ToUpperInvariant();
        }
    }
}
