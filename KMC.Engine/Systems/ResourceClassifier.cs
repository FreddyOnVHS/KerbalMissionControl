using System;

namespace KMC.Engine.Capabilities
{
    internal static class ResourceClassifier
    {
        public static ResourceDescriptor Classify(
            string name)
        {
            string value =
                name ??
                string.Empty;

            ResourceDescriptor result =
                new ResourceDescriptor
                {
                    InternalName = value,
                    DisplayName = value,
                    Category = ResourceCategory.Unknown,
                    IsKnown = false
                };

            if (Equal(value, "ElectricCharge"))
            {
                Set(result, "Electric Charge", ResourceCategory.Electrical);
            }
            else if (Equal(value, "LiquidFuel"))
            {
                Set(result, "Liquid Fuel", ResourceCategory.Fuel);
            }
            else if (Equal(value, "Oxidizer"))
            {
                Set(result, "Oxidizer", ResourceCategory.Oxidizer);
            }
            else if (Equal(value, "MonoPropellant"))
            {
                Set(result, "Monopropellant", ResourceCategory.ReactionControl);
            }
            else if (Equal(value, "SolidFuel"))
            {
                Set(result, "Solid Fuel", ResourceCategory.SolidPropellant);
            }
            else if (Equal(value, "XenonGas"))
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

        private static bool Equal(
            string left,
            string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
