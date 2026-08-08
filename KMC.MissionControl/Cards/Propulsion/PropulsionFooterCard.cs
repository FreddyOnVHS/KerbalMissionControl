using System;
using System.Drawing;
using KMC.Engine.Propulsion;
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

        protected override Rectangle CalculateContentBounds(
            Rectangle localBounds)
        {
            return localBounds;
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

            string[] labels;
            string[] values;

            if (model.Engineering != null &&
                model.Engineering.Status != null)
            {
                PropulsionStatusModel status =
                    model.Engineering.Status;

                labels =
                    new string[]
                    {
                        "STAGE",
                        "THROTTLE",
                        "THRUST",
                        "AVAILABLE",
                        "READY",
                        "PRODUCING",
                        "NEXT STAGE",
                        "NEXT LOSS",
                        "STATUS"
                    };

                values =
                    new string[]
                    {
                        status.LiveCurrentStage
                            .ToString("00"),

                        Percent(
                            model.Telemetry.Throttle),

                        status.CurrentThrustKnown
                            ? status.CurrentThrust
                                .ToString("0.0") +
                              " kN"
                            : "---",

                        status.AvailableThrustKnown
                            ? status.AvailableThrust
                                .ToString("0.0") +
                              " kN"
                            : "---",

                        status.ReadyEngineCount
                            .ToString("00"),

                        status.ProducingEngineCount
                            .ToString("00"),

                        status.NextStage
                            .ToString("00"),

                        status.NextStageEngineLossCount
                            .ToString("00"),

                        ShortCondition(
                            status)
                    };
            }
            else
            {
                labels =
                    new string[]
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

                values =
                    new string[]
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
            }

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
                    index > 0,
                    index == labels.Length - 1 &&
                    model.Engineering != null &&
                    model.Engineering.Status != null
                        ? model.Engineering.Status.Severity
                        : PropulsionSeverity.Normal);
            }
        }

        private static string ShortCondition(
            PropulsionStatusModel status)
        {
            if (status == null)
            {
                return "---";
            }

            switch (status.Condition)
            {
                case PropulsionCondition.EngineFlameout:
                    return "FLAMEOUT";

                case PropulsionCondition.PropulsionLost:
                    return "PROP LOST";

                case PropulsionCondition.NextStageFeedRisk:
                    return "STAGE RISK";

                case PropulsionCondition.NextStagePropulsionTerminated:
                    return "PROP END";

                case PropulsionCondition.NextStageEngineSeparation:
                    return "STAGE SEP";

                case PropulsionCondition.Standby:
                    return "STANDBY";

                case PropulsionCondition.Nominal:
                    return "NOMINAL";

                case PropulsionCondition.DataIncomplete:
                    return "DATA";

                default:
                    return
                        status.Severity
                            .ToString()
                            .ToUpperInvariant();
            }
        }

        private static void DrawCell(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            bool drawDivider,
            PropulsionSeverity severity)
        {
            Color valueColor = FooterValueColor(context, severity);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    valueColor))
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

        private static Color FooterValueColor(
            MissionRenderContext context,
            PropulsionSeverity severity)
        {
            switch (severity)
            {
                case PropulsionSeverity.Critical:
                    return Color.FromArgb(255, 255, 82, 72);
                case PropulsionSeverity.Warning:
                    return Color.FromArgb(255, 255, 196, 72);
                case PropulsionSeverity.Advisory:
                    return Color.FromArgb(255, 220, 185, 92);
                default:
                    return context.PhosphorColor;
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
