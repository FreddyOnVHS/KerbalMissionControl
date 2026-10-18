using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Telemetry;
using KMC.Shared.Topology;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Pages
{
    public sealed class SchematicPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const int HeaderOffset = 70;
        private const int Gap = 14;
        private const int PanelHeaderHeight = 34;

        public string Name
        {
            get { return "VESSEL SCHEMATIC"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get { return Size.Empty; }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return MissionPageContentProfile.DenseEngineering;
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            MissionPageLayout layout =
                new MissionPageLayout(
                    context);

            layout.DrawHeader(
                Name,
                "VEHICLE / STRUCTURE");

            Rectangle content =
                context.ContentBounds;

            int top =
                content.Top +
                HeaderOffset;

            int sideWidth =
                Math.Max(
                    260,
                    (int)(content.Width * 0.22));

            Rectangle viewBounds =
                new Rectangle(
                    content.Left,
                    top,
                    Math.Max(
                        1,
                        content.Width -
                        sideWidth -
                        Gap),
                    Math.Max(
                        1,
                        content.Bottom -
                        top -
                        4));

            Rectangle statusBounds =
                new Rectangle(
                    viewBounds.Right +
                    Gap,
                    top,
                    Math.Max(
                        1,
                        content.Right -
                        viewBounds.Right -
                        Gap),
                    viewBounds.Height);

            DrawPanelFrame(
                context,
                viewBounds,
                "ACTIVE VESSEL / SIDE");

            DrawPanelFrame(
                context,
                statusBounds,
                "SCHEMATIC STATUS");

            VesselSchematicSnapshot snapshot =
                VesselSchematicTelemetryStore.GetLatest();

            if (snapshot == null ||
                snapshot.Topology == null ||
                snapshot.Topology.Nodes == null ||
                snapshot.Topology.Nodes.Count == 0)
            {
                DrawWaiting(
                    context,
                    viewBounds);

                DrawStatus(
                    context,
                    statusBounds,
                    null,
                    DateTime.MinValue,
                    0,
                    0);

                return;
            }

            int outlineParts;
            int fallbackParts;

            DrawVessel(
                context,
                viewBounds,
                snapshot.Topology,
                out outlineParts,
                out fallbackParts);

            DrawStatus(
                context,
                statusBounds,
                snapshot.Topology,
                snapshot.ReceivedUtc,
                outlineParts,
                fallbackParts);
        }

        private static void DrawVessel(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            out int outlineParts,
            out int fallbackParts)
        {
            outlineParts = 0;
            fallbackParts = 0;

            Rectangle drawing =
                Rectangle.FromLTRB(
                    bounds.Left + 34,
                    bounds.Top +
                        PanelHeaderHeight +
                        24,
                    bounds.Right - 34,
                    bounds.Bottom - 30);

            double minH =
                double.PositiveInfinity;

            double maxH =
                double.NegativeInfinity;

            double minV =
                double.PositiveInfinity;

            double maxV =
                double.NegativeInfinity;

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                if (node.SideOutlinePoints != null &&
                    node.SideOutlinePoints.Count >= 3)
                {
                    outlineParts++;

                    for (int pointIndex = 0;
                         pointIndex < node.SideOutlinePoints.Count;
                         pointIndex++)
                    {
                        VesselOutlinePoint point =
                            node.SideOutlinePoints[pointIndex];

                        Expand(
                            point.Horizontal,
                            point.Vertical,
                            ref minH,
                            ref maxH,
                            ref minV,
                            ref maxV);
                    }
                }
                else
                {
                    fallbackParts++;

                    Expand(
                        node.VesselZ,
                        node.VesselY,
                        ref minH,
                        ref maxH,
                        ref minV,
                        ref maxV);
                }
            }

            if (!IsFinite(minH) ||
                !IsFinite(maxH) ||
                !IsFinite(minV) ||
                !IsFinite(maxV))
            {
                DrawWaiting(
                    context,
                    bounds);

                return;
            }

            double rangeH =
                Math.Max(
                    0.5,
                    maxH - minH);

            double rangeV =
                Math.Max(
                    0.5,
                    maxV - minV);

            double scale =
                Math.Min(
                    drawing.Width /
                        (rangeH * 1.08),
                    drawing.Height /
                        (rangeV * 1.08));

            double centerH =
                (minH + maxH) /
                2.0;

            double centerV =
                (minV + maxV) /
                2.0;

            float screenCenterX =
                drawing.Left +
                drawing.Width /
                2.0f;

            float screenCenterY =
                drawing.Top +
                drawing.Height /
                2.0f;

            context.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            for (int nodeIndex = 0;
                 nodeIndex < topology.Nodes.Count;
                 nodeIndex++)
            {
                VesselTopologyNode node =
                    topology.Nodes[nodeIndex];

                if (node == null)
                {
                    continue;
                }

                bool isRoot =
                    topology.HasRootPart &&
                    node.PartId ==
                        topology.RootPartId;

                if (node.SideOutlinePoints != null &&
                    node.SideOutlinePoints.Count >= 3)
                {
                    PointF[] polygon =
                        new PointF[
                            node.SideOutlinePoints.Count];

                    for (int pointIndex = 0;
                         pointIndex < node.SideOutlinePoints.Count;
                         pointIndex++)
                    {
                        VesselOutlinePoint point =
                            node.SideOutlinePoints[pointIndex];

                        polygon[pointIndex] =
                            ToScreen(
                                point.Horizontal,
                                point.Vertical,
                                centerH,
                                centerV,
                                scale,
                                screenCenterX,
                                screenCenterY);
                    }

                    DrawPartOutline(
                        context,
                        polygon,
                        node,
                        isRoot);
                }
                else
                {
                    PointF center =
                        ToScreen(
                            node.VesselZ,
                            node.VesselY,
                            centerH,
                            centerV,
                            scale,
                            screenCenterX,
                            screenCenterY);

                    DrawFallbackGlyph(
                        context,
                        center,
                        node,
                        isRoot);
                }
            }
        }

        private static void DrawPartOutline(
            MissionRenderContext context,
            PointF[] polygon,
            VesselTopologyNode node,
            bool isRoot)
        {
            if (polygon == null ||
                polygon.Length < 3 ||
                node == null)
            {
                return;
            }

            RectangleF visualBounds =
                GetPolygonBounds(
                    polygon);

            if (ShouldSuppressMinorPart(
                    node,
                    visualBounds))
            {
                return;
            }

            if (node.Category ==
                VesselNodeCategory.Engine)
            {
                DrawEngineBell(
                    context,
                    visualBounds,
                    isRoot);

                return;
            }

            bool primaryHull =
                IsPrimaryHullPart(
                    node);

            bool major =
                IsMajorPart(
                    node);

            bool minor =
                IsMinorPart(
                    node);

            Color outlineColor =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            /*
             * Visual hierarchy:
             * - primary hull establishes the spacecraft silhouette
             * - important subsystem parts remain readable but secondary
             * - small/internal details recede into the background
             */
            int fillAlpha =
                isRoot
                    ? 44
                    : primaryHull
                        ? 38
                        : major
                            ? 20
                            : minor
                                ? 2
                                : 6;

            int lineAlpha =
                isRoot
                    ? 255
                    : primaryHull
                        ? 245
                        : major
                            ? 195
                            : minor
                                ? 58
                                : 115;

            float lineWidth =
                isRoot
                    ? 2.35f
                    : primaryHull
                        ? 2.0f
                        : major
                            ? 1.4f
                            : minor
                                ? 0.70f
                                : 0.95f;

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        fillAlpha,
                        outlineColor)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        lineAlpha,
                        outlineColor),
                    lineWidth))
            {
                context.Graphics.FillPolygon(
                    fill,
                    polygon);

                context.Graphics.DrawPolygon(
                    pen,
                    polygon);
            }
        }

        private static void DrawEngineBell(
            MissionRenderContext context,
            RectangleF bounds,
            bool isRoot)
        {
            if (bounds.Width <= 0.0f ||
                bounds.Height <= 0.0f)
            {
                return;
            }

            /*
             * Schematic convention: render liquid-engine parts as a clean
             * bell/nozzle rather than exposing a boxy convex hull. The bell
             * stays inside the real projected engine bounds, so positioning
             * and overall scale still come from live vessel geometry.
             */
            float centerX =
                bounds.Left +
                bounds.Width * 0.5f;

            /*
             * Keep the bell inside the live engine envelope but use only the
             * central portion of that envelope. The raw engine mesh bounds
             * can include mount/housing geometry that made the schematic bell
             * look oversized.
             *
             * Also bias the bell upward so its throat visually attaches to
             * the engine mount instead of floating below the spacecraft.
             */
            float bellHeight =
                bounds.Height * 0.74f;

            float topY =
                bounds.Top -
                bounds.Height * 0.04f;

            float bottomY =
                topY +
                bellHeight;

            float throatY =
                topY +
                bellHeight * 0.28f;

            float topHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.16f);

            float throatHalfWidth =
                Math.Max(
                    2.0f,
                    bounds.Width * 0.22f);

            float exitHalfWidth =
                Math.Max(
                    3.0f,
                    bounds.Width * 0.34f);

            PointF[] bell =
            {
                new PointF(
                    centerX - topHalfWidth,
                    topY),

                new PointF(
                    centerX + topHalfWidth,
                    topY),

                new PointF(
                    centerX + throatHalfWidth,
                    throatY),

                new PointF(
                    centerX + exitHalfWidth,
                    bottomY),

                new PointF(
                    centerX - exitHalfWidth,
                    bottomY),

                new PointF(
                    centerX - throatHalfWidth,
                    throatY)
            };

            Color color =
                isRoot
                    ? Color.White
                    : context.PhosphorColor;

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        26,
                        color)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        isRoot
                            ? 245
                            : 235,
                        color),
                    isRoot
                        ? 2.2f
                        : 1.8f))
            {
                context.Graphics.FillPolygon(
                    fill,
                    bell);

                context.Graphics.DrawPolygon(
                    pen,
                    bell);

                // Simple nozzle-exit line makes the bell read immediately.
                context.Graphics.DrawLine(
                    pen,
                    centerX - exitHalfWidth,
                    bottomY,
                    centerX + exitHalfWidth,
                    bottomY);
            }
        }

        private static RectangleF GetPolygonBounds(
            PointF[] polygon)
        {
            float minX =
                float.PositiveInfinity;

            float maxX =
                float.NegativeInfinity;

            float minY =
                float.PositiveInfinity;

            float maxY =
                float.NegativeInfinity;

            for (int index = 0;
                 index < polygon.Length;
                 index++)
            {
                minX =
                    Math.Min(
                        minX,
                        polygon[index].X);

                maxX =
                    Math.Max(
                        maxX,
                        polygon[index].X);

                minY =
                    Math.Min(
                        minY,
                        polygon[index].Y);

                maxY =
                    Math.Max(
                        maxY,
                        polygon[index].Y);
            }

            return
                RectangleF.FromLTRB(
                    minX,
                    minY,
                    maxX,
                    maxY);
        }

        private static bool ShouldSuppressMinorPart(
            VesselTopologyNode node,
            RectangleF bounds)
        {
            if (node == null ||
                !IsMinorPart(node))
            {
                return false;
            }

            /*
             * Tiny low-priority details create visual noise at mission-control
             * scale. Preserve them when they are large enough to matter.
             */
            return
                bounds.Width < 11.0f &&
                bounds.Height < 11.0f;
        }

        private static bool IsMinorPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            return
                node.Category ==
                    VesselNodeCategory.Unknown ||
                node.Category ==
                    VesselNodeCategory.Structural ||
                node.Category ==
                    VesselNodeCategory.ReactionWheel ||
                node.Category ==
                    VesselNodeCategory.Battery ||
                node.Category ==
                    VesselNodeCategory.Generator ||
                node.Category ==
                    VesselNodeCategory.Antenna ||
                node.Category ==
                    VesselNodeCategory.RcsThruster;
        }
        private static bool IsPrimaryHullPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            /*
             * These categories should visually define the vehicle's main
             * silhouette before secondary equipment is noticed.
             */
            return
                node.Category ==
                    VesselNodeCategory.Command ||
                node.Category ==
                    VesselNodeCategory.FuelTank ||
                node.Category ==
                    VesselNodeCategory.Fairing ||
                node.Category ==
                    VesselNodeCategory.Payload ||
                node.Category ==
                    VesselNodeCategory.SolidBooster;
        }
        private static bool IsMajorPart(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                return false;
            }

            return
                node.Category ==
                    VesselNodeCategory.Command ||
                node.Category ==
                    VesselNodeCategory.Engine ||
                node.Category ==
                    VesselNodeCategory.SolidBooster ||
                node.Category ==
                    VesselNodeCategory.FuelTank ||
                node.Category ==
                    VesselNodeCategory.SolarPanel ||
                node.Category ==
                    VesselNodeCategory.DockingPort ||
                node.IsSeparationBoundary;
        }

        private static void DrawFallbackGlyph(
            MissionRenderContext context,
            PointF point,
            VesselTopologyNode node,
            bool isRoot)
        {
            float size =
                isRoot
                    ? 7.0f
                    : 4.0f;

            Color color =
                isRoot
                    ? Color.White
                    : context.DimPhosphorColor;

            using (Pen pen =
                new Pen(
                    color,
                    isRoot
                        ? 1.8f
                        : 1.0f))
            {
                context.Graphics.DrawEllipse(
                    pen,
                    point.X - size,
                    point.Y - size,
                    size * 2.0f,
                    size * 2.0f);
            }
        }

        private static PointF ToScreen(
            double horizontal,
            double vertical,
            double centerH,
            double centerV,
            double scale,
            float screenCenterX,
            float screenCenterY)
        {
            return
                new PointF(
                    screenCenterX +
                        (float)(
                            (horizontal - centerH) *
                            scale),

                    screenCenterY -
                        (float)(
                            (vertical - centerV) *
                            scale));
        }

        private static void Expand(
            double horizontal,
            double vertical,
            ref double minH,
            ref double maxH,
            ref double minV,
            ref double maxV)
        {
            if (!IsFinite(horizontal) ||
                !IsFinite(vertical))
            {
                return;
            }

            minH =
                Math.Min(
                    minH,
                    horizontal);

            maxH =
                Math.Max(
                    maxH,
                    horizontal);

            minV =
                Math.Min(
                    minV,
                    vertical);

            maxV =
                Math.Max(
                    maxV,
                    vertical);
        }

        private static void DrawWaiting(
            MissionRenderContext context,
            Rectangle bounds)
        {
            Rectangle message =
                Rectangle.FromLTRB(
                    bounds.Left + 24,
                    bounds.Top +
                        PanelHeaderHeight +
                        40,
                    bounds.Right - 24,
                    bounds.Bottom - 24);

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.DrawString(
                    "WAITING FOR ACTIVE VESSEL SCHEMATIC",
                    context.LargeFont,
                    brush,
                    message,
                    format);
            }
        }

        private static void DrawStatus(
            MissionRenderContext context,
            Rectangle bounds,
            VesselTopology topology,
            DateTime receivedUtc,
            int outlineParts,
            int fallbackParts)
        {
            int x =
                bounds.Left + 14;

            int valueX =
                bounds.Left +
                Math.Max(
                    100,
                    (int)(bounds.Width * 0.43));

            int y =
                bounds.Top +
                PanelHeaderHeight +
                16;

            DrawField(
                context,
                "LINK",
                topology != null
                    ? "SCHEM ONLINE"
                    : "WAITING",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "VESSEL",
                topology != null
                    ? Safe(
                        topology.VesselName)
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "PARTS",
                topology != null
                    ? topology.PartCount.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "OUTLINES",
                topology != null
                    ? outlineParts.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "FALLBACK",
                topology != null
                    ? fallbackParts.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "STAGE",
                topology != null
                    ? topology.CurrentStage.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "REV",
                topology != null
                    ? topology.Revision.ToString()
                    : "---",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "VIEW",
                "SIDE",
                x,
                valueX,
                y);

            y += 28;

            DrawField(
                context,
                "AGE",
                receivedUtc != DateTime.MinValue
                    ? Math.Max(
                        0.0,
                        (DateTime.UtcNow -
                         receivedUtc).TotalSeconds)
                        .ToString("0.0") +
                      " S"
                    : "---",
                x,
                valueX,
                y);

            y += 48;

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    "MESH HULL SCHEMATIC",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 24;

                context.Graphics.DrawString(
                    "NO STRUCTURAL LINKS",
                    context.SmallFont,
                    brush,
                    x,
                    y);

                y += 22;

                context.Graphics.DrawString(
                    "SINGLE SIDE VIEW",
                    context.SmallFont,
                    brush,
                    x,
                    y);
            }
        }

        private static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            using (SolidBrush dim =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush bright =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    dim,
                    labelX,
                    y);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    bright,
                    valueX,
                    y);
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (Pen pen =
                new Pen(
                    context.DimPhosphorColor,
                    1.0f))
            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width - 1),
                    Math.Max(
                        0,
                        bounds.Height - 1));

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    brush,
                    bounds.Left + 10,
                    bounds.Top + 8);

                context.Graphics.DrawLine(
                    pen,
                    bounds.Left,
                    bounds.Top +
                        PanelHeaderHeight,
                    bounds.Right,
                    bounds.Top +
                        PanelHeaderHeight);
            }
        }

        private static string Safe(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
                    ? "---"
                    : value.Trim();
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }
    }
}