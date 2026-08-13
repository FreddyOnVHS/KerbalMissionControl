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
        GeneratorB = 11
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
