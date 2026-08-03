using System;
using System.Collections.Generic;
using System.Text;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    internal static class VesselTopologyDiagnostics
    {
        public static string CreateReport(
            VesselTopology topology)
        {
            if (topology == null)
            {
                return "[KMC] Vessel topology unavailable.";
            }

            StringBuilder builder =
                new StringBuilder();

            builder.AppendLine(
                "[KMC] Vessel Topology Phase 2B");

            builder.AppendFormat(
                "[KMC] Vessel: {0}  Revision: {1}  Parts: {2}",
                topology.VesselName,
                topology.Revision,
                topology.PartCount);

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Root: {0}  Current stage: {1}  Next stage: {2}  Branches: {3}  Symmetry groups: {4}  Separation boundaries: {5}",
                topology.HasRootPart
                    ? topology.RootPartId.ToString()
                    : "---",
                topology.CurrentStage,
                topology.NextStage,
                topology.StructuralBranchCount,
                topology.SymmetryGroupCount,
                topology.SeparationBoundaryCount);

            builder.AppendLine();

            List<VesselTopologyNode> ordered =
                new List<VesselTopologyNode>(
                    topology.Nodes);

            ordered.Sort(
                delegate(
                    VesselTopologyNode left,
                    VesselTopologyNode right)
                {
                    int depthCompare =
                        left.StructuralDepth.CompareTo(
                            right.StructuralDepth);

                    return depthCompare != 0
                        ? depthCompare
                        : left.PartId.CompareTo(
                            right.PartId);
                });

            for (int index = 0;
                 index < ordered.Count;
                 index++)
            {
                VesselTopologyNode node =
                    ordered[index];

                builder.AppendFormat(
                    "[KMC] Part {0}: category={1}, depth={2}, branch={3}, parent={4}, attach={5}, stackChildren=[{6}], surfaceChildren=[{7}], symmetryGroup={8}, activationStage={9}, separationStage={10}, boundary={11}, survivesNext={12}, title=\"{13}\"",
                    node.PartId,
                    node.Category,
                    node.StructuralDepth,
                    node.BranchRootPartId,
                    node.HasParent
                        ? node.ParentPartId.ToString()
                        : "---",
                    node.AttachmentType,
                    JoinIds(
                        node.StackChildPartIds),
                    JoinIds(
                        node.SurfaceChildPartIds),
                    node.SymmetryGroupId == 0
                        ? "---"
                        : node.SymmetryGroupId.ToString(),
                    FormatStage(
                        node.ActivationStage),
                    FormatStage(
                        node.SeparationStage),
                    node.IsSeparationBoundary
                        ? "YES"
                        : "NO",
                    node.SurvivesNextStage
                        ? "YES"
                        : "NO",
                    node.PartTitle);

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string FormatStage(
            int stage)
        {
            return stage >= 0
                ? stage.ToString("00")
                : "--";
        }

        private static string JoinIds(
            IList<uint> values)
        {
            if (values == null ||
                values.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder =
                new StringBuilder();

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(
                    values[index]);
            }

            return builder.ToString();
        }
    }
}
