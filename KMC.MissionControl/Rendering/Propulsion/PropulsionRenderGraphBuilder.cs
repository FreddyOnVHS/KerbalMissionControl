using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Converts the complete vessel topology into a compact logical graph
    /// suitable for an engineering schematic.
    ///
    /// Functional propulsion parts are always retained. Unremarkable
    /// structural chains are collapsed by connecting each retained node to
    /// its nearest retained ancestor.
    /// </summary>
    public sealed class PropulsionRenderGraphBuilder
    {
        public PropulsionRenderGraph Build(
            VesselTopology topology)
        {
            PropulsionRenderGraph graph =
                new PropulsionRenderGraph();

            if (topology == null)
            {
                return graph;
            }

            graph.VesselName =
                topology.VesselName ?? string.Empty;

            graph.TopologyRevision =
                topology.Revision;

            graph.RootPartId =
                topology.RootPartId;

            graph.HasRootPart =
                topology.HasRootPart;

            graph.CurrentStage =
                topology.CurrentStage;

            graph.NextStage =
                topology.NextStage;

            Dictionary<uint, VesselTopologyNode> sourceNodes =
                BuildSourceMap(topology.Nodes);

            HashSet<uint> retainedIds =
                SelectRetainedNodes(
                    topology,
                    sourceNodes);

            graph.CollapsedPartCount =
                Math.Max(
                    0,
                    topology.Nodes.Count -
                    retainedIds.Count);

            Dictionary<uint, PropulsionGraphNode> graphNodes =
                CreateGraphNodes(
                    topology,
                    sourceNodes,
                    retainedIds,
                    graph);

            CreateStructuralEdges(
                sourceNodes,
                retainedIds,
                graphNodes,
                graph);

            CreatePropellantEdges(
                sourceNodes,
                graphNodes,
                graph);

            SortGraph(graph);

            return graph;
        }

        private static Dictionary<uint, VesselTopologyNode> BuildSourceMap(
            IList<VesselTopologyNode> source)
        {
            Dictionary<uint, VesselTopologyNode> result =
                new Dictionary<uint, VesselTopologyNode>();

            if (source == null)
            {
                return result;
            }

            for (int index = 0;
                 index < source.Count;
                 index++)
            {
                VesselTopologyNode node =
                    source[index];

                if (node != null)
                {
                    result[node.PartId] =
                        node;
                }
            }

            return result;
        }

        private static HashSet<uint> SelectRetainedNodes(
            VesselTopology topology,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            HashSet<uint> retained =
                new HashSet<uint>();

            foreach (KeyValuePair<uint, VesselTopologyNode> pair in nodes)
            {
                VesselTopologyNode node =
                    pair.Value;

                if (ShouldRetain(node) ||
                    topology.HasRootPart &&
                    node.PartId == topology.RootPartId)
                {
                    retained.Add(node.PartId);
                }
            }

            /*
             * Preserve branch anchors so radial boosters and parallel stacks
             * remain visually distinct even when their first part is merely
             * structural.
             */
            foreach (KeyValuePair<uint, VesselTopologyNode> pair in nodes)
            {
                VesselTopologyNode node =
                    pair.Value;

                if (node.BranchRootPartId != 0)
                {
                    retained.Add(node.BranchRootPartId);
                }
            }

            return retained;
        }

        private static bool ShouldRetain(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            if (node.IsSeparationBoundary ||
                node.Category == VesselNodeCategory.Engine ||
                node.Category == VesselNodeCategory.SolidBooster ||
                node.Category == VesselNodeCategory.FuelTank ||
                node.Category == VesselNodeCategory.Command ||
                node.Category == VesselNodeCategory.RcsThruster ||
                node.Category == VesselNodeCategory.Battery ||
                node.Category == VesselNodeCategory.Generator ||
                node.Category == VesselNodeCategory.SolarPanel ||
                node.Category == VesselNodeCategory.DockingPort)
            {
                return true;
            }

            return
                node.PropellantRequirements.Count > 0 ||
                HasPropulsionResource(node);
        }

        private static bool HasPropulsionResource(
            VesselTopologyNode node)
        {
            for (int index = 0;
                 index < node.Resources.Count;
                 index++)
            {
                string name =
                    node.Resources[index].Name;

                if (EqualsResource(name, "LiquidFuel") ||
                    EqualsResource(name, "Oxidizer") ||
                    EqualsResource(name, "MonoPropellant") ||
                    EqualsResource(name, "SolidFuel") ||
                    EqualsResource(name, "XenonGas"))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<uint, PropulsionGraphNode> CreateGraphNodes(
            VesselTopology topology,
            IDictionary<uint, VesselTopologyNode> sourceNodes,
            ISet<uint> retainedIds,
            PropulsionRenderGraph graph)
        {
            Dictionary<uint, PropulsionGraphNode> result =
                new Dictionary<uint, PropulsionGraphNode>();

            foreach (uint partId in retainedIds)
            {
                VesselTopologyNode source;

                if (!sourceNodes.TryGetValue(partId, out source))
                {
                    continue;
                }

                PropulsionGraphNode node =
                    CreateGraphNode(
                        topology,
                        source);

                result[partId] =
                    node;

                graph.Nodes.Add(node);
            }

            return result;
        }

        private static PropulsionGraphNode CreateGraphNode(
            VesselTopology topology,
            VesselTopologyNode source)
        {
            PropulsionGraphNode result =
                new PropulsionGraphNode
                {
                    PartId = source.PartId,
                    Title = source.PartTitle ?? string.Empty,
                    PartName = source.PartName ?? string.Empty,
                    Category = source.Category,
                    Roles = source.Roles,
                    ActivationStage = source.ActivationStage,
                    SeparationStage = source.SeparationStage,
                    StructuralDepth = source.StructuralDepth,
                    BranchRootPartId = source.BranchRootPartId,
                    SymmetryGroupId = source.SymmetryGroupId,
                    IsRoot =
                        topology.HasRootPart &&
                        source.PartId == topology.RootPartId,
                    IsSeparationBoundary =
                        source.IsSeparationBoundary,
                    SurvivesNextStage =
                        source.SurvivesNextStage,
                    DryMassTonnes =
                        source.DryMassTonnes,
                    ResourceMassTonnes =
                        source.ResourceMassTonnes,
                    VesselX = source.VesselX,
                    VesselY = source.VesselY,
                    VesselZ = source.VesselZ
                };

            for (int index = 0;
                 index < source.Resources.Count;
                 index++)
            {
                AddUnique(
                    result.ResourceNames,
                    source.Resources[index].Name);
            }

            for (int index = 0;
                 index < source.PropellantRequirements.Count;
                 index++)
            {
                VesselPropellantRequirement requirement =
                    source.PropellantRequirements[index];

                AddUnique(
                    result.PropellantNames,
                    requirement.Name);

                for (int sourceIndex = 0;
                     sourceIndex <
                        requirement.ReachableSourcePartIds.Count;
                     sourceIndex++)
                {
                    AddUnique(
                        result.SourcePartIds,
                        requirement
                            .ReachableSourcePartIds[sourceIndex]);
                }
            }

            return result;
        }

        private static void CreateStructuralEdges(
            IDictionary<uint, VesselTopologyNode> sourceNodes,
            ISet<uint> retainedIds,
            IDictionary<uint, PropulsionGraphNode> graphNodes,
            PropulsionRenderGraph graph)
        {
            foreach (uint retainedId in retainedIds)
            {
                VesselTopologyNode node;

                if (!sourceNodes.TryGetValue(
                        retainedId,
                        out node) ||
                    !node.HasParent)
                {
                    continue;
                }

                uint ancestorId =
                    FindNearestRetainedAncestor(
                        node,
                        sourceNodes,
                        retainedIds);

                if (ancestorId == 0 ||
                    ancestorId == node.PartId ||
                    !graphNodes.ContainsKey(ancestorId))
                {
                    continue;
                }

                AddEdge(
                    graph,
                    ancestorId,
                    node.PartId,
                    node.IsSeparationBoundary
                        ? PropulsionGraphEdgeKind.Separation
                        : PropulsionGraphEdgeKind.Structural,
                    string.Empty);
            }
        }

        private static uint FindNearestRetainedAncestor(
            VesselTopologyNode node,
            IDictionary<uint, VesselTopologyNode> nodes,
            ISet<uint> retainedIds)
        {
            uint currentId =
                node.ParentPartId;

            HashSet<uint> visited =
                new HashSet<uint>();

            while (currentId != 0 &&
                   visited.Add(currentId))
            {
                if (retainedIds.Contains(currentId))
                {
                    return currentId;
                }

                VesselTopologyNode current;

                if (!nodes.TryGetValue(
                        currentId,
                        out current) ||
                    !current.HasParent)
                {
                    return 0;
                }

                currentId =
                    current.ParentPartId;
            }

            return 0;
        }

        private static void CreatePropellantEdges(
            IDictionary<uint, VesselTopologyNode> sourceNodes,
            IDictionary<uint, PropulsionGraphNode> graphNodes,
            PropulsionRenderGraph graph)
        {
            foreach (KeyValuePair<uint, PropulsionGraphNode> pair in graphNodes)
            {
                VesselTopologyNode engineNode;

                if (!sourceNodes.TryGetValue(
                        pair.Key,
                        out engineNode))
                {
                    continue;
                }

                for (int requirementIndex = 0;
                     requirementIndex <
                        engineNode.PropellantRequirements.Count;
                     requirementIndex++)
                {
                    VesselPropellantRequirement requirement =
                        engineNode.PropellantRequirements[requirementIndex];

                    for (int sourceIndex = 0;
                         sourceIndex <
                            requirement.ReachableSourcePartIds.Count;
                         sourceIndex++)
                    {
                        uint sourcePartId =
                            requirement
                                .ReachableSourcePartIds[sourceIndex];

                        if (!graphNodes.ContainsKey(sourcePartId) ||
                            sourcePartId == engineNode.PartId)
                        {
                            continue;
                        }

                        AddEdge(
                            graph,
                            sourcePartId,
                            engineNode.PartId,
                            PropulsionGraphEdgeKind.Propellant,
                            requirement.Name);
                    }
                }
            }
        }

        private static void AddEdge(
            PropulsionRenderGraph graph,
            uint fromPartId,
            uint toPartId,
            PropulsionGraphEdgeKind kind,
            string resourceName)
        {
            for (int index = 0;
                 index < graph.Edges.Count;
                 index++)
            {
                PropulsionGraphEdge existing =
                    graph.Edges[index];

                if (existing.FromPartId == fromPartId &&
                    existing.ToPartId == toPartId &&
                    existing.Kind == kind &&
                    string.Equals(
                        existing.ResourceName,
                        resourceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            graph.Edges.Add(
                new PropulsionGraphEdge
                {
                    FromPartId = fromPartId,
                    ToPartId = toPartId,
                    Kind = kind,
                    ResourceName =
                        resourceName ?? string.Empty
                });
        }

        private static void SortGraph(
            PropulsionRenderGraph graph)
        {
            graph.Nodes.Sort(
                delegate(
                    PropulsionGraphNode left,
                    PropulsionGraphNode right)
                {
                    int depthCompare =
                        left.StructuralDepth.CompareTo(
                            right.StructuralDepth);

                    return depthCompare != 0
                        ? depthCompare
                        : left.PartId.CompareTo(
                            right.PartId);
                });

            graph.Edges.Sort(
                delegate(
                    PropulsionGraphEdge left,
                    PropulsionGraphEdge right)
                {
                    int fromCompare =
                        left.FromPartId.CompareTo(
                            right.FromPartId);

                    if (fromCompare != 0)
                    {
                        return fromCompare;
                    }

                    int toCompare =
                        left.ToPartId.CompareTo(
                            right.ToPartId);

                    return toCompare != 0
                        ? toCompare
                        : left.Kind.CompareTo(
                            right.Kind);
                });
        }

        private static void AddUnique(
            IList<string> values,
            string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (string.Equals(
                        values[index],
                        value,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static void AddUnique(
            IList<uint> values,
            uint value)
        {
            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (values[index] == value)
                {
                    return;
                }
            }

            values.Add(value);
        }

        private static bool EqualsResource(
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
