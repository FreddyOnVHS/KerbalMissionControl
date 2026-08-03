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
                "[KMC] Vessel Topology Phase 2A");

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
                    ? topology.RootPartId.ToString()
                    : "---",
                topology.MaximumInverseStage);

            builder.AppendLine();

            AppendCategorySummary(
                builder,
                topology.Nodes);

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
                    "[KMC] Part {0}: category={1}, roles=[{2}], resources=[{3}], stage={4}, parent={5}, attach={6}, children=[{7}], symmetry=[{8}], title=\"{9}\", mass={10:0.000}+{11:0.000}t, pos=({12:0.00},{13:0.00},{14:0.00})",
                    node.PartId,
                    node.Category,
                    FormatRoles(
                        node.Roles),
                    JoinStrings(
                        node.StoredResourceNames),
                    node.InverseStage,
                    node.HasParent
                        ? node.ParentPartId.ToString()
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

        private static void AppendCategorySummary(
            StringBuilder builder,
            IList<VesselTopologyNode> nodes)
        {
            Dictionary<VesselNodeCategory, int> counts =
                new Dictionary<VesselNodeCategory, int>();

            for (int index = 0;
                 index < nodes.Count;
                 index++)
            {
                VesselNodeCategory category =
                    nodes[index].Category;

                int count;

                counts.TryGetValue(
                    category,
                    out count);

                counts[category] =
                    count + 1;
            }

            builder.Append(
                "[KMC] Categories:");

            Array categories =
                Enum.GetValues(
                    typeof(VesselNodeCategory));

            for (int index = 0;
                 index < categories.Length;
                 index++)
            {
                VesselNodeCategory category =
                    (VesselNodeCategory)
                    categories.GetValue(index);

                int count;

                if (!counts.TryGetValue(
                        category,
                        out count) ||
                    count <= 0)
                {
                    continue;
                }

                builder.AppendFormat(
                    " {0}={1}",
                    category,
                    count);
            }

            builder.AppendLine();
        }

        private static string FormatRoles(
            VesselNodeRole roles)
        {
            return roles == VesselNodeRole.None
                ? string.Empty
                : roles.ToString();
        }

        private static string JoinStrings(
            IList<string> values)
        {
            if (values == null ||
                values.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                ",",
                new List<string>(
                    values).ToArray());
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
