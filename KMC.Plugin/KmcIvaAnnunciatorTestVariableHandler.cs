using KMC.Shared;

namespace KMC.Plugin
{
    /// <summary>
    /// RPM external-variable handler for explicit instructor IVA tests.
    /// The proven KmcRpmVariableHandler / KmcRpmPowerWake path is untouched.
    /// </summary>
    public sealed class KmcIvaAnnunciatorTestVariableHandler : PartModule
    {
        public object ProcessVariable(string variableName)
        {
            if (part == null || part.vessel == null)
                return 0.0;

            string vesselId = part.vessel.id.ToString();

            switch (variableName)
            {
                case "KMC_IVA_TEST_WARP":
                    return Active(vesselId, IvaAnnunciatorTestId.Warp);
                case "KMC_IVA_TEST_MECO":
                    return Active(vesselId, IvaAnnunciatorTestId.Meco);
                case "KMC_IVA_TEST_ENG_FAILURE":
                    return Active(vesselId, IvaAnnunciatorTestId.EngineFailure);
                case "KMC_IVA_TEST_ENG_OVERHEAT":
                    return Active(vesselId, IvaAnnunciatorTestId.EngineOverheat);
                default:
                    return null;
            }
        }

        private static double Active(
            string vesselId,
            IvaAnnunciatorTestId testId)
        {
            return KmcIvaAnnunciatorTestReceiver.IsActive(vesselId, testId)
                ? 1.0
                : 0.0;
        }
    }
}
