using System;
using System.Collections.Generic;
using KMC.Engine.Capabilities;
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

            Dictionary<uint, PowerNode> nodes =
                new Dictionary<uint, PowerNode>();

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

                nodes[node.PartId] =
                    node;

                network.Nodes.Add(
                    node);

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

            /*
             * Build 8.0 records physical parent/child adjacency.
             *
             * This does NOT yet claim stock KSP has segmented electrical
             * buses. It simply preserves the structural topology needed for
             * later staging and section analysis.
             */
            foreach (PowerNode current
                in network.Nodes)
            {
                if (!current.HasParent)
                {
                    continue;
                }

                if (!nodes.ContainsKey(
                        current.ParentPartId))
                {
                    continue;
                }

                network.Connections.Add(
                    new PowerConnection
                    {
                        FromPartId =
                            current.ParentPartId,

                        ToPartId =
                            current.PartId,

                        ConnectionType =
                            ElectricalConnectionType.StructuralParentChild
                    });
            }

            network.Diagnostics.Add(
                "Electrical domain model built from vessel topology.");

            network.Diagnostics.Add(
                "Build 8.0 models electrical component identity and structural adjacency only.");

            network.Diagnostics.Add(
                "Generation and consumption rates are intentionally unresolved until later milestones.");

            network.Diagnostics.Add(
                "Stock ElectricCharge is not treated as a physically segmented bus in this milestone.");

            return network;
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
                node.Sources.Add(
                    NewSource(
                        topologyNode.PartId,
                        PowerSourceType.Solar,
                        "Solar"));
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.FuelCell))
            {
                node.Sources.Add(
                    NewSource(
                        topologyNode.PartId,
                        PowerSourceType.FuelCell,
                        "Fuel Cell"));
            }

            if (topologyNode.HasRole(
                    VesselNodeRole.ElectricalGeneration))
            {
                node.Sources.Add(
                    NewSource(
                        topologyNode.PartId,
                        ResolveGeneratorType(
                            topologyNode),
                        "Electrical Generator"));
            }
        }

        private static PowerSource NewSource(
            uint partId,
            PowerSourceType type,
            string name)
        {
            return
                new PowerSource
                {
                    PartId =
                        partId,

                    SourceType =
                        type,

                    SourceName =
                        name,

                    HasKnownGenerationRate =
                        false,

                    GenerationRateEcPerSecond =
                        0.0
                };
        }

        private static PowerSourceType ResolveGeneratorType(
            VesselTopologyNode topologyNode)
        {
            string partText =
                ((topologyNode.PartName ?? string.Empty) +
                 " " +
                 (topologyNode.PartTitle ?? string.Empty))
                    .ToLowerInvariant();

            if (partText.Contains(
                    "rtg") ||
                partText.Contains(
                    "radioisotope"))
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
            AddConsumerRole(
                topologyNode,
                node,
                VesselNodeRole.Command,
                PowerConsumerType.Command,
                "Command");

            AddConsumerRole(
                topologyNode,
                node,
                VesselNodeRole.ReactionWheel,
                PowerConsumerType.AttitudeControl,
                "Attitude Control");

            AddConsumerRole(
                topologyNode,
                node,
                VesselNodeRole.Antenna,
                PowerConsumerType.Communication,
                "Communication");

            AddConsumerRole(
                topologyNode,
                node,
                VesselNodeRole.Science,
                PowerConsumerType.Science,
                "Science");

            AddConsumerRole(
                topologyNode,
                node,
                VesselNodeRole.RcsThruster,
                PowerConsumerType.ReactionControl,
                "Reaction Control");

            /*
             * An engine is recorded as a potential electrical consumer class,
             * but no demand is assumed. This supports electric/mod engines
             * later without pretending every stock engine consumes EC.
             */
            if (topologyNode.HasRole(
                    VesselNodeRole.Engine) &&
                ConsumesElectricCharge(
                    topologyNode))
            {
                node.Consumers.Add(
                    NewConsumer(
                        topologyNode.PartId,
                        PowerConsumerType.Propulsion,
                        "Electric Propulsion"));
            }

            if (HasModuleElectricInput(
                    topologyNode))
            {
                if (node.Consumers.Count == 0)
                {
                    node.Consumers.Add(
                        NewConsumer(
                            topologyNode.PartId,
                            PowerConsumerType.Utility,
                            "Module Electric Load"));
                }
            }
        }

        private static void AddConsumerRole(
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

            node.Consumers.Add(
                NewConsumer(
                    topologyNode.PartId,
                    type,
                    name));
        }

        private static PowerConsumer NewConsumer(
            uint partId,
            PowerConsumerType type,
            string name)
        {
            return
                new PowerConsumer
                {
                    PartId =
                        partId,

                    ConsumerType =
                        type,

                    ConsumerName =
                        name,

                    HasKnownConsumptionRate =
                        false,

                    ConsumptionRateEcPerSecond =
                        0.0
                };
        }

        private static bool ConsumesElectricCharge(
            VesselTopologyNode topologyNode)
        {
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
                HasModuleElectricInput(
                    topologyNode);
        }

        private static bool HasModuleElectricInput(
            VesselTopologyNode topologyNode)
        {
            for (int moduleIndex = 0;
                 moduleIndex < topologyNode.Modules.Count;
                 moduleIndex++)
            {
                VesselModuleDescriptor module =
                    topologyNode.Modules[moduleIndex];

                if (module == null ||
                    module.InputResources == null)
                {
                    continue;
                }

                for (int inputIndex = 0;
                     inputIndex < module.InputResources.Count;
                     inputIndex++)
                {
                    VesselModuleResource input =
                        module.InputResources[inputIndex];

                    if (input != null &&
                        string.Equals(
                            input.Name,
                            "ElectricCharge",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return
                            true;
                    }
                }
            }

            return
                false;
        }
    }
}
