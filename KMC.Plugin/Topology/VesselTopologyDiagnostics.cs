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
                "[KMC] Vessel Topology Phase 1");

            builder.AppendFormat(
                "[KMC] Vessel: {0}  ID: {1}  Revision: {2}",
                string.IsNullOrEmpty(
                    topology.VesselName)
                    ? "---"
                    : topology.VesselName,
                string.IsNullOrEmpty(
                    topology.VesselId)
                    ? "---"
                    : topology.VesselId,
                topology.Revision);

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Parts: {0}  Root: {1}  Max inverse stage: {2}",
                topology.PartCount,
                topology.HasRootPart
                    ? topology.RootPartId
                        .ToString()
                    : "---",
                topology.MaximumInverseStage);

            builder.AppendLine();

            List<VesselTopologyNode> ordered =
                new List<VesselTopologyNode>(
                    topology.Nodes);

            ordered.Sort(
                delegate(
                    VesselTopologyNode left,
                    VesselTopologyNode right)
                {
                    int stageCompare =
                        right.InverseStage.CompareTo(
                            left.InverseStage);

                    return stageCompare != 0
                        ? stageCompare
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
                    "[KMC] Part {0}: stage={1}, parent={2}, attach={3}, children=[{4}], symmetry=[{5}], title=\"{6}\", mass={7:0.000}+{8:0.000}t, pos=({9:0.00},{10:0.00},{11:0.00})",
                    node.PartId,
                    node.InverseStage,
                    node.HasParent
                        ? node.ParentPartId
                            .ToString()
                        : "---",
                    node.AttachmentType,
                    JoinIds(
                        node.ChildPartIds),
                    JoinIds(
                        node.SymmetryPartIds),
                    node.PartTitle,
                    node.DryMassTonnes,
                    node.ResourceMassTonnes,
                    node.VesselX,
                    node.VesselY,
                    node.VesselZ);

                builder.AppendLine();
            }

            return builder.ToString();
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
                    builder.Append(
                        ',');
                }

                builder.Append(
                    values[index]);
            }

            return builder.ToString();
        }
    }
}
