using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Propulsion
{
    internal static class PropulsionTopologyAnalyzer
    {
        public static PropulsionTopologyModel Analyze(
            VesselTopology topology)
        {
            PropulsionTopologyModel model =
                new PropulsionTopologyModel();

            if (topology == null)
            {
                return model;
            }

            model.Available =
                true;

            model.VesselId =
                topology.VesselId ??
                string.Empty;

            model.VesselName =
                topology.VesselName ??
                string.Empty;

            model.TopologyRevision =
                topology.Revision;

            model.TopologyCurrentStage =
                topology.CurrentStage;

            model.TopologyNextStage =
                topology.NextStage;

            Dictionary<uint, VesselTopologyNode> nodes =
                BuildNodeMap(
                    topology.Nodes);

            HashSet<int> separationStages =
                new HashSet<int>();

            HashSet<string> sourceKeys =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                if (node.IsSeparationBoundary &&
                    node.SeparationStage >= 0)
                {
                    separationStages.Add(
                        node.SeparationStage);
                }

                if (!IsPropulsionEngine(
                        node))
                {
                    continue;
                }

                PropulsionEngineModel engine =
                    CreateEngine(
                        node);

                CopyRequirementsAndSources(
                    node,
                    engine,
                    nodes,
                    sourceKeys,
                    model);

                model.Engines.Add(
                    engine);
            }

            model.Engines.Sort(
                delegate(
                    PropulsionEngineModel left,
                    PropulsionEngineModel right)
                {
                    int stageCompare =
                        right.ActivationStage
                            .CompareTo(
                                left.ActivationStage);

                    if (stageCompare != 0)
                    {
                        return stageCompare;
                    }

                    return
                        left.PartId.CompareTo(
                            right.PartId);
                });

            model.ResourceSources.Sort(
                delegate(
                    PropulsionResourceSourceModel left,
                    PropulsionResourceSourceModel right)
                {
                    int partCompare =
                        left.PartId.CompareTo(
                            right.PartId);

                    if (partCompare != 0)
                    {
                        return partCompare;
                    }

                    return
                        string.Compare(
                            left.ResourceName,
                            right.ResourceName,
                            StringComparison
                                .OrdinalIgnoreCase);
                });

            List<int> sortedStages =
                new List<int>(
                    separationStages);

            sortedStages.Sort(
                delegate(
                    int left,
                    int right)
                {
                    return
                        right.CompareTo(
                            left);
                });

            for (int index = 0;
                 index < sortedStages.Count;
                 index++)
            {
                model.SeparationStages.Add(
                    sortedStages[index]);
            }

            return model;
        }

        private static Dictionary<uint, VesselTopologyNode>
            BuildNodeMap(
                IList<VesselTopologyNode> nodes)
        {
            Dictionary<uint, VesselTopologyNode> result =
                new Dictionary<uint, VesselTopologyNode>();

            if (nodes == null)
            {
                return result;
            }

            for (int index = 0;
                 index < nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    nodes[index];

                if (node != null)
                {
                    result[node.PartId] =
                        node;
                }
            }

            return result;
        }

        private static bool IsPropulsionEngine(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            return
                node.Category ==
                    VesselNodeCategory.Engine ||
                node.Category ==
                    VesselNodeCategory.SolidBooster;
        }

        private static PropulsionEngineModel CreateEngine(
            VesselTopologyNode node)
        {
            return
                new PropulsionEngineModel
                {
                    PartId =
                        node.PartId,

                    PartTitle =
                        node.PartTitle ??
                        string.Empty,

                    PartName =
                        node.PartName ??
                        string.Empty,

                    Category =
                        node.Category,

                    ActivationStage =
                        node.ActivationStage,

                    SeparationStage =
                        node.SeparationStage,

                    StructuralDepth =
                        node.StructuralDepth,

                    BranchRootPartId =
                        node.BranchRootPartId,

                    SymmetryGroupId =
                        node.SymmetryGroupId,

                    VesselX =
                        node.VesselX,

                    VesselY =
                        node.VesselY,

                    VesselZ =
                        node.VesselZ,

                    SurvivesNextStage =
                        node.SurvivesNextStage
                };
        }

        private static void CopyRequirementsAndSources(
            VesselTopologyNode node,
            PropulsionEngineModel engine,
            IDictionary<uint, VesselTopologyNode> nodes,
            ISet<string> sourceKeys,
            PropulsionTopologyModel model)
        {
            if (node.PropellantRequirements == null)
            {
                return;
            }

            for (int requirementIndex = 0;
                 requirementIndex <
                    node.PropellantRequirements.Count;
                 requirementIndex++)
            {
                VesselPropellantRequirement sourceRequirement =
                    node.PropellantRequirements[
                        requirementIndex];

                if (sourceRequirement == null)
                {
                    continue;
                }

                PropulsionPropellantRequirementModel requirement =
                    new PropulsionPropellantRequirementModel
                    {
                        ResourceId =
                            sourceRequirement.ResourceId,

                        ResourceName =
                            sourceRequirement.Name ??
                            string.Empty,

                        Ratio =
                            sourceRequirement.Ratio,

                        DensityTonnesPerUnit =
                            sourceRequirement
                                .DensityTonnesPerUnit,

                        RawFlowMode =
                            sourceRequirement.RawFlowMode ??
                            string.Empty
                    };

                for (int sourceIndex = 0;
                     sourceIndex <
                        sourceRequirement
                            .ReachableSourcePartIds.Count;
                     sourceIndex++)
                {
                    uint sourcePartId =
                        sourceRequirement
                            .ReachableSourcePartIds[
                                sourceIndex];

                    AddUnique(
                        requirement.ReachableSourcePartIds,
                        sourcePartId);

                    AddResourceSource(
                        sourcePartId,
                        sourceRequirement,
                        nodes,
                        sourceKeys,
                        model);
                }

                engine.PropellantRequirements.Add(
                    requirement);
            }
        }

        private static void AddResourceSource(
            uint sourcePartId,
            VesselPropellantRequirement requirement,
            IDictionary<uint, VesselTopologyNode> nodes,
            ISet<string> sourceKeys,
            PropulsionTopologyModel model)
        {
            string resourceName =
                requirement.Name ??
                string.Empty;

            string key =
                sourcePartId.ToString() +
                "|" +
                resourceName;

            if (!sourceKeys.Add(
                    key))
            {
                return;
            }

            PropulsionResourceSourceModel result =
                new PropulsionResourceSourceModel
                {
                    PartId =
                        sourcePartId,

                    ResourceId =
                        requirement.ResourceId,

                    ResourceName =
                        resourceName
                };

            VesselTopologyNode sourceNode;

            if (nodes.TryGetValue(
                    sourcePartId,
                    out sourceNode) &&
                sourceNode != null)
            {
                result.PartTitle =
                    sourceNode.PartTitle ??
                    string.Empty;

                result.SurvivesNextStage =
                    sourceNode.SurvivesNextStage;

                VesselResourceState resource =
                    FindResource(
                        sourceNode.Resources,
                        requirement.ResourceId,
                        resourceName);

                if (resource != null)
                {
                    result.ResourceStateAvailable =
                        true;

                    result.Amount =
                        resource.Amount;

                    result.Capacity =
                        resource.Capacity;

                    result.DensityTonnesPerUnit =
                        resource.DensityTonnesPerUnit;

                    result.FlowEnabled =
                        resource.FlowEnabled;
                }
            }

            model.ResourceSources.Add(
                result);
        }

        private static VesselResourceState FindResource(
            IList<VesselResourceState> resources,
            int resourceId,
            string resourceName)
        {
            if (resources == null)
            {
                return null;
            }

            for (int index = 0;
                 index < resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    resources[index];

                if (resource == null)
                {
                    continue;
                }

                if (resource.ResourceId ==
                        resourceId ||
                    string.Equals(
                        resource.Name,
                        resourceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return resource;
                }
            }

            return null;
        }

        private static void AddUnique(
            IList<uint> values,
            uint value)
        {
            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (values[index] ==
                    value)
                {
                    return;
                }
            }

            values.Add(
                value);
        }
    }
}
