using System;
using System.Drawing;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class PropulsionFooterCard :
        MissionDisplayCard<PropulsionPageRenderModel>
    {
        public PropulsionFooterCard()
            : base(
                "prop.footer",
                "PROPULSION FOOTER")
        {
        }

        protected override bool DrawStandardFrame
        {
            get { return false; }
        }

        protected override Rectangle CalculateContentBounds()
        {
            return Bounds;
        }

        protected override void DrawContent(
            MissionRenderContext context,
            Rectangle contentBounds,
            PropulsionPageRenderModel model)
        {
            if (model == null ||
                model.Telemetry == null)
            {
                return;
            }

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
                    contentBounds);

                context.Graphics.DrawRectangle(
                    border,
                    contentBounds);
            }

            string[] labels =
            {
                "STAGE",
                "THROTTLE",
                "THRUST",
                "TWR",
                "ISP",
                "ENGINES",
                "ACTIVE LF",
                "ACTIVE OX",
                "GRAPH REV"
            };

            string[] values =
            {
                model.Telemetry.CurrentStage
                    .ToString("00"),

                Percent(
                    model.Telemetry.Throttle),

                Number(
                    model.Telemetry.CurrentThrust,
                    "0.0",
                    " kN"),

                Number(
                    model.Telemetry.ThrustToWeightRatio,
                    "0.00",
                    ""),

                Number(
                    model.Telemetry.AverageSpecificImpulse,
                    "0",
                    " s"),

                model.Telemetry.EngineCount
                    .ToString("00"),

                Percent(
                    Fraction(
                        model.Telemetry.StageLiquidFuelAmount,
                        model.Telemetry.StageLiquidFuelCapacity)),

                Percent(
                    Fraction(
                        model.Telemetry.StageOxidizerAmount,
                        model.Telemetry.StageOxidizerCapacity)),

                model.Graph != null
                    ? model.Graph.TopologyRevision
                        .ToString()
                    : "--"
            };

            int cellWidth =
                Math.Max(
                    1,
                    contentBounds.Width /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        contentBounds.Left +
                        index * cellWidth,
                        contentBounds.Top,
                        index ==
                        labels.Length - 1
                            ? contentBounds.Right -
                              (contentBounds.Left +
                               index * cellWidth)
                            : cellWidth,
                        contentBounds.Height);

                DrawCell(
                    context,
                    cell,
                    labels[index],
                    values[index],
                    index > 0);
            }
        }

        private static void DrawCell(
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
