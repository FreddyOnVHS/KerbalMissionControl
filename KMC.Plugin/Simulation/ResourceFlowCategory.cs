using System;

namespace KMC.Plugin.Simulation
{
    internal enum ResourceFlowCategory
    {
        Unknown,
        NoFlow,
        AllVessel,
        StagePriority,
        StackPriority
    }

    internal static class ResourceFlowCategoryParser
    {
        public static ResourceFlowCategory Parse(
            object flowMode)
        {
            string value =
                flowMode != null
                    ? flowMode.ToString()
                    : string.Empty;

            if (value.IndexOf(
                    "NO_FLOW",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourceFlowCategory.NoFlow;
            }

            if (value.IndexOf(
                    "ALL_VESSEL",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourceFlowCategory.AllVessel;
            }

            if (value.IndexOf(
                    "STAGE_PRIORITY",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourceFlowCategory.StagePriority;
            }

            if (value.IndexOf(
                    "STACK",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return ResourceFlowCategory.StackPriority;
            }

            return ResourceFlowCategory.Unknown;
        }
    }
}
