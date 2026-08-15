using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.14.2A POWER 2/2 distribution-bus overlay.
    ///
    /// The legacy detail page's aggregate electrical diagnostic predates the
    /// switched A/B/ESS distribution model. POWER 1/2 already uses the newer
    /// model, so 2/2 must consume that exact same bus truth to avoid contradictory
    /// operator evidence.
    /// </summary>
    internal static class PowerDistributionBusDetailRenderer
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
                110,
                125,
                120);

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
                engineering.Snapshot.SpacecraftSystems == null ||
                engineering.Snapshot.SpacecraftSystems.ElectricalDistribution == null)
            {
                return;
            }

            SyntheticElectricalDistributionModel distribution =
                engineering.Snapshot.SpacecraftSystems.ElectricalDistribution;

            Rectangle area =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 80);

            int gap =
                Math.Max(
                    12,
                    area.Width / 160);

            int topHeight =
                area.Height * 31 / 100;

            int sourceWidth =
                area.Width * 25 / 100;

            int loadWidth =
                area.Width * 31 / 100;

            Rectangle sources =
                new Rectangle(
                    area.Left,
                    area.Top,
                    sourceWidth,
                    topHeight);

            Rectangle loads =
                new Rectangle(
                    area.Right -
                    loadWidth,
                    area.Top,
                    loadWidth,
                    topHeight);

            Rectangle panel =
                new Rectangle(
                    sources.Right + gap,
                    area.Top,
                    loads.Left -
                    sources.Right -
                    gap * 2,
                    topHeight);

            DrawPanel(
                context,
                panel,
                distribution);
        }

        private static void DrawPanel(
            MissionRenderContext context,
            Rectangle panel,
            SyntheticElectricalDistributionModel distribution)
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
                    "DISTRIBUTION BUSES / A-B-ESS",
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

            int rowHeight =
                Math.Max(
                    smallHeight * 2 + 14,
                    body.Height / 3);

            DrawBus(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    rowHeight),
                distribution.FindBus(
                    "BUS_MAIN_A"),
                context,
                smallHeight);

            DrawBus(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + rowHeight,
                    body.Width,
                    rowHeight),
                distribution.FindBus(
                    "BUS_MAIN_B"),
                context,
                smallHeight);

            DrawBus(
                graphics,
                new Rectangle(
                    body.Left,
                    body.Top + rowHeight * 2,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        (body.Top + rowHeight * 2))),
                distribution.FindBus(
                    "BUS_ESS"),
                context,
                smallHeight);
        }

        private static void DrawBus(
            Graphics graphics,
            Rectangle row,
            SyntheticElectricalBus bus,
            MissionRenderContext context,
            int smallHeight)
        {
            if (row.Width <= 0 ||
                row.Height <= 0)
            {
                return;
            }

            Color color =
                BusColor(
                    bus,
                    context);

            string name =
                bus != null &&
                !string.IsNullOrWhiteSpace(
                    bus.DisplayName)
                    ? bus.DisplayName
                    : "BUS --";

            string state =
                bus != null
                    ? SplitWords(
                        bus.State.ToString())
                    : "UNAVAILABLE";

            string voltage =
                bus != null
                    ? bus.Voltage.ToString("0.0") +
                      " V"
                    : "--";

            Rectangle headline =
                new Rectangle(
                    row.Left,
                    row.Top,
                    row.Width,
                    smallHeight + 8);

            DrawText(
                graphics,
                headline,
                name +
                "   " +
                state +
                "   " +
                voltage,
                context.SmallFont,
                color,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            string source =
                bus != null &&
                !string.IsNullOrWhiteSpace(
                    bus.ActiveSourceId)
                    ? bus.ActiveSourceId
                        .Replace(
                            "SRC_",
                            string.Empty)
                    : "NONE";

            string demand =
                bus != null
                    ? bus.DemandAmps.ToString("0.0") +
                      "/" +
                      bus.AvailableCurrentAmps.ToString("0.0") +
                      " A"
                    : "--";

            string load =
                bus == null ||
                bus.AvailableCurrentAmps <=
                    0.000001 ||
                bus.State ==
                    SyntheticElectricalBusState.Unpowered ||
                bus.State ==
                    SyntheticElectricalBusState.Failed
                    ? "--"
                    : bus.LoadPercent.ToString("0") +
                      "%";

            string autoShed =
                bus != null &&
                bus.ShedDemandAmps > 0.01
                    ? bus.ShedDemandAmps
                        .ToString("0.0") +
                      " A"
                    : "--";

            string manualShed =
                bus != null &&
                bus.ManualShedDemandAmps > 0.01
                    ? bus.ManualShedDemandAmps
                        .ToString("0.0") +
                      " A"
                    : "--";

            Rectangle detail =
                new Rectangle(
                    row.Left,
                    headline.Bottom,
                    row.Width,
                    Math.Max(
                        0,
                        row.Bottom -
                        headline.Bottom));

            DrawText(
                graphics,
                detail,
                "SRC " +
                source +
                "   DEM " +
                demand +
                "   LOAD " +
                load +
                "   AUTO " +
                autoShed +
                "   MAN " +
                manualShed,
                context.SmallFont,
                context.PhosphorColor,
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
                    row.Left,
                    row.Bottom - 1,
                    row.Right,
                    row.Bottom - 1);
            }
        }

        private static Color BusColor(
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
                    return
                        Healthy;

                case SyntheticElectricalBusState.HighLoad:
                    return
                        Advisory;

                case SyntheticElectricalBusState.Overloaded:
                    return
                        Warning;

                case SyntheticElectricalBusState.Undervoltage:
                    return
                        Critical;

                case SyntheticElectricalBusState.Failed:
                    return
                        Critical;

                case SyntheticElectricalBusState.Unpowered:
                    return
                        Dead;

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
                return
                    "---";
            }

            StringBuilder builder =
                new StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char current =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(
                        current) &&
                    !char.IsUpper(
                        value[index - 1]))
                {
                    builder.Append(
                        ' ');
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
