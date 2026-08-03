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
        private readonly PropulsionDisplayRenderer
            _displayRenderer =
                new PropulsionDisplayRenderer();

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
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 78,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 98);

            int footerHeight =
                Math.Max(
                    58,
                    working.Height / 11);

            Rectangle display =
                new Rectangle(
                    working.Left,
                    working.Top,
                    working.Width,
                    working.Height -
                    footerHeight -
                    12);

            Rectangle footer =
                new Rectangle(
                    working.Left,
                    display.Bottom + 12,
                    working.Width,
                    footerHeight);

            PropulsionRenderGraph graph =
                PropulsionGraphStore.GetCurrent();

            _displayRenderer.Draw(
                context.Graphics,
                display,
                graph,
                telemetry,
                context.SmallFont,
                context.SmallFont,
                context.SmallFont,
                context.PhosphorColor,
                context.DimPhosphorColor);

            DrawFooter(
                context,
                footer,
                telemetry,
                graph);
        }

        private static void DrawFooter(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry,
            PropulsionRenderGraph graph)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        65,
                        2,
                        14,
                        20)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        125,
                        context.DimPhosphorColor),
                    1.3f))
            {
                context.Graphics.FillRectangle(
                    fill,
                    bounds);

                context.Graphics.DrawRectangle(
                    border,
                    bounds);
            }

            string[] labels =
            {
                "STAGE",
                "THROTTLE",
                "THRUST",
                "TWR",
                "ISP",
                "ENGINES",
                "FUEL",
                "OX",
                "GRAPH REV"
            };

            string[] values =
            {
                telemetry.CurrentStage
                    .ToString("00"),
                Percent(
                    telemetry.Throttle),
                Number(
                    telemetry.CurrentThrust,
                    "0.0",
                    " kN"),
                Number(
                    telemetry
                        .ThrustToWeightRatio,
                    "0.00",
                    ""),
                Number(
                    telemetry
                        .AverageSpecificImpulse,
                    "0",
                    " s"),
                telemetry.EngineCount
                    .ToString("00"),
                Percent(
                    Fraction(
                        telemetry
                            .TotalLiquidFuelAmount,
                        telemetry
                            .TotalLiquidFuelCapacity)),
                Percent(
                    Fraction(
                        telemetry
                            .TotalOxidizerAmount,
                        telemetry
                            .TotalOxidizerCapacity)),
                graph != null
                    ? graph.TopologyRevision
                        .ToString()
                    : "--"
            };

            int cellWidth =
                Math.Max(
                    1,
                    bounds.Width /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        bounds.Left +
                        index * cellWidth,
                        bounds.Top,
                        index ==
                        labels.Length - 1
                            ? bounds.Right -
                              (bounds.Left +
                               index * cellWidth)
                            : cellWidth,
                        bounds.Height);

                DrawFooterCell(
                    context,
                    cell,
                    labels[index],
                    values[index],
                    index > 0);
            }
        }

        private static void DrawFooterCell(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            bool drawDivider)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        80,
                        context.DimPhosphorColor)))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                if (drawDivider)
                {
                    context.Graphics.DrawLine(
                        divider,
                        bounds.Left,
                        bounds.Top + 8,
                        bounds.Left,
                        bounds.Bottom - 8);
                }

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top + 5,
                        bounds.Width,
                        bounds.Height / 2 - 2),
                    centered);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    new Rectangle(
                        bounds.Left,
                        bounds.Top +
                        bounds.Height / 2,
                        bounds.Width,
                        bounds.Height / 2 - 3),
                    centered);
            }
        }

        private static double Fraction(
            double amount,
            double capacity)
        {
            if (capacity <= 0.0)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    amount / capacity));
        }

        private static string Percent(
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

        private static string Number(
            double value,
            string format,
            string suffix)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return "---";
            }

            return Math.Max(
                    0.0,
                    value)
                .ToString(format) +
                suffix;
        }
    }
}
