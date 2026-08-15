using System;

namespace KMC.MissionControl.Training
{
    public enum InstructorFailurePreset
    {
        PowerEcLeak = 0,
        CommA = 1,
        CommB = 2,
        GuidA = 3,
        GuidB = 4,
        PumpA = 5,
        PumpB = 6,
        EngineDerate50 = 7,
        EngineShutdown = 8,
        ReactionWheel25 = 9,
        GeneratorA = 10,
        GeneratorB = 11,
        GenAContactorFailedOpen = 12,
        MainATransferFailedOpen = 13,
        GuidABreakerTripped = 14,
        GenAContactorFalseOpenIndication = 15,
        GenAContactorWeldedClosed = 16,
        GeneratorADegraded50 = 17,
        EngineFeedValveClosed = 18,
        EngineFeedRestriction = 19,
        EngineFeedIntermittent = 20,
        EngineStartInhibit = 21,
        EngineThrustDecay = 22,
        EngineThrustUnstable = 23,
        EngineThrustIndicationFailLow = 24,
        MainBusAFailed = 25,
        MainBusBFailed = 26
    }

    public enum InstructorScenarioPreset
    {
        ASideSystemsCascade = 0
    }

    public static class InstructorTrainingText
    {
        public static string GetFailurePresetName(
            InstructorFailurePreset preset)
        {
            switch (preset)
            {
                case InstructorFailurePreset.PowerEcLeak:
                    return "POWER - EC LEAK 8.0 EC/S";

                case InstructorFailurePreset.GeneratorA:
                    return "POWER - GENERATOR A FAILED";

                case InstructorFailurePreset.GeneratorB:
                    return "POWER - GENERATOR B FAILED";

                case InstructorFailurePreset.GeneratorADegraded50:
                    return "POWER - GENERATOR A DEGRADED 50%";

                case InstructorFailurePreset.GenAContactorFailedOpen:
                    return "POWER - GEN A CONTACTOR FAILED OPEN";

                case InstructorFailurePreset.MainATransferFailedOpen:
                    return "POWER - MAIN A TRANSFER FAILED OPEN";

                case InstructorFailurePreset.MainBusAFailed:
                    return "POWER - MAIN BUS A FAILED";

                case InstructorFailurePreset.MainBusBFailed:
                    return "POWER - MAIN BUS B FAILED";

                case InstructorFailurePreset.GuidABreakerTripped:
                    return "POWER - GUID A BREAKER TRIPPED";

                case InstructorFailurePreset.GenAContactorFalseOpenIndication:
                    return "POWER - GEN A CONTACTOR FALSE OPEN IND";

                case InstructorFailurePreset.GenAContactorWeldedClosed:
                    return "POWER - GEN A CONTACTOR WELDED CLOSED";

                case InstructorFailurePreset.CommA:
                    return "COMM - TRANSCEIVER A FAILED";

                case InstructorFailurePreset.CommB:
                    return "COMM - TRANSCEIVER B FAILED";

                case InstructorFailurePreset.GuidA:
                    return "GNC - GUID COMPUTER A FAILED";

                case InstructorFailurePreset.GuidB:
                    return "GNC - GUID COMPUTER B FAILED";

                case InstructorFailurePreset.PumpA:
                    return "PROP - FEED PUMP A FAILED";

                case InstructorFailurePreset.PumpB:
                    return "PROP - FEED PUMP B FAILED";

                case InstructorFailurePreset.EngineDerate50:
                    return "PROP - EXACT ENGINE 50% DERATE";

                case InstructorFailurePreset.EngineShutdown:
                    return "PROP - EXACT ENGINE SHUTDOWN";

                case InstructorFailurePreset.EngineFeedValveClosed:
                    return "PROP - EXACT ENGINE FEED VALVE CLOSED";

                case InstructorFailurePreset.EngineFeedRestriction:
                    return "PROP - EXACT ENGINE FEED RESTRICTION";

                case InstructorFailurePreset.EngineFeedIntermittent:
                    return "PROP - EXACT ENGINE INTERMITTENT FEED";

                case InstructorFailurePreset.EngineStartInhibit:
                    return "PROP - EXACT ENGINE START INHIBIT";

                case InstructorFailurePreset.EngineThrustDecay:
                    return "PROP - EXACT ENGINE THRUST DECAY";

                case InstructorFailurePreset.EngineThrustUnstable:
                    return "PROP - EXACT ENGINE UNSTABLE THRUST";

                case InstructorFailurePreset.EngineThrustIndicationFailLow:
                    return "PROP - EXACT ENGINE THRUST IND FAIL LOW";

                case InstructorFailurePreset.ReactionWheel25:
                    return "GNC - REACTION WHEEL 25% AUTHORITY";

                default:
                    return preset.ToString();
            }
        }

        public static string GetScenarioName(
            InstructorScenarioPreset preset)
        {
            switch (preset)
            {
                case InstructorScenarioPreset.ASideSystemsCascade:
                    return "A-SIDE SYSTEMS CASCADE";

                default:
                    return preset.ToString();
            }
        }
    }
}
