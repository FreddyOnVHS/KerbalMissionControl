using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.11.1 POWER integration for the Engine-owned synthetic
    /// spacecraft electrical distribution.
    ///
    /// This intentionally does not replace or reinterpret stock KSP
    /// ElectricCharge telemetry. It surfaces the KMC spacecraft-design
    /// Main A / Main B / Essential distribution in the established POWER
    /// upper summary region while the existing real-EC engineering panels
    /// remain below.
    /// </summary>
    public static class SyntheticDistributionOverlayRenderer
    {
        private static readonly Color Healthy =
            Color.FromArgb(
                112,
                202,
                154);

        private static readonly Color Advisory =
            Color.FromArgb(
                232,
                188,
                84);

        private static readonly Color Warning =
            Color.FromArgb(
                236,
                142,
                66);

        private static readonly Color Critical =
            Color.FromArgb(
                236,
                92,
                76);

        private static readonly Color Dead =
            Color.FromArgb(
                196,
                72,
                72);

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.SpacecraftSystems == null ||
                engineering.Snapshot.SpacecraftSystems.ElectricalDistribution == null)
            {
                return;
            }

            SyntheticElectricalDistributionModel distribution =
                engineering.Snapshot
                    .SpacecraftSystems
                    .ElectricalDistribution;

            Graphics graphics =
                context.Graphics;

            Rectangle pageArea =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 80);

            int height =
                Math.Max(
                    170,
                    pageArea.Height * 31 / 100);

            Rectangle bounds =
                new Rectangle(
                    pageArea.Left,
                    pageArea.Top,
                    pageArea.Width,
                    Math.Min(
                        height,
                        pageArea.Height));

            using (SolidBrush background =
                new SolidBrush(
                    Color.FromArgb(
                        3,
                        8,
                        7)))
            {
                graphics.FillRectangle(
                    background,
                    bounds);
            }

            using (Pen outer =
                new Pen(
                    Color.FromArgb(
                        190,
                        context.DimPhosphorColor),
                    1.6f))
            {
                graphics.DrawRectangle(
                    outer,
                    bounds);
            }

            int titleHeight =
                Math.Max(
                    28,
                    context.SmallFont.Height + 14);

            Rectangle title =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Top + 4,
                    bounds.Width - 24,
                    titleHeight - 6);

            DrawText(
                graphics,
                title,
                "SPACECRAFT DISTRIBUTION  /  SYNTHETIC 28V DC",
                context.SmallFont,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        125,
                        context.DimPhosphorColor),
                    1.0f))
            {
                graphics.DrawLine(
                    divider,
                    bounds.Left + 10,
                    bounds.Top + titleHeight,
                    bounds.Right - 10,
                    bounds.Top + titleHeight);
            }

            Rectangle body =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Top + titleHeight + 8,
                    bounds.Width - 24,
                    Math.Max(
                        0,
                        bounds.Height -
                        titleHeight -
                        30));

            int gap =
                Math.Max(
                    10,
                    body.Width / 150);

            int cardWidth =
                Math.Max(
                    1,
                    (body.Width - gap * 2) / 3);

            Rectangle mainA =
                new Rectangle(
                    body.Left,
                    body.Top,
                    cardWidth,
                    body.Height);

            Rectangle mainB =
                new Rectangle(
                    mainA.Right + gap,
                    body.Top,
                    cardWidth,
                    body.Height);

            Rectangle essential =
                new Rectangle(
                    mainB.Right + gap,
                    body.Top,
                    Math.Max(
                        1,
                        body.Right -
                        mainB.Right -
                        gap),
                    body.Height);

            DrawBusCard(
                graphics,
                mainA,
                distribution.FindBus(
                    "BUS_MAIN_A"),
                distribution,
                new string[]
                {
                    "SRC_GEN_A",
                    "SRC_BAT_A"
                },
                context);

            DrawBusCard(
                graphics,
                mainB,
                distribution.FindBus(
                    "BUS_MAIN_B"),
                distribution,
                new string[]
                {
                    "SRC_GEN_B",
                    "SRC_BAT_B"
                },
                context);

            DrawBusCard(
                graphics,
                essential,
                distribution.FindBus(
                    "BUS_ESS"),
                distribution,
                new string[]
                {
                    "FEED_ESS_A",
                    "FEED_ESS_B"
                },
                context);

            Rectangle note =
                new Rectangle(
                    bounds.Left + 12,
                    bounds.Bottom -
                    Math.Max(
                        18,
                        context.SmallFont.Height + 4),
                    bounds.Width - 24,
                    Math.Max(
                        16,
                        context.SmallFont.Height + 2));

            DrawText(
                graphics,
                note,
                "KMC SYNTHETIC DISTRIBUTION  |  REAL KSP EC STORAGE / FLOW / ENDURANCE REMAINS BELOW",
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawBusCard(
            Graphics graphics,
            Rectangle bounds,
            SyntheticElectricalBus bus,
            SyntheticElectricalDistributionModel distribution,
            string[] sourceIds,
            MissionRenderContext context)
        {
            Color stateColor =
                BusStateColor(
                    bus,
                    context);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        12,
                        stateColor)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        150,
                        stateColor),
                    1.3f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    border,
                    bounds);
            }

            int pad = 10;
            int y = bounds.Top + 6;
            int rowHeight =
                Math.Max(
                    20,
                    context.SmallFont.Height + 7);

            string title =
                bus != null
                    ? bus.DisplayName
                    : "BUS UNAVAILABLE";

            DrawText(
                graphics,
                new Rectangle(
                    bounds.Left + pad,
                    y,
                    bounds.Width - pad * 2,
                    rowHeight + 2),
                title,
                context.LargeFont,
                stateColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            y += rowHeight + 6;

            string state =
                bus != null
                    ? SplitWords(
                        bus.State.ToString())
                    : "UNAVAILABLE";

            DrawPair(
                graphics,
                bounds,
                ref y,
                "STATE",
                state,
                "VOLTAGE",
                bus != null
                    ? bus.Voltage.ToString("0.0") + " V"
                    : "--",
                stateColor,
                context,
                rowHeight);

            DrawPair(
                graphics,
                bounds,
                ref y,
                "DEMAND",
                bus != null
                    ? bus.DemandAmps.ToString("0.0") + " A"
                    : "--",
                "AVAILABLE",
                bus != null
                    ? bus.AvailableCurrentAmps.ToString("0.0") + " A"
                    : "--",
                stateColor,
                context,
                rowHeight);

            DrawPair(
                graphics,
                bounds,
                ref y,
                "LOAD",
                bus != null
                    ? FormatLoad(
                        bus.LoadPercent)
                    : "--",
                "ACTIVE SRC",
                bus != null
                    ? bus.ActiveSourceCount.ToString()
                    : "--",
                stateColor,
                context,
                rowHeight);

            if (sourceIds == null)
            {
                return;
            }

            for (int index = 0;
                 index < sourceIds.Length;
                 index++)
            {
                SyntheticElectricalSource source =
                    distribution != null
                        ? distribution.FindSource(
                            sourceIds[index])
                        : null;

                if (source == null)
                {
                    continue;
                }

                Color sourceColor =
                    SourceStateColor(
                        source,
                        context);

                DrawSingle(
                    graphics,
                    bounds,
                    ref y,
                    source.DisplayName,
                    SourceStateText(
                        source),
                    sourceColor,
                    context,
                    rowHeight);
            }
        }

        private static void DrawPair(
            Graphics graphics,
            Rectangle card,
            ref int y,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            Color valueColor,
            MissionRenderContext context,
            int rowHeight)
        {
            if (y + rowHeight >
                card.Bottom - 4)
            {
                return;
            }

            int pad = 10;
            int gap = 12;
            int usable =
                card.Width -
                pad * 2 -
                gap;

            int half =
                Math.Max(
                    1,
                    usable / 2);

            Rectangle left =
                new Rectangle(
                    card.Left + pad,
                    y,
                    half,
                    rowHeight);

            Rectangle right =
                new Rectangle(
                    left.Right + gap,
                    y,
                    Math.Max(
                        1,
                        card.Right -
                        pad -
                        left.Right -
                        gap),
                    rowHeight);

            DrawInline(
                graphics,
                left,
                leftLabel,
                leftValue,
                valueColor,
                context);

            DrawInline(
                graphics,
                right,
                rightLabel,
                rightValue,
                valueColor,
                context);

            y += rowHeight;
        }

        private static void DrawSingle(
            Graphics graphics,
            Rectangle card,
            ref int y,
            string label,
            string value,
            Color valueColor,
            MissionRenderContext context,
            int rowHeight)
        {
            if (y + rowHeight >
                card.Bottom - 4)
            {
                return;
            }

            DrawInline(
                graphics,
                new Rectangle(
                    card.Left + 10,
                    y,
                    card.Width - 20,
                    rowHeight),
                label,
                value,
                valueColor,
                context);

            y += rowHeight;
        }

        private static void DrawInline(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            Color valueColor,
            MissionRenderContext context)
        {
            int labelWidth =
                Math.Max(
                    70,
                    bounds.Width * 50 / 100);

            DrawText(
                graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        1,
                        labelWidth - 6),
                    bounds.Height),
                label,
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                graphics,
                new Rectangle(
                    bounds.Left + labelWidth,
                    bounds.Top,
                    Math.Max(
                        1,
                        bounds.Width -
                        labelWidth),
                    bounds.Height),
                value,
                context.SmallFont,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static string FormatLoad(
            double percent)
        {
            if (double.IsNaN(percent))
            {
                return "--";
            }

            if (double.IsInfinity(percent) ||
                percent >= 999.0)
            {
                return ">999%";
            }

            return
                percent.ToString("0") +
                "%";
        }

        private static string SourceStateText(
            SyntheticElectricalSource source)
        {
            if (source == null)
            {
                return "UNAVAILABLE";
            }

            if (!source.CommandedAvailable)
            {
                return "CMD OFF";
            }

            return
                SplitWords(
                    source.State.ToString());
        }

        private static Color SourceStateColor(
            SyntheticElectricalSource source,
            MissionRenderContext context)
        {
            if (source == null)
            {
                return
                    context.DimPhosphorColor;
            }

            if (!source.CommandedAvailable)
            {
                return Advisory;
            }

            switch (source.State)
            {
                case SyntheticElectricalSourceState.Online:
                    return Healthy;

                case SyntheticElectricalSourceState.Degraded:
                    return Warning;

                case SyntheticElectricalSourceState.Offline:
                    return Dead;

                default:
                    return
                        context.DimPhosphorColor;
            }
        }

        private static Color BusStateColor(
            SyntheticElectricalBus bus,
            MissionRenderContext context)
        {
            if (bus == null)
            {
                return
                    context.DimPhosphorColor;
            }

            switch (bus.State)
            {
                case SyntheticElectricalBusState.Nominal:
                    return Healthy;

                case SyntheticElectricalBusState.HighLoad:
                    return Advisory;

                case SyntheticElectricalBusState.Overloaded:
                    return Critical;

                case SyntheticElectricalBusState.Undervoltage:
                    return Warning;

                case SyntheticElectricalBusState.Unpowered:
                    return Dead;

                default:
                    return
                        context.DimPhosphorColor;
            }
        }

        private static string SplitWords(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "---";
            }

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char current =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(current) &&
                    !char.IsUpper(
                        value[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(
                    current);
            }

            return
                builder.ToString()
                    .ToUpperInvariant();
        }

        private static void DrawText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color,
            TextFormatFlags flags)
        {
            if (graphics == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            TextRenderer.DrawText(
                graphics,
                string.IsNullOrWhiteSpace(text)
                    ? "---"
                    : text.Trim().ToUpperInvariant(),
                font,
                bounds,
                color,
                flags);
        }
    }
}
