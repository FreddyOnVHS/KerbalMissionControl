using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Electrical
{
    internal static class ElectricalStorageAnalyzer
    {
        public static ElectricalStorageModel Analyze(
            VesselTopology topology,
            ElectricalNetwork network)
        {
            ElectricalStorageModel model =
                new ElectricalStorageModel();

            if (topology == null)
            {
                model.Diagnostics.Add(
                    "Electrical storage analysis unavailable: vessel topology has not been received.");

                return
                    model;
            }

            model.TopologyRevision =
                topology.Revision;

            model.CurrentStage =
                topology.CurrentStage;

            model.NextStage =
                topology.NextStage;

            if (network == null)
            {
                model.Diagnostics.Add(
                    "Electrical storage analysis unavailable: electrical network has not been built.");

                return
                    model;
            }

            Dictionary<uint, VesselTopologyNode> topologyByPart =
                BuildTopologyIndex(
                    topology);

            Dictionary<int, ElectricalStorageStageSection> stages =
                new Dictionary<int, ElectricalStorageStageSection>();

            Dictionary<uint, ElectricalStorageBranchSection> branches =
                new Dictionary<uint, ElectricalStorageBranchSection>();

            for (int nodeIndex = 0;
                 nodeIndex < network.Nodes.Count;
                 nodeIndex++)
            {
                PowerNode powerNode =
                    network.Nodes[nodeIndex];

                if (powerNode == null ||
                    powerNode.Storage == null ||
                    powerNode.Storage.Count == 0)
                {
                    continue;
                }

                VesselTopologyNode topologyNode;

                if (!topologyByPart.TryGetValue(
                        powerNode.PartId,
                        out topologyNode))
                {
                    model.Diagnostics.Add(
                        "Storage part " +
                        powerNode.PartId +
                        " is present in the electrical network but missing from the topology index.");

                    continue;
                }

                double stored =
                    0.0;

                double capacity =
                    0.0;

                for (int storageIndex = 0;
                     storageIndex < powerNode.Storage.Count;
                     storageIndex++)
                {
                    PowerStorage storage =
                        powerNode.Storage[storageIndex];

                    if (storage == null)
                    {
                        continue;
                    }

                    stored +=
                        Math.Max(
                            0.0,
                            storage.AmountEc);

                    capacity +=
                        Math.Max(
                            0.0,
                            storage.CapacityEc);
                }

                ElectricalStoragePart part =
                    new ElectricalStoragePart
                    {
                        PartId =
                            powerNode.PartId,

                        BranchRootPartId =
                            topologyNode.BranchRootPartId,

                        PartName =
                            powerNode.PartName,

                        PartTitle =
                            powerNode.PartTitle,

                        StructuralDepth =
                            topologyNode.StructuralDepth,

                        ActivationStage =
                            topologyNode.ActivationStage,

                        SeparationStage =
                            topologyNode.SeparationStage,

                        WillSeparateOnNextStage =
                            topologyNode.WillSeparateOnNextStage,

                        StoredEc =
                            stored,

                        CapacityEc =
                            capacity
                    };

                model.Parts.Add(
                    part);

                model.StoredEc +=
                    stored;

                model.CapacityEc +=
                    capacity;

                if (part.WillSeparateOnNextStage)
                {
                    model.NextStageLostStoredEc +=
                        stored;

                    model.NextStageLostCapacityEc +=
                        capacity;
                }

                AddToStageSection(
                    stages,
                    part);

                AddToBranchSection(
                    branches,
                    part);
            }

            CopyStageSections(
                stages,
                model);

            CopyBranchSections(
                branches,
                model);

            ValidateAgainstNetwork(
                network,
                model);

            model.Diagnostics.Add(
                "Electrical storage model built from topology ElectricCharge resources.");

            model.Diagnostics.Add(
                "Next-stage storage loss uses VesselTopologyNode.WillSeparateOnNextStage.");

            model.Diagnostics.Add(
                "Storage amounts are topology snapshot state, not high-frequency EC flow telemetry.");

            if (model.LosesAllStorageOnNextStage)
            {
                model.Diagnostics.Add(
                    "WARNING: next staging event is predicted to remove all known ElectricCharge storage capacity.");
            }
            else if (model.HasStorageLossOnNextStage)
            {
                model.Diagnostics.Add(
                    "Next staging event is predicted to remove some ElectricCharge storage.");
            }

            return
                model;
        }

        private static Dictionary<uint, VesselTopologyNode>
            BuildTopologyIndex(
                VesselTopology topology)
        {
            Dictionary<uint, VesselTopologyNode> result =
                new Dictionary<uint, VesselTopologyNode>();

            if (topology.Nodes == null)
            {
                return
                    result;
            }

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node != null)
                {
                    result[node.PartId] =
                        node;
                }
            }

            return
                result;
        }

        private static void AddToStageSection(
            Dictionary<int, ElectricalStorageStageSection> sections,
            ElectricalStoragePart part)
        {
            ElectricalStorageStageSection section;

            if (!sections.TryGetValue(
                    part.SeparationStage,
                    out section))
            {
                section =
                    new ElectricalStorageStageSection
                    {
                        SeparationStage =
                            part.SeparationStage
                    };

                sections[part.SeparationStage] =
                    section;
            }

            section.Parts.Add(
                part);

            section.StoredEc +=
                part.StoredEc;

            section.CapacityEc +=
                part.CapacityEc;

            if (part.WillSeparateOnNextStage)
            {
                section.WillSeparateOnNextStage =
                    true;
            }
        }

        private static void AddToBranchSection(
            Dictionary<uint, ElectricalStorageBranchSection> sections,
            ElectricalStoragePart part)
        {
            uint branchRoot =
                part.BranchRootPartId;

            ElectricalStorageBranchSection section;

            if (!sections.TryGetValue(
                    branchRoot,
                    out section))
            {
                section =
                    new ElectricalStorageBranchSection
                    {
                        BranchRootPartId =
                            branchRoot
                    };

                sections[branchRoot] =
                    section;
            }

            section.Parts.Add(
                part);

            section.StoredEc +=
                part.StoredEc;

            section.CapacityEc +=
                part.CapacityEc;

            if (part.WillSeparateOnNextStage)
            {
                section.ContainsNextStageLoss =
                    true;
            }
        }

        private static void CopyStageSections(
            Dictionary<int, ElectricalStorageStageSection> source,
            ElectricalStorageModel model)
        {
            List<int> keys =
                new List<int>(
                    source.Keys);

            keys.Sort(
                CompareStages);

            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                model.StageSections.Add(
                    source[keys[index]]);
            }
        }

        private static int CompareStages(
            int left,
            int right)
        {
            if (left < 0 &&
                right >= 0)
            {
                return
                    1;
            }

            if (right < 0 &&
                left >= 0)
            {
                return
                    -1;
            }

            return
                right.CompareTo(
                    left);
        }

        private static void CopyBranchSections(
            Dictionary<uint, ElectricalStorageBranchSection> source,
            ElectricalStorageModel model)
        {
            List<uint> keys =
                new List<uint>(
                    source.Keys);

            keys.Sort();

            for (int index = 0;
                 index < keys.Count;
                 index++)
            {
                model.BranchSections.Add(
                    source[keys[index]]);
            }
        }

        private static void ValidateAgainstNetwork(
            ElectricalNetwork network,
            ElectricalStorageModel model)
        {
            if (Math.Abs(
                    model.StoredEc -
                    network.StoredElectricCharge) >
                0.001)
            {
                model.Diagnostics.Add(
                    "WARNING: storage model EC amount does not match electrical network aggregate.");
            }

            if (Math.Abs(
                    model.CapacityEc -
                    network.ElectricChargeCapacity) >
                0.001)
            {
                model.Diagnostics.Add(
                    "WARNING: storage model EC capacity does not match electrical network aggregate.");
            }
        }
    }
}
