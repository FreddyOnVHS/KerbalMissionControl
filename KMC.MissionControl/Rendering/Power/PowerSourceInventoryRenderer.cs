using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.14.2 real KSP electrical-source inventory for POWER 2/2.
    ///
    /// The underlying attribution telemetry already identifies individual
    /// producer parts. This renderer exposes that evidence without changing the
    /// mature POWER 1/2 one-line or inventing synthetic A/B assignments.
    /// </summary>
    internal static class PowerSourceInventoryRenderer
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

        private static readonly Color PanelFill =
            Color.FromArgb(
                255,
                7,
                18,
                20);

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null ||
                engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.Power == null)
            {
                return;
            }

            ElectricalAttributionModel attribution =
                engineering.Snapshot.Power.Attribution;

            /*
             * Match the top-left POWER 2/2 panel geometry used by
             * PowerPageRenderer. Drawing this panel after the base detail
             * renderer intentionally replaces only the generic source summary.
             */
            Rectangle area =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 80);

            int topHeight =
                area.Height * 31 / 100;

            int sourceWidth =
                area.Width * 25 / 100;

            Rectangle panel =
                new Rectangle(
                    area.Left,
                    area.Top,
                    sourceWidth,
                    topHeight);

            DrawPanel(
                context,
                panel,
                attribution);
        }

        private static void DrawPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalAttributionModel attribution)
        {
            Graphics graphics =
                context.Graphics;

            int smallHeight =
                Math.Max(
                    1,
                    TextRenderer.MeasureText(
                        graphics,
                        "Ag",
                        context.SmallFont,
                        new Size(
                            int.MaxValue,
                            int.MaxValue),
                        TextFormatFlags.NoPadding)
                    .Height);

            int titleHeight =
                smallHeight + 12;

            using (SolidBrush fill =
                new SolidBrush(
                    PanelFill))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        150,
                        context.DimPhosphorColor),
                    1.4f))
            {
                graphics.FillRectangle(
                    fill,
                    panel);

                graphics.DrawRectangle(
                    border,
                    panel);

                DrawText(
                    graphics,
                    new Rectangle(
                        panel.Left + 12,
                        panel.Top + 5,
                        panel.Width - 24,
                        smallHeight + 4),
                    "REAL KSP GENERATION SOURCES",
                    context.SmallFont,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                graphics.DrawLine(
                    border,
                    panel.Left + 10,
                    panel.Top + titleHeight,
                    panel.Right - 10,
                    panel.Top + titleHeight);
            }

            Rectangle body =
                new Rectangle(
                    panel.Left + 12,
                    panel.Top + titleHeight + 6,
                    Math.Max(
                        0,
                        panel.Width - 24),
                    Math.Max(
                        0,
                        panel.Height -
                        titleHeight -
                        18));

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                DrawCentered(
                    graphics,
                    body,
                    "SOURCE TELEMETRY WAITING",
                    context,
                    context.DimPhosphorColor);

                return;
            }

            int producerCount = 0;
            int activeCount = 0;

            for (int index = 0;
                 index < attribution.Entries.Count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    attribution.Entries[index];

                if (entry == null ||
                    entry.Kind !=
                        ElectricalAttributionKind.Producer)
                {
                    continue;
                }

                producerCount++;

                if (IsProducing(
                        entry))
                {
                    activeCount++;
                }
            }

            int summaryHeight =
                smallHeight + 18;

            Rectangle summary =
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    summaryHeight);

            int half =
                summary.Width / 2;

            DrawText(
                graphics,
                new Rectangle(
                    summary.Left,
                    summary.Top,
                    half,
                    summary.Height),
                "PRODUCERS " +
                producerCount.ToString() +
                " / ACTIVE " +
                activeCount.ToString(),
                context.SmallFont,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                graphics,
                new Rectangle(
                    summary.Left + half,
                    summary.Top,
                    summary.Width - half,
                    summary.Height),
                "GEN " +
                attribution.KnownCurrentGenerationEcPerSecond
                    .ToString("0.###") +
                " / MAX " +
                attribution.DeclaredMaximumGenerationEcPerSecond
                    .ToString("0.###") +
                " EC/S",
                context.SmallFont,
                context.PhosphorColor,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            Rectangle table =
                new Rectangle(
                    body.Left,
                    summary.Bottom + 4,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        summary.Bottom -
                        4));

            DrawSourceTable(
                graphics,
                table,
                attribution,
                context,
                smallHeight);
        }

        private static void DrawSourceTable(
            Graphics graphics,
            Rectangle table,
            ElectricalAttributionModel attribution,
            MissionRenderContext context,
            int smallHeight)
        {
            if (table.Width <= 0 ||
                table.Height <= 0)
            {
                return;
            }

            /*
             * Build 14.14.2A:
             * Do not force long KSP part titles through narrow table columns.
             * Each producer gets a compact multi-line block with the full panel
             * width available to the device title.
             */
            int metaHeight =
                smallHeight + 8;

            int deviceHeight =
                smallHeight * 2 + 8;

            int blockHeight =
                deviceHeight +
                metaHeight * 2 +
                6;

            int blocksAvailable =
                Math.Max(
                    1,
                    table.Height /
                    Math.Max(
                        1,
                        blockHeight));

            int shown = 0;
            int total = 0;
            int y =
                table.Top;

            for (int index = 0;
                 index < attribution.Entries.Count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    attribution.Entries[index];

                if (entry == null ||
                    entry.Kind !=
                        ElectricalAttributionKind.Producer)
                {
                    continue;
                }

                total++;

                if (shown >=
                    blocksAvailable ||
                    y + blockHeight >
                        table.Bottom)
                {
                    continue;
                }

                DrawSourceBlock(
                    graphics,
                    new Rectangle(
                        table.Left,
                        y,
                        table.Width,
                        blockHeight),
                    entry,
                    context,
                    smallHeight);

                shown++;
                y +=
                    blockHeight;
            }

            if (total == 0)
            {
                DrawCentered(
                    graphics,
                    table,
                    "NO REAL EC PRODUCERS DISCOVERED",
                    context,
                    context.DimPhosphorColor);

                return;
            }

            if (total > shown)
            {
                DrawText(
                    graphics,
                    new Rectangle(
                        table.Left,
                        Math.Max(
                            table.Top,
                            table.Bottom -
                            metaHeight),
                        table.Width,
                        metaHeight),
                    "+" +
                    (total - shown).ToString() +
                    " MORE SOURCE" +
                    (total - shown == 1
                        ? string.Empty
                        : "S"),
                    context.SmallFont,
                    context.DimPhosphorColor,
                    TextFormatFlags.Right |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);
            }
        }

        private static void DrawSourceBlock(
            Graphics graphics,
            Rectangle block,
            ElectricalAttributionEntry entry,
            MissionRenderContext context,
            int smallHeight)
        {
            int metaHeight =
                smallHeight + 8;

            int deviceHeight =
                smallHeight * 2 + 8;

            Rectangle device =
                new Rectangle(
                    block.Left,
                    block.Top,
                    block.Width,
                    deviceHeight);

            string title =
                string.IsNullOrWhiteSpace(
                    entry.PartTitle)
                    ? "PART #" +
                      entry.PartId.ToString()
                    : entry.PartTitle;

            DrawText(
                graphics,
                device,
                title,
                context.SmallFont,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoClipping);

            Rectangle identity =
                new Rectangle(
                    block.Left,
                    device.Bottom,
                    block.Width,
                    metaHeight);

            string type =
                string.IsNullOrWhiteSpace(
                    entry.Category)
                    ? "OTHER"
                    : entry.Category;

            DrawText(
                graphics,
                identity,
                "TYPE " +
                type +
                "   PART #" +
                entry.PartId.ToString(),
                context.SmallFont,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            Rectangle status =
                new Rectangle(
                    block.Left,
                    identity.Bottom,
                    block.Width,
                    metaHeight);

            string rate =
                SourceRate(
                    entry);

            Color stateColor =
                SourceStateColor(
                    entry,
                    context);

            DrawText(
                graphics,
                status,
                "STATE " +
                SourceState(
                    entry) +
                "   OUT / MAX " +
                rate +
                " EC/S",
                context.SmallFont,
                stateColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        65,
                        context.DimPhosphorColor),
                    1.0f))
            {
                graphics.DrawLine(
                    divider,
                    block.Left,
                    block.Bottom - 2,
                    block.Right,
                    block.Bottom - 2);
            }
        }

        private static bool IsProducing(
            ElectricalAttributionEntry entry)
        {
            return
                entry != null &&
                entry.Enabled &&
                entry.CurrentRateKnown &&
                entry.CurrentRateEcPerSecond >
                    0.000001;
        }

        private static string SourceState(
            ElectricalAttributionEntry entry)
        {
            if (entry == null)
            {
                return
                    "UNKNOWN";
            }

            if (!entry.Enabled)
            {
                return
                    "DISABLED";
            }

            if (IsProducing(
                    entry))
            {
                return
                    "PRODUCING";
            }

            if (entry.ActiveStateKnown &&
                !entry.Active)
            {
                return
                    "INACTIVE";
            }

            if (entry.CurrentRateKnown)
            {
                /*
                 * We know output is zero, but do not invent the reason.
                 * For example, a solar panel may be deployed but shadowed.
                 */
                return
                    "NO OUTPUT";
            }

            if (entry.ActiveStateKnown &&
                entry.Active)
            {
                return
                    "ACTIVE";
            }

            if (entry.MaximumRateKnown)
            {
                return
                    "AVAILABLE";
            }

            return
                "STATE UNKNOWN";
        }

        private static Color SourceStateColor(
            ElectricalAttributionEntry entry,
            MissionRenderContext context)
        {
            if (entry == null)
            {
                return
                    Advisory;
            }

            if (IsProducing(
                    entry))
            {
                return
                    Healthy;
            }

            if (!entry.Enabled ||
                (entry.ActiveStateKnown &&
                 !entry.Active))
            {
                return
                    context.DimPhosphorColor;
            }

            if (!entry.CurrentRateKnown &&
                !entry.ActiveStateKnown)
            {
                return
                    Advisory;
            }

            return
                context.PhosphorColor;
        }

        private static string SourceRate(
            ElectricalAttributionEntry entry)
        {
            if (entry == null)
            {
                return
                    "--";
            }

            string current =
                entry.CurrentRateKnown
                    ? entry.CurrentRateEcPerSecond
                        .ToString("0.###")
                    : "--";

            string maximum =
                entry.MaximumRateKnown
                    ? entry.MaximumRateEcPerSecond
                        .ToString("0.###")
                    : "--";

            return
                current +
                " / " +
                maximum;
        }

        private static void DrawCell(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color,
            bool rightAligned)
        {
            DrawText(
                graphics,
                new Rectangle(
                    bounds.Left + 3,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width - 6),
                    bounds.Height),
                text,
                font,
                color,
                (rightAligned
                    ? TextFormatFlags.Right
                    : TextFormatFlags.Left) |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawCentered(
            Graphics graphics,
            Rectangle bounds,
            string text,
            MissionRenderContext context,
            Color color)
        {
            DrawText(
                graphics,
                bounds,
                text,
                context.SmallFont,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawText(
            Graphics graphics,
            Rectangle bounds,
            string text,
            Font font,
            Color color,
            TextFormatFlags flags)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            TextRenderer.DrawText(
                graphics,
                string.IsNullOrWhiteSpace(
                    text)
                    ? "---"
                    : text
                        .Trim()
                        .ToUpperInvariant(),
                font,
                bounds,
                color,
                flags);
        }
    }
}
