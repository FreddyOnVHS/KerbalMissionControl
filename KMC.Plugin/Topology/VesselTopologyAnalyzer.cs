using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Derives renderer-ready connectivity and staging metadata from the raw
    /// vessel parent-child graph.
    /// </summary>
    internal static class VesselTopologyAnalyzer
    {
        public static void Analyze(
            VesselTopology topology)
        {
            if (topology == null ||
                topology.Nodes == null ||
                topology.Nodes.Count == 0)
            {
                return;
            }

            Dictionary<uint, VesselTopologyNode> nodes =
                BuildNodeMap(
                    topology.Nodes);

            SplitChildConnections(
                topology.Nodes,
                nodes);

            AssignSymmetryGroups(
                topology,
                nodes);

            AssignStructureAndStaging(
                topology,
                nodes);
        }

        private static Dictionary<uint, VesselTopologyNode> BuildNodeMap(
            IList<VesselTopologyNode> source)
        {
            Dictionary<uint, VesselTopologyNode> result =
                new Dictionary<uint, VesselTopologyNode>();

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

        private static void SplitChildConnections(
            IList<VesselTopologyNode> source,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            for (int index = 0;
                 index < source.Count;
                 index++)
            {
                VesselTopologyNode parent =
                    source[index];

                parent.StackChildPartIds.Clear();
                parent.SurfaceChildPartIds.Clear();

                for (int childIndex = 0;
                     childIndex < parent.ChildPartIds.Count;
                     childIndex++)
                {
                    VesselTopologyNode child;

                    if (!nodes.TryGetValue(
                            parent.ChildPartIds[childIndex],
                            out child))
                    {
                        continue;
                    }

                    IList<uint> target =
                        child.AttachmentType ==
                            VesselAttachmentType.Surface
                            ? parent.SurfaceChildPartIds
                            : parent.StackChildPartIds;

                    AddUnique(
                        target,
                        child.PartId);
                }
            }
        }

        private static void AssignSymmetryGroups(
            VesselTopology topology,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            HashSet<uint> visited =
                new HashSet<uint>();

            int groupCount =
                0;

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode seed =
                    topology.Nodes[index];

                if (visited.Contains(
                        seed.PartId) ||
                    seed.SymmetryPartIds.Count == 0)
                {
                    continue;
                }

                List<VesselTopologyNode> group =
                    CollectSymmetryGroup(
                        seed,
                        nodes);

                if (group.Count <= 1)
                {
                    continue;
                }

                uint representative =
                    group[0].PartId;

                for (int memberIndex = 1;
                     memberIndex < group.Count;
                     memberIndex++)
                {
                    representative =
                        Math.Min(
                            representative,
                            group[memberIndex].PartId);
                }

                for (int memberIndex = 0;
                     memberIndex < group.Count;
                     memberIndex++)
                {
                    VesselTopologyNode member =
                        group[memberIndex];

                    member.SymmetryGroupId =
                        representative;

                    visited.Add(
                        member.PartId);
                }

                groupCount++;
            }

            topology.SymmetryGroupCount =
                groupCount;
        }

        private static List<VesselTopologyNode> CollectSymmetryGroup(
            VesselTopologyNode seed,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            List<VesselTopologyNode> result =
                new List<VesselTopologyNode>();

            Queue<uint> pending =
                new Queue<uint>();

            HashSet<uint> visited =
                new HashSet<uint>();

            pending.Enqueue(
                seed.PartId);

            while (pending.Count > 0)
            {
                uint partId =
                    pending.Dequeue();

                if (!visited.Add(
                        partId))
                {
                    continue;
                }

                VesselTopologyNode node;

                if (!nodes.TryGetValue(
                        partId,
                        out node))
                {
                    continue;
                }

                result.Add(
                    node);

                for (int index = 0;
                     index < node.SymmetryPartIds.Count;
                     index++)
                {
                    pending.Enqueue(
                        node.SymmetryPartIds[index]);
                }
            }

            return result;
        }

        private static void AssignStructureAndStaging(
            VesselTopology topology,
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            VesselTopologyNode root;

            if (!topology.HasRootPart ||
                !nodes.TryGetValue(
                    topology.RootPartId,
                    out root))
            {
                return;
            }

            int nextStage =
                topology.NextStage;

            int branchCount =
                root.ChildPartIds.Count;

            topology.StructuralBranchCount =
                Math.Max(
                    1,
                    branchCount);

            topology.SeparationBoundaryCount =
                0;

            Traverse(
                topology,
                nodes,
                root,
                0,
                root.PartId,
                -1,
                nextStage,
                new HashSet<uint>());
        }

        private static void Traverse(
            VesselTopology topology,
            IDictionary<uint, VesselTopologyNode> nodes,
            VesselTopologyNode node,
            int depth,
            uint branchRootPartId,
            int inheritedSeparationStage,
            int nextStage,
            ISet<uint> visited)
        {
            if (node == null ||
                !visited.Add(
                    node.PartId))
            {
                return;
            }

            node.StructuralDepth =
                depth;

            node.BranchRootPartId =
                branchRootPartId;

            node.IsSeparationBoundary =
                node.HasRole(
                    VesselNodeRole.Decoupler) ||
                node.HasRole(
                    VesselNodeRole.Separator);

            if (node.IsSeparationBoundary)
            {
                topology.SeparationBoundaryCount++;
            }

            node.ActivationStage =
                HasStagedActivation(
                    node)
                    ? node.InverseStage
                    : -1;

            int separationStage =
                inheritedSeparationStage;

            if (node.IsSeparationBoundary)
            {
                separationStage =
                    node.InverseStage;
            }

            node.SeparationStage =
                separationStage;

            node.WillSeparateOnNextStage =
                separationStage >= 0 &&
                separationStage ==
                    nextStage;

            for (int index = 0;
                 index < node.ChildPartIds.Count;
                 index++)
            {
                VesselTopologyNode child;

                if (!nodes.TryGetValue(
                        node.ChildPartIds[index],
                        out child))
                {
                    continue;
                }

                uint childBranchRoot =
                    depth == 0
                        ? child.PartId
                        : branchRootPartId;

                Traverse(
                    topology,
                    nodes,
                    child,
                    depth + 1,
                    childBranchRoot,
                    separationStage,
                    nextStage,
                    visited);
            }
        }

        private static bool HasStagedActivation(
            VesselTopologyNode node)
        {
            return
                node.HasRole(
                    VesselNodeRole.Engine) ||
                node.HasRole(
                    VesselNodeRole.Decoupler) ||
                node.HasRole(
                    VesselNodeRole.Separator) ||
                node.HasRole(
                    VesselNodeRole.Fairing);
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
