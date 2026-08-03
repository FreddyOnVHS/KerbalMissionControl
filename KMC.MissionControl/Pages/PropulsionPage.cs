using System;
using System.Drawing;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Pages
{
    public sealed class PropulsionPage :
        IMissionPage
    {
        private const int Gap = 16;
        private const int Padding = 18;

        private readonly PropulsionSchematicRenderer
            _schematicRenderer =
                new PropulsionSchematicRenderer();

        public string Name
        {
            get { return "PROPULSION"; }
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

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout pageLayout =
                new MissionPageLayout(context);

            pageLayout.DrawHeader(
                Name,
                "CH 04");

            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 20,
                    context.ContentBounds.Top + 82,
                    context.ContentBounds.Width - 40,
                    context.ContentBounds.Height - 102);

            int leftWidth =
                Math.Max(
                    300,
                    Math.Min(
                        470,
                        working.Width / 3));

            Rectangle statusPanel =
                new Rectangle(
                    working.Left,
                    working.Top,
                    leftWidth,
                    working.Height);

            Rectangle schematicPanel =
                new Rectangle(
                    statusPanel.Right + Gap,
                    working.Top,
                    working.Right -
                    statusPanel.Right -
                    Gap,
                    working.Height);

            DrawPanel(
                context,
                statusPanel,
                "ENGINE / PROPELLANT STATUS");

            DrawPanel(
                context,
                schematicPanel,
                "LIVE PROPULSION SCHEMATIC");

            DrawStatus(
                context,
                Rectangle.Inflate(
                    statusPanel,
                    -Padding,
                    -58),
                telemetry);

            Rectangle schematicBounds =
                new Rectangle(
                    schematicPanel.Left + 12,
                    schematicPanel.Top + 58,
                    schematicPanel.Width - 24,
                    schematicPanel.Height - 70);

            PropulsionRenderGraph graph =
                PropulsionGraphStore.GetCurrent();

            _schematicRenderer.Draw(
                context.Graphics,
                schematicBounds,
                graph,
                context.SmallFont,
                context.SmallFont,
                context.PhosphorColor,
                context.DimPhosphorColor);

            DrawSchematicHeader(
                context,
                schematicPanel,
                graph);
        }

        private static void DrawStatus(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            int y = bounds.Top;
            int row = 31;

            DrawRow(context, bounds, y,
                "CURRENT STAGE",
                telemetry.CurrentStage.ToString("00"));
            y += row;

            DrawRow(context, bounds, y,
                "ENGINES",
                telemetry.EngineCount.ToString("00"));
            y += row;

            DrawRow(context, bounds, y,
                "IGNITED",
                telemetry.IgnitedEngineCount.ToString("00"));
            y += row;

            DrawRow(context, bounds, y,
                "PRODUCING",
                telemetry.ProducingThrustEngineCount.ToString("00"));
            y += row;

            DrawRow(context, bounds, y,
                "FLAMEOUT",
                telemetry.FlameoutEngineCount.ToString("00"));
            y += row + 12;

            DrawDivider(context, bounds, y);
            y += 18;

            DrawRow(context, bounds, y,
                "THROTTLE",
                FormatPercent(telemetry.Throttle));
            y += row;

            DrawRow(context, bounds, y,
                "THRUST",
                Format(telemetry.CurrentThrust, "0.0", " kN"));
            y += row;

            DrawRow(context, bounds, y,
                "MAX THRUST",
                Format(telemetry.MaximumThrust, "0.0", " kN"));
            y += row;

            DrawRow(context, bounds, y,
                "TWR",
                Format(telemetry.ThrustToWeightRatio, "0.00", ""));
            y += row;

            DrawRow(context, bounds, y,
                "AVG ISP",
                Format(telemetry.AverageSpecificImpulse, "0.0", " s"));
            y += row + 12;

            DrawDivider(context, bounds, y);
            y += 18;

            DrawResource(
                context,
                bounds,
                ref y,
                "LIQUID FUEL",
                telemetry.TotalLiquidFuelAmount,
                telemetry.TotalLiquidFuelCapacity);

            DrawResource(
                context,
                bounds,
                ref y,
                "OXIDIZER",
                telemetry.TotalOxidizerAmount,
                telemetry.TotalOxidizerCapacity);

            DrawResource(
                context,
                bounds,
                ref y,
                "MONOPROPELLANT",
                telemetry.TotalMonopropellantAmount,
                telemetry.TotalMonopropellantCapacity);
        }

        private static void DrawSchematicHeader(
            MissionRenderContext context,
            Rectangle panel,
            PropulsionRenderGraph graph)
        {
            string status =
                graph == null
                    ? "TOPOLOGY LINK: WAIT"
                    : "REV " +
                      graph.TopologyRevision +
                      "  NODES " +
                      graph.Nodes.Count +
                      "  EDGES " +
                      graph.Edges.Count;

            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawString(
                    status,
                    context.SmallFont,
                    brush,
                    panel.Right - 330,
                    panel.Top + 16);
            }
        }

        private static void DrawResource(
            MissionRenderContext context,
            Rectangle bounds,
            ref int y,
            string label,
            double amount,
            double capacity)
        {
            double fraction =
                capacity > 0.0
                    ? Math.Max(
                        0.0,
                        Math.Min(
                            1.0,
                            amount / capacity))
                    : 0.0;

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush fillBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Pen framePen =
                new Pen(
                    Color.FromArgb(
                        130,
                        context.DimPhosphorColor)))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    bounds.Left,
                    y);

                Rectangle bar =
                    new Rectangle(
                        bounds.Left,
                        y + 22,
                        bounds.Width - 60,
                        12);

                context.Graphics.DrawRectangle(
                    framePen,
                    bar);

                Rectangle fill =
                    new Rectangle(
                        bar.Left + 1,
                        bar.Top + 1,
                        Math.Max(
                            0,
                            (int)
                            ((bar.Width - 1) *
                             fraction)),
                        Math.Max(
                            0,
                            bar.Height - 1));

                context.Graphics.FillRectangle(
                    fillBrush,
                    fill);

                context.Graphics.DrawString(
                    (fraction * 100.0)
                        .ToString("0") +
                    "%",
                    context.SmallFont,
                    fillBrush,
                    bar.Right + 8,
                    y + 15);
            }

            y += 54;
        }

        private static void DrawRow(
            MissionRenderContext context,
            Rectangle bounds,
            int y,
            string label,
            string value)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    bounds.Left,
                    y);

                SizeF size =
                    context.Graphics.MeasureString(
                        value,
                        context.SmallFont);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    bounds.Right - size.Width,
                    y);
            }
        }

        private static void DrawDivider(
            MissionRenderContext context,
            Rectangle bounds,
            int y)
        {
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        100,
                        context.DimPhosphorColor)))
            {
                context.Graphics.DrawLine(
                    pen,
                    bounds.Left,
                    y,
                    bounds.Right,
                    y);
            }
        }

        private static void DrawPanel(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        70,
                        2,
                        14,
                        20)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        130,
                        context.DimPhosphorColor),
                    1.5f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.FillRectangle(
                    background,
                    bounds);

                context.Graphics.DrawRectangle(
                    border,
                    bounds);

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + Padding,
                    bounds.Top + 15);

                context.Graphics.DrawLine(
                    border,
                    bounds.Left + Padding,
                    bounds.Top + 47,
                    bounds.Right - Padding,
                    bounds.Top + 47);
            }
        }

        private static string FormatPercent(
            double fraction)
        {
            return
                (Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        fraction)) *
                 100.0)
                .ToString("0") +
                "%";
        }

        private static string Format(
            double value,
            string format,
            string suffix)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "---";
            }

            return Math.Max(0.0, value)
                .ToString(format) +
                suffix;
        }
    }
}
