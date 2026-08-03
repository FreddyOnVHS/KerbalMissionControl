using System;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Classifies KSP parts by modules and resources rather than stock part
    /// names. This allows most modded parts using standard KSP modules to be
    /// recognized automatically.
    /// </summary>
    internal static class VesselPartClassifier
    {
        public static void Classify(
            Part part,
            VesselTopologyNode node)
        {
            if (part == null ||
                node == null)
            {
                return;
            }

            ReadModules(
                part,
                node);

            ReadResources(
                part,
                node);

            ReadCrewCapability(
                part,
                node);

            node.Category =
                SelectPrimaryCategory(
                    node);
        }

        private static void ReadModules(
            Part part,
            VesselTopologyNode node)
        {
            if (part.Modules == null)
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

                if (module is ModuleCommand)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Command);
                }

                ModuleEngines engine =
                    module as ModuleEngines;

                if (engine != null)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Engine);

                    if (EngineUsesResource(
                            engine,
                            "SolidFuel"))
                    {
                        AddRole(
                            node,
                            VesselNodeRole.SolidPropulsion);
                    }
                    else
                    {
                        AddRole(
                            node,
                            VesselNodeRole.LiquidPropulsion);
                    }
                }

                if (module is ModuleDecouple)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Decoupler);
                }

                if (module is ModuleAnchoredDecoupler)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Decoupler);
                }

                string moduleName =
                    module.moduleName ??
                    module.GetType().Name ??
                    string.Empty;

                if (ContainsIgnoreCase(
                        moduleName,
                        "Separator"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.Separator);
                }

                if (module is ModuleProceduralFairing ||
                    ContainsIgnoreCase(
                        moduleName,
                        "Fairing"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.Fairing);
                }

                if (module is ModuleRCS)
                {
                    AddRole(
                        node,
                        VesselNodeRole.RcsThruster);
                }

                if (module is ModuleReactionWheel)
                {
                    AddRole(
                        node,
                        VesselNodeRole.ReactionWheel);
                }

                if (module is ModuleDeployableSolarPanel)
                {
                    AddRole(
                        node,
                        VesselNodeRole.SolarGeneration);
                }

                if (module is ModuleGenerator)
                {
                    AddRole(
                        node,
                        VesselNodeRole.ElectricalGeneration);
                }

                if (ContainsIgnoreCase(
                        moduleName,
                        "ResourceConverter"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.ElectricalGeneration);
                }

                if (ContainsIgnoreCase(
                        moduleName,
                        "FuelCell"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.FuelCell |
                        VesselNodeRole.ElectricalGeneration);
                }

                if (module is ModuleDockingNode)
                {
                    AddRole(
                        node,
                        VesselNodeRole.DockingPort);
                }

                if (module is ModuleDataTransmitter)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Antenna);
                }

                if (module is ModuleScienceExperiment ||
                    module is ModuleScienceContainer)
                {
                    AddRole(
                        node,
                        VesselNodeRole.Science);
                }

                if (ContainsIgnoreCase(
                        moduleName,
                        "Inventory") ||
                    ContainsIgnoreCase(
                        moduleName,
                        "Cargo"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.Cargo);
                }
            }
        }

        private static void ReadResources(
            Part part,
            VesselTopologyNode node)
        {
            if (part.Resources == null)
            {
                return;
            }

            for (int index = 0;
                 index < part.Resources.Count;
                 index++)
            {
                PartResource resource =
                    part.Resources[index];

                if (resource == null ||
                    resource.info == null)
                {
                    continue;
                }

                string resourceName =
                    resource.info.name ??
                    string.Empty;

                AddUniqueResource(
                    node,
                    resourceName);

                if (EqualsIgnoreCase(
                        resourceName,
                        "LiquidFuel"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresLiquidFuel);
                }
                else if (EqualsIgnoreCase(
                             resourceName,
                             "Oxidizer"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresOxidizer);
                }
                else if (EqualsIgnoreCase(
                             resourceName,
                             "MonoPropellant"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresMonopropellant);
                }
                else if (EqualsIgnoreCase(
                             resourceName,
                             "SolidFuel"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresSolidFuel);
                }
                else if (EqualsIgnoreCase(
                             resourceName,
                             "XenonGas"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresXenonGas);
                }
                else if (EqualsIgnoreCase(
                             resourceName,
                             "ElectricCharge"))
                {
                    AddRole(
                        node,
                        VesselNodeRole.StoresElectricCharge);
                }
            }
        }

        private static void ReadCrewCapability(
            Part part,
            VesselTopologyNode node)
        {
            if (part.CrewCapacity > 0)
            {
                AddRole(
                    node,
                    VesselNodeRole.Crew);
            }
        }

        private static VesselNodeCategory SelectPrimaryCategory(
            VesselTopologyNode node)
        {
            if (node.HasRole(
                    VesselNodeRole.Command))
            {
                return VesselNodeCategory.Command;
            }

            if (node.HasRole(
                    VesselNodeRole.Engine))
            {
                return node.HasRole(
                        VesselNodeRole.SolidPropulsion)
                    ? VesselNodeCategory.SolidBooster
                    : VesselNodeCategory.Engine;
            }

            if (node.HasRole(
                    VesselNodeRole.Decoupler) ||
                node.HasRole(
                    VesselNodeRole.Separator))
            {
                return VesselNodeCategory.Decoupler;
            }

            if (node.HasRole(
                    VesselNodeRole.Fairing))
            {
                return VesselNodeCategory.Fairing;
            }

            if (HasPropellantStorage(
                    node))
            {
                return VesselNodeCategory.FuelTank;
            }

            if (node.HasRole(
                    VesselNodeRole.RcsThruster))
            {
                return VesselNodeCategory.RcsThruster;
            }

            if (node.HasRole(
                    VesselNodeRole.ReactionWheel))
            {
                return VesselNodeCategory.ReactionWheel;
            }

            if (node.HasRole(
                    VesselNodeRole.SolarGeneration))
            {
                return VesselNodeCategory.SolarPanel;
            }

            if (node.HasRole(
                    VesselNodeRole.FuelCell) ||
                node.HasRole(
                    VesselNodeRole.ElectricalGeneration))
            {
                return VesselNodeCategory.Generator;
            }

            if (node.HasRole(
                    VesselNodeRole.DockingPort))
            {
                return VesselNodeCategory.DockingPort;
            }

            if (node.HasRole(
                    VesselNodeRole.Antenna))
            {
                return VesselNodeCategory.Antenna;
            }

            if (node.HasRole(
                    VesselNodeRole.StoresElectricCharge))
            {
                return VesselNodeCategory.Battery;
            }

            if (node.HasRole(
                    VesselNodeRole.Science) ||
                node.HasRole(
                    VesselNodeRole.Cargo))
            {
                return VesselNodeCategory.Payload;
            }

            /*
             * Parts with no recognized active system are treated as
             * structural for schematic layout. Unknown is reserved for an
             * invalid or incomplete part record.
             */
            return VesselNodeCategory.Structural;
        }

        private static bool HasPropellantStorage(
            VesselTopologyNode node)
        {
            return
                node.HasRole(
                    VesselNodeRole.StoresLiquidFuel) ||
                node.HasRole(
                    VesselNodeRole.StoresOxidizer) ||
                node.HasRole(
                    VesselNodeRole.StoresMonopropellant) ||
                node.HasRole(
                    VesselNodeRole.StoresSolidFuel) ||
                node.HasRole(
                    VesselNodeRole.StoresXenonGas);
        }

        private static bool EngineUsesResource(
            ModuleEngines engine,
            string resourceName)
        {
            if (engine == null ||
                engine.propellants == null)
            {
                return false;
            }

            for (int index = 0;
                 index < engine.propellants.Count;
                 index++)
            {
                Propellant propellant =
                    engine.propellants[index];

                if (propellant != null &&
                    EqualsIgnoreCase(
                        propellant.name,
                        resourceName))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRole(
            VesselTopologyNode node,
            VesselNodeRole role)
        {
            node.Roles |=
                role;
        }

        private static void AddUniqueResource(
            VesselTopologyNode node,
            string resourceName)
        {
            if (string.IsNullOrEmpty(
                    resourceName))
            {
                return;
            }

            for (int index = 0;
                 index < node.StoredResourceNames.Count;
                 index++)
            {
                if (EqualsIgnoreCase(
                        node.StoredResourceNames[index],
                        resourceName))
                {
                    return;
                }
            }

            node.StoredResourceNames.Add(
                resourceName);
        }

        private static bool ContainsIgnoreCase(
            string value,
            string fragment)
        {
            return
                !string.IsNullOrEmpty(value) &&
                !string.IsNullOrEmpty(fragment) &&
                value.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool EqualsIgnoreCase(
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
