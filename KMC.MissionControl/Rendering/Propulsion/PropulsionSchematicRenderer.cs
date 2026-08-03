using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    public sealed class PropulsionSchematicRenderer
    {
        private readonly PropulsionLayoutEngine _layoutEngine =
            new PropulsionLayoutEngine();

        public void Draw(
            Graphics graphics,
            Rectangle bounds,
            PropulsionRenderGraph graph,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            if (graphics == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            GraphicsState state =
                graphics.Save();

            try
            {
                graphics.SmoothingMode =
                    SmoothingMode.AntiAlias;

                DrawBackground(
                    graphics,
                    bounds,
                    dimPhosphor);

                if (graph == null ||
                    graph.Nodes.Count == 0)
                {
                    DrawNoLink(
                        graphics,
                        bounds,
                        labelFont,
                        dimPhosphor);
                    return;
                }

                Rectangle layoutBounds =
                    Rectangle.Inflate(
                        bounds,
                        -18,
                        -18);

                PropulsionLayout layout =
                    _layoutEngine.Build(
                        graph,
                        layoutBounds);

                DrawEdges(
                    graphics,
                    graph,
                    layout,
                    dimPhosphor);

                foreach (PropulsionGraphNode node
                    in graph.Nodes)
                {
                    PropulsionLayoutNode layoutNode;

                    if (layout.Nodes.TryGetValue(
                            node.PartId,
                            out layoutNode))
                    {
                        DrawNode(
                            graphics,
                            layoutNode,
                            labelFont,
                            smallFont,
                            phosphor,
                            dimPhosphor);
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawBackground(
            Graphics graphics,
            Rectangle bounds,
            Color dimPhosphor)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        48,
                        2,
                        13,
                        18)))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        25,
                        dimPhosphor),
                    1.0f))
            {
                graphics.FillRectangle(
                    brush,
                    bounds);

                for (int x = bounds.Left;
                     x < bounds.Right;
                     x += 32)
                {
                    graphics.DrawLine(
                        gridPen,
                        x,
                        bounds.Top,
                        x,
                        bounds.Bottom);
                }

                for (int y = bounds.Top;
                     y < bounds.Bottom;
                     y += 32)
                {
                    graphics.DrawLine(
                        gridPen,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private static void DrawNoLink(
            Graphics graphics,
            Rectangle bounds,
            Font font,
            Color color)
        {
            using (SolidBrush brush =
                new SolidBrush(color))
            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                graphics.DrawString(
                    "AWAITING VESSEL TOPOLOGY",
                    font,
                    brush,
                    bounds,
                    format);
            }
        }

        private static void DrawEdges(
            Graphics graphics,
            PropulsionRenderGraph graph,
            PropulsionLayout layout,
            Color dimPhosphor)
        {
            foreach (PropulsionGraphEdge edge
                in graph.Edges)
            {
                PropulsionLayoutNode from;
                PropulsionLayoutNode to;

                if (!layout.Nodes.TryGetValue(
                        edge.FromPartId,
                        out from) ||
                    !layout.Nodes.TryGetValue(
                        edge.ToPartId,
                        out to))
                {
                    continue;
                }

                Color color =
                    GetEdgeColor(
                        edge,
                        dimPhosphor);

                using (Pen pen =
                    new Pen(
                        color,
                        edge.Kind ==
                            PropulsionGraphEdgeKind
                                .Propellant
                            ? 2.0f
                            : 1.25f))
                {
                    if (edge.Kind ==
                        PropulsionGraphEdgeKind
                            .Separation)
                    {
                        pen.DashStyle =
                            DashStyle.Dash;
                    }
                    else if (edge.Kind ==
                             PropulsionGraphEdgeKind
                                 .Propellant)
                    {
                        pen.EndCap =
                            LineCap.ArrowAnchor;
                    }

                    PointF start =
                        new PointF(
                            from.Center.X,
                            from.Bounds.Bottom);

                    PointF end =
                        new PointF(
                            to.Center.X,
                            to.Bounds.Top);

                    float middleY =
                        (start.Y + end.Y) /
                        2.0f;

                    graphics.DrawLines(
                        pen,
                        new[]
                        {
                            start,
                            new PointF(
                                start.X,
                                middleY),
                            new PointF(
                                end.X,
                                middleY),
                            end
                        });
                }
            }
        }

        private static Color GetEdgeColor(
            PropulsionGraphEdge edge,
            Color dimPhosphor)
        {
            if (edge.Kind ==
                PropulsionGraphEdgeKind.Separation)
            {
                return Color.FromArgb(
                    220,
                    255,
                    190,
                    60);
            }

            if (edge.Kind ==
                PropulsionGraphEdgeKind.Propellant)
            {
                if (string.Equals(
                        edge.ResourceName,
                        "Oxidizer",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    return Color.FromArgb(
                        230,
                        40,
                        210,
                        255);
                }

                if (string.Equals(
                        edge.ResourceName,
                        "MonoPropellant",
                        StringComparison
                            .OrdinalIgnoreCase))
                {
                    return Color.FromArgb(
                        230,
                        255,
                        145,
                        30);
                }

                return Color.FromArgb(
                    230,
                    40,
                    230,
                    80);
            }

            return Color.FromArgb(
                170,
                dimPhosphor);
        }

        private static void DrawNode(
            Graphics graphics,
            PropulsionLayoutNode layoutNode,
            Font labelFont,
            Font smallFont,
            Color phosphor,
            Color dimPhosphor)
        {
            PropulsionGraphNode node =
                layoutNode.Node;

            RectangleF bounds =
                layoutNode.Bounds;

            Color accent =
                GetNodeColor(
                    node.Category,
                    phosphor);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        185,
                        3,
                        16,
                        22)))
            using (Pen border =
                new Pen(accent, 1.6f))
            using (SolidBrush titleBrush =
                new SolidBrush(accent))
            using (SolidBrush detailBrush =
                new SolidBrush(dimPhosphor))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center,
                    Trimming =
                        StringTrimming.EllipsisCharacter
                })
            {
                if (node.Category ==
                    VesselNodeCategory.Engine)
                {
                    PointF[] bell =
                    {
                        new PointF(
                            bounds.Left + 10,
                            bounds.Top + 4),
                        new PointF(
                            bounds.Right - 10,
                            bounds.Top + 4),
                        new PointF(
                            bounds.Right - 2,
                            bounds.Bottom - 4),
                        new PointF(
                            bounds.Left + 2,
                            bounds.Bottom - 4)
                    };

                    graphics.FillPolygon(
                        fill,
                        bell);

                    graphics.DrawPolygon(
                        border,
                        bell);
                }
                else if (node.Category ==
                         VesselNodeCategory.Decoupler ||
                         node.IsSeparationBoundary)
                {
                    graphics.FillRectangle(
                        fill,
                        bounds);

                    border.DashStyle =
                        DashStyle.Dash;

                    graphics.DrawRectangle(
                        border,
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height);
                }
                else
                {
                    graphics.FillRectangle(
                        fill,
                        bounds);

                    graphics.DrawRectangle(
                        border,
                        bounds.X,
                        bounds.Y,
                        bounds.Width,
                        bounds.Height);
                }

                RectangleF titleBounds =
                    new RectangleF(
                        bounds.Left + 3,
                        bounds.Top + 4,
                        bounds.Width - 6,
                        22);

                graphics.DrawString(
                    GetShortTitle(node),
                    labelFont,
                    titleBrush,
                    titleBounds,
                    centered);

                RectangleF detailBounds =
                    new RectangleF(
                        bounds.Left + 3,
                        bounds.Top + 25,
                        bounds.Width - 6,
                        17);

                graphics.DrawString(
                    GetNodeDetail(node),
                    smallFont,
                    detailBrush,
                    detailBounds,
                    centered);
            }
        }

        private static string GetShortTitle(
            PropulsionGraphNode node)
        {
            switch (node.Category)
            {
                case VesselNodeCategory.Command:
                    return "COMMAND";

                case VesselNodeCategory.Engine:
                    return "ENGINE";

                case VesselNodeCategory.FuelTank:
                    return "TANK";

                case VesselNodeCategory.Decoupler:
                    return "DECOUPLER";

                case VesselNodeCategory.RcsThruster:
                    return "RCS";

                case VesselNodeCategory.Battery:
                    return "BATTERY";

                case VesselNodeCategory.DockingPort:
                    return "DOCK";

                case VesselNodeCategory.SolarPanel:
                    return "SOLAR";

                case VesselNodeCategory.Generator:
                    return "POWER";

                default:
                    return node.Category.ToString()
                        .ToUpperInvariant();
            }
        }

        private static string GetNodeDetail(
            PropulsionGraphNode node)
        {
            if (node.Category ==
                VesselNodeCategory.Engine &&
                node.ActivationStage >= 0)
            {
                return "STG " +
                    node.ActivationStage
                        .ToString("00");
            }

            if (node.IsSeparationBoundary &&
                node.SeparationStage >= 0)
            {
                return "SEP " +
                    node.SeparationStage
                        .ToString("00");
            }

            if (node.ResourceNames.Count > 0)
            {
                return node.ResourceNames[0]
                    .ToUpperInvariant();
            }

            return node.PartId.ToString();
        }

        private static Color GetNodeColor(
            VesselNodeCategory category,
            Color defaultColor)
        {
            switch (category)
            {
                case VesselNodeCategory.Engine:
                    return Color.FromArgb(
                        255,
                        80,
                        255,
                        120);

                case VesselNodeCategory.FuelTank:
                    return Color.FromArgb(
                        255,
                        50,
                        220,
                        255);

                case VesselNodeCategory.Decoupler:
                    return Color.FromArgb(
                        255,
                        255,
                        190,
                        60);

                case VesselNodeCategory.RcsThruster:
                    return Color.FromArgb(
                        255,
                        255,
                        145,
                        40);

                case VesselNodeCategory.Battery:
                case VesselNodeCategory.Generator:
                case VesselNodeCategory.SolarPanel:
                    return Color.FromArgb(
                        255,
                        245,
                        220,
                        60);

                case VesselNodeCategory.Command:
                    return Color.White;

                default:
                    return defaultColor;
            }
        }
    }
}
