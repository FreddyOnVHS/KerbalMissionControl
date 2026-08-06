using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    internal static class VesselModuleDiscoveryAnalyzer
    {
        private static readonly string[] ActiveMemberNames =
        {
            "IsActivated",
            "isActive",
            "IsActive",
            "isRunning",
            "IsRunning",
            "generatorIsActive",
            "ConverterIsActive",
            "isDeployed"
        };

        private static readonly string[] StatusMemberNames =
        {
            "status",
            "Status",
            "statusText",
            "StatusText"
        };

        private static readonly string[] InputMemberNames =
        {
            "inputList",
            "InputList",
            "inputResources",
            "InputResources",
            "Inputs"
        };

        private static readonly string[] OutputMemberNames =
        {
            "outputList",
            "OutputList",
            "outputResources",
            "OutputResources",
            "Outputs"
        };

        public static void AnalyzePart(
            Part part,
            VesselTopologyNode node)
        {
            if (part == null ||
                node == null ||
                part.Modules == null)
            {
                return;
            }

            for (int index = 0;
                 index < part.Modules.Count;
                 index++)
            {
                PartModule module =
                    part.Modules[index];

                if (module == null)
                {
                    continue;
                }

                try
                {
                    node.Modules.Add(
                        CreateDescriptor(module));
                }
                catch
                {
                    VesselModuleDescriptor fallback =
                        new VesselModuleDescriptor
                        {
                            ModuleName =
                                GetModuleName(module),
                            ModuleTypeName =
                                module.GetType().FullName ??
                                module.GetType().Name,
                            DisplayName =
                                module.GetType().Name,
                            IsEnabled =
                                SafeIsEnabled(module)
                        };

                    node.Modules.Add(fallback);
                }
            }
        }

        private static VesselModuleDescriptor CreateDescriptor(
            PartModule module)
        {
            Type type =
                module.GetType();

            VesselModuleDescriptor descriptor =
                new VesselModuleDescriptor
                {
                    ModuleName =
                        GetModuleName(module),
                    ModuleTypeName =
                        type.FullName ??
                        type.Name,
                    DisplayName =
                        type.Name,
                    IsEnabled =
                        SafeIsEnabled(module)
                };

            bool active;

            if (TryReadBoolean(
                    module,
                    ActiveMemberNames,
                    out active))
            {
                descriptor.HasActiveState =
                    true;

                descriptor.IsActive =
                    active;
            }

            string status;

            if (TryReadString(
                    module,
                    StatusMemberNames,
                    out status))
            {
                descriptor.StatusText =
                    status;
            }

            ReadResourceCollection(
                module,
                InputMemberNames,
                descriptor.InputResources);

            ReadResourceCollection(
                module,
                OutputMemberNames,
                descriptor.OutputResources);

            return descriptor;
        }

        private static string GetModuleName(
            PartModule module)
        {
            try
            {
                if (!string.IsNullOrEmpty(
                        module.moduleName))
                {
                    return module.moduleName;
                }
            }
            catch
            {
            }

            return module.GetType().Name;
        }

        private static bool SafeIsEnabled(
            PartModule module)
        {
            try
            {
                return module.isEnabled;
            }
            catch
            {
                return true;
            }
        }

        private static bool TryReadBoolean(
            object instance,
            IList<string> names,
            out bool value)
        {
            value = false;

            object raw;

            if (!TryReadMember(
                    instance,
                    names,
                    out raw) ||
                raw == null)
            {
                return false;
            }

            if (raw is bool)
            {
                value = (bool)raw;
                return true;
            }

            bool parsed;

            if (bool.TryParse(
                    raw.ToString(),
                    out parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        private static bool TryReadString(
            object instance,
            IList<string> names,
            out string value)
        {
            value = string.Empty;

            object raw;

            if (!TryReadMember(
                    instance,
                    names,
                    out raw) ||
                raw == null)
            {
                return false;
            }

            value =
                raw.ToString() ??
                string.Empty;

            return !string.IsNullOrEmpty(value);
        }

        private static void ReadResourceCollection(
            object module,
            IList<string> names,
            IList<VesselModuleResource> destination)
        {
            object raw;

            if (!TryReadMember(
                    module,
                    names,
                    out raw) ||
                raw == null)
            {
                return;
            }

            IEnumerable enumerable =
                raw as IEnumerable;

            if (enumerable == null)
            {
                return;
            }

            foreach (object entry in enumerable)
            {
                VesselModuleResource resource =
                    ReadResourceEntry(entry);

                if (resource == null ||
                    string.IsNullOrEmpty(resource.Name))
                {
                    continue;
                }

                destination.Add(resource);
            }
        }

        private static VesselModuleResource ReadResourceEntry(
            object entry)
        {
            if (entry == null)
            {
                return null;
            }

            string name;

            if (!TryReadString(
                    entry,
                    new[]
                    {
                        "ResourceName",
                        "resourceName",
                        "Name",
                        "name"
                    },
                    out name))
            {
                return null;
            }

            double ratio =
                0.0;

            object rawRatio;

            if (TryReadMember(
                    entry,
                    new[]
                    {
                        "Ratio",
                        "ratio",
                        "Rate",
                        "rate"
                    },
                    out rawRatio) &&
                rawRatio != null)
            {
                try
                {
                    ratio =
                        Convert.ToDouble(
                            rawRatio);
                }
                catch
                {
                    ratio =
                        0.0;
                }
            }

            return new VesselModuleResource
            {
                Name = name,
                Ratio = ratio
            };
        }

        private static bool TryReadMember(
            object instance,
            IList<string> names,
            out object value)
        {
            value = null;

            if (instance == null)
            {
                return false;
            }

            Type type =
                instance.GetType();

            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            for (int index = 0;
                 index < names.Count;
                 index++)
            {
                string name =
                    names[index];

                try
                {
                    PropertyInfo property =
                        type.GetProperty(
                            name,
                            flags);

                    if (property != null &&
                        property.GetIndexParameters().Length == 0)
                    {
                        value =
                            property.GetValue(
                                instance,
                                null);

                        return true;
                    }
                }
                catch
                {
                }

                try
                {
                    FieldInfo field =
                        type.GetField(
                            name,
                            flags);

                    if (field != null)
                    {
                        value =
                            field.GetValue(
                                instance);

                        return true;
                    }
                }
                catch
                {
                }
            }

            return false;
        }
    }
}
