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

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("[KMC] Vessel Topology Phase 2C");
            builder.AppendFormat(
                "[KMC] Vessel: {0}  Revision: {1}  Parts: {2}  Current stage: {3}  Next stage: {4}",
                topology.VesselName,
                topology.Revision,
                topology.PartCount,
                topology.CurrentStage,
                topology.NextStage);
            builder.AppendLine();

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node = topology.Nodes[index];

                builder.AppendFormat(
                    "[KMC] Part {0}: category={1}, depth={2}, branch={3}, activation={4}, separation={5}, crossfeed={6}, resources=[{7}], propellants=[{8}], title=\"{9}\"",
                    node.PartId,
                    node.Category,
                    node.StructuralDepth,
                    node.BranchRootPartId,
                    FormatStage(node.ActivationStage),
                    FormatStage(node.SeparationStage),
                    node.AllowsCrossFeed ? "YES" : "NO",
                    FormatResources(node.Resources),
                    FormatPropellants(node.PropellantRequirements),
                    node.PartTitle);
                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static string FormatResources(
            IList<VesselResourceState> resources)
        {
            if (resources == null ||
                resources.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            for (int index = 0;
                 index < resources.Count;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append("; ");
                }

                VesselResourceState resource = resources[index];

                builder.AppendFormat(
                    "{0}={1:0.###}/{2:0.###} ({3:0}%) flow={4}",
                    resource.Name,
                    resource.Amount,
                    resource.Capacity,
                    resource.FillFraction * 100.0,
                    resource.FlowEnabled ? "ON" : "OFF");
            }

            return builder.ToString();
        }

        private static string FormatPropellants(
            IList<VesselPropellantRequirement> requirements)
        {
            if (requirements == null ||
                requirements.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();

            for (int index = 0;
                 index < requirements.Count;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append("; ");
                }

                VesselPropellantRequirement requirement =
                    requirements[index];

                builder.AppendFormat(
                    "{0} ratio={1:0.###} mode={2} sources=[{3}]",
                    requirement.Name,
                    requirement.Ratio,
                    requirement.RawFlowMode,
                    JoinIds(requirement.ReachableSourcePartIds));
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

            StringBuilder builder = new StringBuilder();

            for (int index = 0;
                 index < values.Count;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder.Append(values[index]);
            }

            return builder.ToString();
        }
    }
}
