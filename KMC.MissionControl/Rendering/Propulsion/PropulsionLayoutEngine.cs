using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Deterministic engineering-schematic layout.
    /// Vertical placement follows structural depth. Horizontal placement is
    /// seeded by KSP vessel-space X, then spread to prevent overlap.
    /// </summary>
    public sealed class PropulsionLayoutEngine
    {
        private const float MinimumNodeWidth = 72.0f;
        private const float MaximumNodeWidth = 112.0f;
        private const float NodeHeight = 46.0f;
        private const float HorizontalGap = 16.0f;
        private const float VerticalGap = 34.0f;

        public PropulsionLayout Build(
            PropulsionRenderGraph graph,
            Rectangle bounds)
        {
            PropulsionLayout result =
                new PropulsionLayout();

            if (graph == null ||
                graph.Nodes.Count == 0 ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return result;
            }

            int maximumDepth =
                Math.Max(
                    1,
                    graph.Nodes.Max(
                        node => node.StructuralDepth));

            float nodeWidth =
                Math.Max(
                    MinimumNodeWidth,
                    Math.Min(
                        MaximumNodeWidth,
                        bounds.Width / 5.5f));

            float usableHeight =
                Math.Max(
                    NodeHeight,
                    bounds.Height - NodeHeight);

            float depthSpacing =
                Math.Max(
                    NodeHeight + 8.0f,
                    Math.Min(
                        NodeHeight + VerticalGap,
                        usableHeight /
                        Math.Max(1, maximumDepth)));

            Dictionary<int, List<PropulsionGraphNode>> levels =
                graph.Nodes
                    .GroupBy(node => node.StructuralDepth)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderBy(node => node.VesselX)
                            .ThenBy(node => node.PartId)
                            .ToList());

            foreach (KeyValuePair<int, List<PropulsionGraphNode>> level
                in levels.OrderBy(pair => pair.Key))
            {
                List<PropulsionGraphNode> nodes =
                    level.Value;

                float totalWidth =
                    nodes.Count * nodeWidth +
                    Math.Max(0, nodes.Count - 1) *
                    HorizontalGap;

                float startX =
                    bounds.Left +
                    (bounds.Width - totalWidth) /
                    2.0f;

                if (totalWidth > bounds.Width)
                {
                    nodeWidth =
                        Math.Max(
                            44.0f,
                            (bounds.Width -
                             HorizontalGap *
                             Math.Max(0, nodes.Count - 1)) /
                            Math.Max(1, nodes.Count));

                    totalWidth =
                        nodes.Count * nodeWidth +
                        Math.Max(0, nodes.Count - 1) *
                        HorizontalGap;

                    startX =
                        bounds.Left +
                        (bounds.Width - totalWidth) /
                        2.0f;
                }

                float y =
                    bounds.Top +
                    level.Key * depthSpacing;

                for (int index = 0;
                     index < nodes.Count;
                     index++)
                {
                    PropulsionGraphNode node =
                        nodes[index];

                    result.Nodes[node.PartId] =
                        new PropulsionLayoutNode
                        {
                            Node = node,
                            Bounds =
                                new RectangleF(
                                    startX +
                                    index *
                                    (nodeWidth +
                                     HorizontalGap),
                                    y,
                                    nodeWidth,
                                    NodeHeight)
                        };
                }
            }

            return result;
        }
    }
}
