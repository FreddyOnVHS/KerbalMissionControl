using System.Text;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public static class PropulsionRenderGraphDiagnostics
    {
        public static string CreateReport(
            PropulsionRenderGraph graph)
        {
            if (graph == null)
            {
                return "PROPULSION GRAPH UNAVAILABLE";
            }

            StringBuilder builder =
                new StringBuilder();

            builder.AppendFormat(
                "PROPULSION GRAPH vessel=\"{0}\" revision={1} nodes={2} edges={3} collapsed={4}",
                graph.VesselName,
                graph.TopologyRevision,
                graph.Nodes.Count,
                graph.Edges.Count,
                graph.CollapsedPartCount);

            builder.AppendLine();

            for (int index = 0;
                 index < graph.Nodes.Count;
                 index++)
            {
                PropulsionGraphNode node =
                    graph.Nodes[index];

                builder.AppendFormat(
                    "NODE {0} category={1} depth={2} branch={3} activation={4} separation={5} root={6} title=\"{7}\"",
                    node.PartId,
                    node.Category,
                    node.StructuralDepth,
                    node.BranchRootPartId,
                    node.ActivationStage,
                    node.SeparationStage,
                    node.IsRoot ? "YES" : "NO",
                    node.Title);

                builder.AppendLine();
            }

            for (int index = 0;
                 index < graph.Edges.Count;
                 index++)
            {
                PropulsionGraphEdge edge =
                    graph.Edges[index];

                builder.AppendFormat(
                    "EDGE {0}->{1} kind={2} resource={3}",
                    edge.FromPartId,
                    edge.ToPartId,
                    edge.Kind,
                    string.IsNullOrEmpty(edge.ResourceName)
                        ? "---"
                        : edge.ResourceName);

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
