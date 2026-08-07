using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Electrical
{
    internal static class ElectricalNetworkBuilder
    {
        public static ElectricalNetwork Build(
            VesselTopology topology)
        {
            ElectricalNetwork network =
                new ElectricalNetwork();

            if (topology == null)
            {
                network.Diagnostics.Add(
                    "Electrical network unavailable: vessel topology has not been received.");

                return network;
            }

            network.VesselName =
                topology.VesselName;

            network.TopologyRevision =
                topology.Revision;

            network.CurrentStage =
                topology.CurrentStage;

            network.StructuralPartCount =
                topology.Nodes != null
                    ? topology.Nodes.Count
                    : 0;

            Dictionary<uint, PowerNode> electricalNodes =
                new Dictionary<uint, PowerNode>();

            if (topology.Nodes != null)
            {
                for (int index = 0;
                     index < topology.Nodes.Count;
                     index++)
                {
                    VesselTopologyNode topologyNode =
                        topology.Nodes[index];

                    if (topologyNode == null)
                    {
                        continue;
                    }

                    PowerNode node =
                        BuildNode(
                            topologyNode);

                    if (node == null)
                    {
                        continue;
                    }

                    electricalNodes[node.PartId] =
                        node;

                    network.Nodes.Add(
                        node);

                    network.BusMemberships.Add(
                        new ElectricalBusMembership
                        {
                            BusId =
                                ElectricalNetwork.VesselElectricChargeBusId,

                            PartId =
                                node.PartId
                        });

                    if (node.Sources.Count > 0)
                    {
                        network.SourceNodeCount++;
                    }

                    if (node.Storage.Count > 0)
                    {
                        network.StorageNodeCount++;
                    }

                    if (node.Consumers.Count > 0)
                    {
                        network.ConsumerNodeCount++;

                        bool hasExplicit =
                            false;

                        bool hasPotential =
                            false;

                        for (int consumerIndex = 0;
                             consumerIndex < node.Consumers.Count;
                             consumerIndex++)
                        {
                            if (node.Consumers[consumerIndex].IsPotentialOnly)
                            {
                                hasPotential =
                                    true;
                            }
                            else
                            {
                                hasExplicit =
                                    true;
                            }
                        }

                        if (hasExplicit)
                        {
                            network.ExplicitConsumerNodeCount++;
                        }

                        if (!hasExplicit &&
                            hasPotential)
                        {
                            network.PotentialConsumerNodeCount++;
                        }
                    }

                    for (int storageIndex = 0;
                         storageIndex < node.Storage.Count;
                         storageIndex++)
                    {
                        network.StoredElectricCharge +=
                            node.Storage[storageIndex].AmountEc;

                        network.ElectricChargeCapacity +=
                            node.Storage[storageIndex].CapacityEc;
                    }
                }

                BuildStructuralTopology(
                    topology,
                    electricalNodes,
                    network);
            }

            network.Diagnostics.Add(
                "Electrical discovery refined from roles, resources, module inputs/outputs, and propellant requirements.");

            network.Diagnostics.Add(
                "All current electrical nodes belong to logical bus VESSEL_EC while the vessel remains connected.");

            network.Diagnostics.Add(
                "Structural topology is retained separately across electrical and non-electrical intermediary parts.");

            network.Diagnostics.Add(
                "Role-only consumers are marked potential until an ElectricCharge input is observed.");

            network.Diagnostics.Add(
                "Generation and consumption rates remain unresolved until later Build 8 milestones.");

            return network;
        }

        private static void BuildStructuralTopology(
            VesselTopology topology,
            Dictionary<uint, PowerNode> electricalNodes,
            ElectricalNetwork network)
        {
            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null ||
                    !node.HasParent)
                {
                    continue;
                }

                network.StructuralConnections.Add(
                    new StructuralConnection
                    {
                        ParentPartId =
                            node.ParentPartId,

                        ChildPartId =
                            node.PartId,

                        ChildActivationStage =
                            node.ActivationStage,

                        ChildSeparationStage =
                            node.SeparationStage,

                        ParentIsElectricalNode =
                            electricalNodes.ContainsKey(
                                node.ParentPartId),

                        ChildIsElectricalNode =
                            electricalNodes.ContainsKey(
                                node.PartId)
                    });
            }
        }

        private static PowerNode BuildNode(
            VesselTopologyNode topologyNode)
        {
            PowerNode node =
                new PowerNode
                {
                    PartId =
                        topologyNode.PartId,

                    ParentPartId =
                        topologyNode.ParentPartId,

                    HasParent =
                        topologyNode.HasParent,

                    PartName =
                        topologyNode.PartName,

                    PartTitle =
                        topologyNode.PartTitle,

                    ActivationStage =
                        topologyNode.ActivationStage,

                    SeparationStage =
                        topologyNode.SeparationStage
                };

            AddSources(
                topologyNode,
                node);

            AddStorage(
                topologyNode,
                node);

            AddConsumers(
                topologyNode,
                node);

            node.RefreshKind();

            return
                node.Kind ==
                    ElectricalNodeKind.Unknown
                    ? null
                    : node;
        }

        private static void AddSources(
            VesselTopologyNode topologyNode,
            PowerNode node)
        {
            if (topologyNode.HasRole(
                    VesselNodeRole.SolarGeneration))
            {
                EnsureSource(
                    node,
                    PowerSourceType.Solar,
                    "Solar",
                    string.Empty,
                    ElectricalEvidenceType.ExistingRole);
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.FuelCell))
            {
                EnsureSource(
                    node,
                    PowerSourceType.FuelCell,
                    "Fuel Cell",
                    string.Empty,
                    ElectricalEvidenceType.ExistingRole);
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.ElectricalGeneration))
            {
                EnsureSource(
                    node,
                    ResolveGeneratorType(
                        topologyNode),
                    "Electrical Generator",
                    string.Empty,
                    ElectricalEvidenceType.ExistingRole);
            }

            if (topologyNode.Modules == null)
            {
                return;
            }

            for (int moduleIndex = 0;
                 moduleIndex < topologyNode.Modules.Count;
                 moduleIndex++)
            {
                VesselModuleDescriptor module =
                    topologyNode.Modules[moduleIndex];

                if (module == null ||
                    module.OutputResources == null)
                {
                    continue;
                }

                if (!ContainsElectricCharge(
                        module.OutputResources))
                {
                    continue;
                }

                PowerSourceType sourceType =
                    ClassifySourceModule(
                        topologyNode,
                        module);

                EnsureSource(
                    node,
                    sourceType,
                    string.IsNullOrWhiteSpace(
                        module.DisplayName)
                        ? "Electrical Producer"
                        : module.DisplayName,
                    module.ModuleName,
                    ElectricalEvidenceType.ModuleOutput);
            }
        }

        private static void EnsureSource(
            PowerNode node,
            PowerSourceType type,
            string name,
            string moduleName,
            ElectricalEvidenceType evidence)
        {
            for (int index = 0;
                 index < node.Sources.Count;
                 index++)
            {
                PowerSource current =
                    node.Sources[index];

                if (current.SourceType ==
                    type)
                {
                    if (evidence ==
                        ElectricalEvidenceType.ModuleOutput)
                    {
                        current.Evidence =
                            evidence;

                        if (!string.IsNullOrWhiteSpace(
                                moduleName))
                        {
                            current.ModuleName =
                                moduleName;
                        }
                    }

                    return;
                }
            }

            node.Sources.Add(
                new PowerSource
                {
                    PartId =
                        node.PartId,

                    SourceType =
                        type,

                    SourceName =
                        name,

                    ModuleName =
                        moduleName ?? string.Empty,

                    Evidence =
                        evidence,

                    HasKnownGenerationRate =
                        false,

                    GenerationRateEcPerSecond =
                        0.0
                });
        }

        private static PowerSourceType ResolveGeneratorType(
            VesselTopologyNode topologyNode)
        {
            string partText =
                ((topologyNode.PartName ?? string.Empty) +
                 " " +
                 (topologyNode.PartTitle ?? string.Empty))
                    .ToLowerInvariant();

            if (partText.Contains("rtg") ||
                partText.Contains("radioisotope"))
            {
                return
                    PowerSourceType.Radioisotope;
            }

            return
                PowerSourceType.Generator;
        }

        private static PowerSourceType ClassifySourceModule(
            VesselTopologyNode topologyNode,
            VesselModuleDescriptor module)
        {
            string text =
                ((module.ModuleName ?? string.Empty) +
                 " " +
                 (module.ModuleTypeName ?? string.Empty) +
                 " " +
                 (module.DisplayName ?? string.Empty) +
                 " " +
                 (topologyNode.PartName ?? string.Empty) +
                 " " +
                 (topologyNode.PartTitle ?? string.Empty))
                    .ToLowerInvariant();

            if (text.Contains("solar"))
            {
                return
                    PowerSourceType.Solar;
            }

            if (text.Contains("fuelcell") ||
                text.Contains("fuel cell"))
            {
                return
                    PowerSourceType.FuelCell;
            }

            if (text.Contains("rtg") ||
                text.Contains("radioisotope"))
            {
                return
                    PowerSourceType.Radioisotope;
            }

            return
                PowerSourceType.Generator;
        }

        private static void AddStorage(
            VesselTopologyNode topologyNode,
            PowerNode node)
        {
            if (topologyNode.Resources == null)
            {
                return;
            }

            for (int index = 0;
                 index < topologyNode.Resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    topologyNode.Resources[index];

                if (resource == null ||
                    !string.Equals(
                        resource.Name,
                        "ElectricCharge",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                node.Storage.Add(
                    new PowerStorage
                    {
                        PartId =
                            topologyNode.PartId,

                        ResourceName =
                            resource.Name,

                        Evidence =
                            ElectricalEvidenceType.StoredResource,

                        AmountEc =
                            Math.Max(
                                0.0,
                                resource.Amount),

                        CapacityEc =
                            Math.Max(
                                0.0,
                                resource.Capacity)
                    });
            }
        }

        private static void AddConsumers(
            VesselTopologyNode topologyNode,
            PowerNode node)
        {
            bool explicitInput =
                AddExplicitModuleConsumers(
                    topologyNode,
                    node);

            bool propellantInput =
                HasElectricChargePropellant(
                    topologyNode);

            if (propellantInput)
            {
                EnsureConsumer(
                    node,
                    ResolveConsumerType(
                        topologyNode),
                    ResolveConsumerName(
                        topologyNode),
                    string.Empty,
                    ElectricalEvidenceType.PropellantRequirement,
                    false);
            }

            /*
             * Role-only consumers remain useful for engineering discovery,
             * but they are explicitly labeled potential until an EC input is
             * visible in module/propellant metadata.
             */
            if (!explicitInput &&
                !propellantInput)
            {
                AddPotentialRoleConsumer(
                    topologyNode,
                    node,
                    VesselNodeRole.Command,
                    PowerConsumerType.Command,
                    "Command");

                AddPotentialRoleConsumer(
                    topologyNode,
                    node,
                    VesselNodeRole.ReactionWheel,
                    PowerConsumerType.AttitudeControl,
                    "Attitude Control");

                AddPotentialRoleConsumer(
                    topologyNode,
                    node,
                    VesselNodeRole.Antenna,
                    PowerConsumerType.Communication,
                    "Communication");

                AddPotentialRoleConsumer(
                    topologyNode,
                    node,
                    VesselNodeRole.Science,
                    PowerConsumerType.Science,
                    "Science");
            }
        }

        private static bool AddExplicitModuleConsumers(
            VesselTopologyNode topologyNode,
            PowerNode node)
        {
            bool found =
                false;

            if (topologyNode.Modules == null)
            {
                return
                    false;
            }

            for (int moduleIndex = 0;
                 moduleIndex < topologyNode.Modules.Count;
                 moduleIndex++)
            {
                VesselModuleDescriptor module =
                    topologyNode.Modules[moduleIndex];

                if (module == null ||
                    module.InputResources == null ||
                    !ContainsElectricCharge(
                        module.InputResources))
                {
                    continue;
                }

                found =
                    true;

                EnsureConsumer(
                    node,
                    ResolveConsumerType(
                        topologyNode),
                    string.IsNullOrWhiteSpace(
                        module.DisplayName)
                        ? ResolveConsumerName(
                            topologyNode)
                        : module.DisplayName,
                    module.ModuleName,
                    ElectricalEvidenceType.ModuleInput,
                    false);
            }

            return
                found;
        }

        private static void AddPotentialRoleConsumer(
            VesselTopologyNode topologyNode,
            PowerNode node,
            VesselNodeRole role,
            PowerConsumerType type,
            string name)
        {
            if (!topologyNode.HasRole(
                    role))
            {
                return;
            }

            EnsureConsumer(
                node,
                type,
                name,
                string.Empty,
                ElectricalEvidenceType.ExistingRole,
                true);
        }

        private static void EnsureConsumer(
            PowerNode node,
            PowerConsumerType type,
            string name,
            string moduleName,
            ElectricalEvidenceType evidence,
            bool potentialOnly)
        {
            for (int index = 0;
                 index < node.Consumers.Count;
                 index++)
            {
                PowerConsumer current =
                    node.Consumers[index];

                if (current.ConsumerType !=
                    type)
                {
                    continue;
                }

                if (!potentialOnly)
                {
                    current.IsPotentialOnly =
                        false;

                    current.Evidence =
                        evidence;

                    if (!string.IsNullOrWhiteSpace(
                            moduleName))
                    {
                        current.ModuleName =
                            moduleName;
                    }
                }

                return;
            }

            node.Consumers.Add(
                new PowerConsumer
                {
                    PartId =
                        node.PartId,

                    ConsumerType =
                        type,

                    ConsumerName =
                        name,

                    ModuleName =
                        moduleName ?? string.Empty,

                    Evidence =
                        evidence,

                    IsPotentialOnly =
                        potentialOnly,

                    HasKnownConsumptionRate =
                        false,

                    ConsumptionRateEcPerSecond =
                        0.0
                });
        }

        private static PowerConsumerType ResolveConsumerType(
            VesselTopologyNode topologyNode)
        {
            if (topologyNode.HasRole(
                    VesselNodeRole.Command))
            {
                return
                    PowerConsumerType.Command;
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.ReactionWheel))
            {
                return
                    PowerConsumerType.AttitudeControl;
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.Antenna))
            {
                return
                    PowerConsumerType.Communication;
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.Science))
            {
                return
                    PowerConsumerType.Science;
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.Engine))
            {
                return
                    PowerConsumerType.Propulsion;
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.RcsThruster))
            {
                return
                    PowerConsumerType.ReactionControl;
            }

            return
                PowerConsumerType.Utility;
        }

        private static string ResolveConsumerName(
            VesselTopologyNode topologyNode)
        {
            PowerConsumerType type =
                ResolveConsumerType(
                    topologyNode);

            switch (type)
            {
                case PowerConsumerType.Command:
                    return "Command";

                case PowerConsumerType.AttitudeControl:
                    return "Attitude Control";

                case PowerConsumerType.Communication:
                    return "Communication";

                case PowerConsumerType.Science:
                    return "Science";

                case PowerConsumerType.Propulsion:
                    return "Electric Propulsion";

                case PowerConsumerType.ReactionControl:
                    return "Reaction Control";

                default:
                    return "Module Electric Load";
            }
        }

        private static bool HasElectricChargePropellant(
            VesselTopologyNode topologyNode)
        {
            if (topologyNode.PropellantRequirements ==
                null)
            {
                return
                    false;
            }

            for (int index = 0;
                 index < topologyNode.PropellantRequirements.Count;
                 index++)
            {
                VesselPropellantRequirement requirement =
                    topologyNode.PropellantRequirements[index];

                if (requirement != null &&
                    string.Equals(
                        requirement.Name,
                        "ElectricCharge",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        true;
                }
            }

            return
                false;
        }

        private static bool ContainsElectricCharge(
            System.Collections.Generic.IList<VesselModuleResource> resources)
        {
            if (resources == null)
            {
                return
                    false;
            }

            for (int index = 0;
                 index < resources.Count;
                 index++)
            {
                VesselModuleResource resource =
                    resources[index];

                if (resource != null &&
                    string.Equals(
                        resource.Name,
                        "ElectricCharge",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return
                        true;
                }
            }

            return
                false;
        }
    }
}
