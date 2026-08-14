using System;
using System.Drawing;
using KMC.Engine.Propulsion;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Cards.Propulsion
{
    public sealed class PropulsionPerformanceCard :
        MissionDisplayCard<PropulsionPageRenderModel>
    {
        public PropulsionPerformanceCard()
            : base(
                "prop.performance",
                "PROPULSION STATUS / THRUST")
        {
        }

        protected override void DrawContent(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionPageRenderModel model)
        {
            if (model == null ||
                model.Engineering == null ||
                model.Engineering.Status == null)
            {
                DrawCentered(
                    context,
                    bounds,
                    "AWAITING ENGINEERING MODEL");
                return;
            }

            PropulsionStatusModel status =
                model.Engineering.Status;

            PropulsionLiveStateModel live =
                model.Engineering.Live;

            int gap = 12;

            int topHeight =
                Math.Max(
                    118,
                    bounds.Height * 47 / 100);

            int channelHeight =
                Math.Max(
                    86,
                    Math.Min(
                        104,
                        bounds.Height * 25 / 100));

            int columnWidth =
                Math.Max(
                    1,
                    (bounds.Width -
                     gap * 2) /
                    3);

            Rectangle engines =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    columnWidth,
                    topHeight);

            Rectangle thrust =
                new Rectangle(
                    engines.Right + gap,
                    bounds.Top,
                    columnWidth,
                    topHeight);

            Rectangle system =
                new Rectangle(
                    thrust.Right + gap,
                    bounds.Top,
                    Math.Max(
                        1,
                        bounds.Right -
                        thrust.Right -
                        gap),
                    topHeight);

            Rectangle channels =
                new Rectangle(
                    bounds.Left,
                    engines.Bottom + 10,
                    bounds.Width,
                    channelHeight);

            Rectangle summary =
                new Rectangle(
                    bounds.Left,
                    channels.Bottom + 10,
                    bounds.Width,
                    Math.Max(
                        1,
                        bounds.Bottom -
                        channels.Bottom -
                        10));

            DrawMiniPanel(
                context,
                engines,
                "ENGINE STATUS",
                new string[]
                {
                    "INSTALLED",
                    "READY",
                    "PRODUCING",
                    "FLAMEOUT"
                },
                new string[]
                {
                    status.InstalledEngineCount.ToString("00"),
                    status.ReadyEngineCount.ToString("00"),
                    status.ProducingEngineCount.ToString("00"),
                    status.FlameoutEngineCount.ToString("00")
                },
                status.FlameoutEngineCount > 0
                    ? SeverityColor(
                        context,
                        status.Severity)
                    : context.PhosphorColor);

            DrawMiniPanel(
                context,
                thrust,
                "THRUST STATUS",
                new string[]
                {
                    "CURRENT",
                    "AVAILABLE",
                    "POTENTIAL",
                    "THROTTLE"
                },
                new string[]
                {
                    status.CurrentThrustKnown
                        ? status.CurrentThrust.ToString("0.0") + " kN"
                        : "---",

                    status.AvailableThrustKnown
                        ? status.AvailableThrust.ToString("0.0") + " kN"
                        : "---",

                    live != null &&
                    live.PotentialMaximumThrustKnown
                        ? live.PotentialMaximumThrust.ToString("0.0") + " kN"
                        : "---",

                    model.Telemetry != null
                        ? Percent(
                            model.Telemetry.Throttle)
                        : "---"
                },
                context.PhosphorColor);

            DrawSystemPanel(
                context,
                system,
                status);

            DrawChannelHealthStrip(
                context,
                channels,
                status);

            DrawSummaryPanel(
                context,
                summary,
                status);
        }

        private static void DrawChannelHealthStrip(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionStatusModel status)
        {
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        95,
                        context.DimPhosphorColor)))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);

                context.Graphics.DrawString(
                    "ENGINE CHANNEL HEALTH  /  DETAIL ON PROP 2/3",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 10,
                    bounds.Top + 5);
            }

            string[] labels =
            {
                "TOTAL",
                "NORMAL",
                "ADVISORY",
                "FAULT",
                "UNKNOWN"
            };

            string[] values =
            {
                status.EngineChannels.Count.ToString("00"),
                status.ChannelNormalCount.ToString("00"),
                status.ChannelAdvisoryCount.ToString("00"),
                status.ChannelFaultCount.ToString("00"),
                status.ChannelUnknownCount.ToString("00")
            };

            int top =
                bounds.Top + 30;

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
                        top,
                        index ==
                            labels.Length - 1
                                ? bounds.Right -
                                  (bounds.Left +
                                   index * cellWidth)
                                : cellWidth,
                        Math.Max(
                            1,
                            bounds.Bottom -
                            top - 4));

                Color valueColor =
                    index == 2 &&
                    status.ChannelAdvisoryCount > 0
                        ? Color.FromArgb(
                            255,
                            220,
                            185,
                            92)
                    : index == 3 &&
                      status.ChannelFaultCount > 0
                        ? Color.FromArgb(
                            255,
                            255,
                            82,
                            72)
                    : index == 4 &&
                      status.ChannelUnknownCount > 0
                        ? context.DimPhosphorColor
                    : context.PhosphorColor;

                DrawHealthCell(
                    context,
                    cell,
                    labels[index],
                    values[index],
                    index > 0,
                    valueColor);
            }
        }

        private static void DrawHealthCell(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            bool divider,
            Color valueColor)
        {
            using (Pen dividerPen =
                new Pen(
                    Color.FromArgb(
                        70,
                        context.DimPhosphorColor)))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    valueColor))
            using (StringFormat centered =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                if (divider)
                {
                    context.Graphics.DrawLine(
                        dividerPen,
                        bounds.Left,
                        bounds.Top + 3,
                        bounds.Left,
                        bounds.Bottom - 3);
                }

                int labelHeight =
                    Math.Max(
                        30,
                        bounds.Height / 2);

                int valueHeight =
                    Math.Max(
                        30,
                        bounds.Height -
                        labelHeight);

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    new Rectangle(
                        bounds.Left + 3,
                        bounds.Top,
                        bounds.Width - 6,
                        labelHeight),
                    centered);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    new Rectangle(
                        bounds.Left + 3,
                        bounds.Top + labelHeight,
                        bounds.Width - 6,
                        valueHeight),
                    centered);
            }
        }

        private static void DrawMiniPanel(
            MissionRenderContext context,
            Rectangle bounds,
            string title,
            string[] labels,
            string[] values,
            Color valueColor)
        {
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        100,
                        context.DimPhosphorColor)))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    valueColor))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 10,
                    bounds.Top + 8);

                int rowTop =
                    bounds.Top + 36;

                int rowHeight =
                    Math.Max(
                        20,
                        (bounds.Height - 42) /
                        labels.Length);

                for (int index = 0;
                     index < labels.Length;
                     index++)
                {
                    int y =
                        rowTop +
                        rowHeight * index;

                    context.Graphics.DrawString(
                        labels[index],
                        context.SmallFont,
                        labelBrush,
                        bounds.Left + 10,
                        y);

                    SizeF valueSize =
                        context.Graphics.MeasureString(
                            values[index],
                            context.SmallFont);

                    context.Graphics.DrawString(
                        values[index],
                        context.SmallFont,
                        valueBrush,
                        bounds.Right -
                        valueSize.Width -
                        10,
                        y);
                }
            }
        }

        private static void DrawSystemPanel(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionStatusModel status)
        {
            Color statusColor =
                SeverityColor(
                    context,
                    status.Severity);

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        120,
                        statusColor),
                    1.4f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush statusBrush =
                new SolidBrush(
                    statusColor))
            using (SolidBrush detailBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);

                context.Graphics.DrawString(
                    "SYSTEM STATUS",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 10,
                    bounds.Top + 8);

                context.Graphics.DrawString(
                    status.Severity.ToString().ToUpperInvariant(),
                    context.SmallFont,
                    statusBrush,
                    bounds.Left + 10,
                    bounds.Top + 40);

                Rectangle condition =
                    new Rectangle(
                        bounds.Left + 10,
                        bounds.Top + 72,
                        bounds.Width - 20,
                        bounds.Height - 80);

                context.Graphics.DrawString(
                    BreakCondition(
                        status.Condition.ToString()),
                    context.SmallFont,
                    detailBrush,
                    condition);
            }
        }

        private static void DrawSummaryPanel(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionStatusModel status)
        {
            if (bounds.Height <= 0)
            {
                return;
            }

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor)))
            using (SolidBrush summaryBrush =
                new SolidBrush(
                    SeverityColor(
                        context,
                        status.Severity)))
            using (SolidBrush detailBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);

                Rectangle summary =
                    new Rectangle(
                        bounds.Left + 12,
                        bounds.Top + 6,
                        bounds.Width - 24,
                        Math.Max(
                            20,
                            bounds.Height / 2 - 2));

                context.Graphics.DrawString(
                    status.Summary,
                    context.SmallFont,
                    summaryBrush,
                    summary);

                Rectangle stage =
                    new Rectangle(
                        bounds.Left + 12,
                        bounds.Top +
                        bounds.Height / 2,
                        bounds.Width - 24,
                        Math.Max(
                            18,
                            bounds.Height / 2 - 5));

                context.Graphics.DrawString(
                    status.StageSummary,
                    context.SmallFont,
                    detailBrush,
                    stage);
            }
        }

        private static Color SeverityColor(
            MissionRenderContext context,
            PropulsionSeverity severity)
        {
            switch (severity)
            {
                case PropulsionSeverity.Critical:
                    return
                        Color.FromArgb(
                            255,
                            255,
                            82,
                            72);

                case PropulsionSeverity.Warning:
                    return
                        Color.FromArgb(
                            255,
                            255,
                            196,
                            72);

                case PropulsionSeverity.Advisory:
                    return
                        Color.FromArgb(
                            255,
                            220,
                            185,
                            92);

                case PropulsionSeverity.Normal:
                    return
                        context.PhosphorColor;

                default:
                    return
                        context.DimPhosphorColor;
            }
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle bounds,
            string text)
        {
            using (SolidBrush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,
                    LineAlignment =
                        StringAlignment.Center
                })
            {
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    brush,
                    bounds,
                    format);
            }
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

        private static string BreakCondition(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "---";
            }

            System.Text.StringBuilder result =
                new System.Text.StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char c =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(c) &&
                    char.IsLower(
                        value[index - 1]))
                {
                    result.Append(' ');
                }

                result.Append(c);
            }

            return
                result.ToString()
                    .ToUpperInvariant();
        }
    }
}
