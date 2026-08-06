using System;

namespace KMC.MissionControl.Capabilities
{
    public static class ResourceClassifier
    {
        public static ResourceDescriptor Classify(string name)
        {
            string n = name ?? string.Empty;

            ResourceDescriptor result =
                new ResourceDescriptor
                {
                    InternalName = n,
                    DisplayName = n,
                    Category = ResourceCategory.Unknown,
                    IsKnown = false
                };

            if (Equal(n, "ElectricCharge"))
            {
                Set(result, "Electric Charge", ResourceCategory.Electrical);
            }
            else if (Equal(n, "LiquidFuel"))
            {
                Set(result, "Liquid Fuel", ResourceCategory.Fuel);
            }
            else if (Equal(n, "Oxidizer"))
            {
                Set(result, "Oxidizer", ResourceCategory.Oxidizer);
            }
            else if (Equal(n, "MonoPropellant"))
            {
                Set(result, "Monopropellant", ResourceCategory.ReactionControl);
            }
            else if (Equal(n, "SolidFuel"))
            {
                Set(result, "Solid Fuel", ResourceCategory.SolidPropellant);
            }
            else if (Equal(n, "XenonGas"))
            {
                Set(result, "Xenon Gas", ResourceCategory.NobleGas);
            }

            return result;
        }

        private static void Set(
            ResourceDescriptor descriptor,
            string displayName,
            ResourceCategory category)
        {
            descriptor.DisplayName = displayName;
            descriptor.Category = category;
            descriptor.IsKnown = true;
        }

        private static bool Equal(string left, string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
