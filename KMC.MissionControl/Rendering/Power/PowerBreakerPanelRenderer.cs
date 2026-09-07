using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// KMC 14.21.8 read-only breaker panel.
    ///
    /// This renderer consumes the live Engine-owned synthetic electrical
    /// distribution directly. It does not maintain a second breaker state and
    /// does not issue breaker commands.
    /// </summary>
    internal static class PowerBreakerPanelRenderer
    {
        private static readonly Color Healthy =
            Color.FromArgb(112, 202, 154);

        private static readonly Color Advisory =
            Color.FromArgb(232, 188, 84);

        private static readonly Color Critical =
            Color.FromArgb(236, 92, 76);

        private static readonly Color PanelFill =
            Color.FromArgb(255, 7, 18, 20);

        private static readonly FeedDefinition[] MainAFeeds =
        {
            new FeedDefinition("GEN A", "CONT_GEN_A"),
            new FeedDefinition("BAT A", "CONT_BAT_A")
        };

        private static readonly FeedDefinition[] EssentialFeeds =
        {
            new FeedDefinition("ESS A", "CONT_ESS_A"),
            new FeedDefinition("ESS B", "CONT_ESS_B")
        };

        private static readonly FeedDefinition[] MainBFeeds =
        {
            new FeedDefinition("GEN B", "CONT_GEN_B"),
            new FeedDefinition("BAT B", "CONT_BAT_B")
        };

        private static readonly BreakerDefinition[] MainA =
        {
            new BreakerDefinition("GUIDANCE A", "BRK_GUID_A"),
            new BreakerDefinition("COMM A", "BRK_COMM_A"),
            new BreakerDefinition("PROP FEED PUMP A", "BRK_PUMP_A"),
            new BreakerDefinition("CABIN FAN A", "BRK_CABIN_FAN_A"),
            new BreakerDefinition("THERMAL HEATER A", "BRK_THERMAL_HEATER_A")
        };

        private static readonly BreakerDefinition[] Essential =
        {
            new BreakerDefinition("FLIGHT COMPUTER", "BRK_FLIGHT_COMPUTER"),
            new BreakerDefinition("INSTRUMENTATION", "BRK_INSTRUMENTATION_ESS"),
            new BreakerDefinition("FLIGHT CONTROL", "BRK_FLIGHT_CONTROL"),
            new BreakerDefinition("REACTION WHEEL", "BRK_REACTION_WHEEL"),
            new BreakerDefinition("ENGINE CONTROL", "BRK_ENGINE_CONTROL"),
            new BreakerDefinition("STAGING CONTROL", "BRK_STAGING_CONTROL"),
            new BreakerDefinition("BRAKE CONTROL", "BRK_BRAKE_CONTROL"),
            new BreakerDefinition("GEAR CONTROL", "BRK_GEAR_CONTROL"),
            new BreakerDefinition("LIGHTING ESS", "BRK_LIGHTING_ESS"),
            new BreakerDefinition("RCS CONTROL", "BRK_RCS_CONTROL")
        };

        private static readonly BreakerDefinition[] MainB =
        {
            new BreakerDefinition("GUIDANCE B", "BRK_GUID_B"),
            new BreakerDefinition("COMM B", "BRK_COMM_B"),
            new BreakerDefinition("PROP FEED PUMP B", "BRK_PUMP_B"),
            new BreakerDefinition("CABIN FAN B", "BRK_CABIN_FAN_B"),
            new BreakerDefinition("THERMAL HEATER B", "BRK_THERMAL_HEATER_B")
        };

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                return;
            }

            Graphics graphics =
                context.Graphics;

            Rectangle content =
                context.ContentBounds;

            Rectangle area =
                new Rectangle(
                    content.Left + 14,
                    content.Top + 112,
                    Math.Max(0, content.Width - 28),
                    Math.Max(0, content.Height - 128));

            using (SolidBrush clear =
                new SolidBrush(
                    Color.FromArgb(255, 4, 15, 18)))
            {
                graphics.FillRectangle(
                    clear,
                    area);
            }

            DrawText(
                context,
                new Rectangle(
                    area.Left + 10,
                    area.Top - 42,
                    area.Width - 20,
                    34),
                "ELECTRICAL SYSTEMS / BREAKERS",
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            SyntheticElectricalDistributionModel distribution =
                engineering != null &&
                engineering.Snapshot != null &&
                engineering.Snapshot.SpacecraftSystems != null
                    ? engineering.Snapshot.SpacecraftSystems.ElectricalDistribution
                    : null;

            if (distribution == null)
            {
                DrawCentered(
                    context,
                    area,
                    "ELECTRICAL BREAKER DATA WAITING");
                return;
            }

            const int gap = 18;
            int usableWidth =
                Math.Max(
                    0,
                    area.Width -
                    gap * 2);

            int columnWidth =
                usableWidth / 3;

            Rectangle left =
                new Rectangle(
                    area.Left,
                    area.Top,
                    columnWidth,
                    area.Height);

            Rectangle middle =
                new Rectangle(
                    left.Right + gap,
                    area.Top,
                    columnWidth,
                    area.Height);

            Rectangle right =
                new Rectangle(
                    middle.Right + gap,
                    area.Top,
                    Math.Max(
                        0,
                        area.Right -
                        middle.Right -
                        gap),
                    area.Height);

            DrawBusPanel(
                context,
                left,
                "MAIN BUS A",
                MainAFeeds,
                MainA,
                distribution);

            DrawBusPanel(
                context,
                middle,
                "ESSENTIAL BUS",
                EssentialFeeds,
                Essential,
                distribution);

            DrawBusPanel(
                context,
                right,
                "MAIN BUS B",
                MainBFeeds,
                MainB,
                distribution);
        }

        private static void DrawBusPanel(
            MissionRenderContext context,
            Rectangle panel,
            string title,
            FeedDefinition[] feeds,
            BreakerDefinition[] definitions,
            SyntheticElectricalDistributionModel distribution)
        {
            if (panel.Width <= 0 ||
                panel.Height <= 0)
            {
                return;
            }

            using (SolidBrush fill =
                new SolidBrush(
                    PanelFill))
            {
                context.Graphics.FillRectangle(
                    fill,
                    panel);
            }

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        105,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawRectangle(
                    border,
                    panel);
            }

            Rectangle titleBox =
                new Rectangle(
                    panel.Left + 10,
                    panel.Top + 8,
                    Math.Max(0, panel.Width - 20),
                    32);

            DrawText(
                context,
                titleBox,
                title,
                context.PhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            int feedTop =
                titleBox.Bottom + 4;

            int bodyLeft =
                panel.Left + 12;

            int bodyWidth =
                Math.Max(
                    0,
                    panel.Width - 24);

            DrawFeedStrip(
                context,
                new Rectangle(
                    bodyLeft,
                    feedTop,
                    bodyWidth,
                    34),
                feeds,
                distribution);

            int headerTop =
                feedTop + 42;

            ColumnLayout columns =
                ColumnLayout.Create(
                    bodyLeft,
                    bodyWidth);

            DrawColumnHeader(
                context,
                headerTop,
                columns);

            int rowsTop =
                headerTop + 30;

            int availableHeight =
                Math.Max(
                    0,
                    panel.Bottom -
                    rowsTop -
                    10);

            int rowHeight =
                Math.Max(
                    36,
                    Math.Min(
                        72,
                        definitions.Length > 0
                            ? availableHeight /
                              definitions.Length
                            : availableHeight));

            for (int index = 0;
                 index < definitions.Length;
                 index++)
            {
                int top =
                    rowsTop +
                    index * rowHeight;

                if (top >= panel.Bottom - 8)
                {
                    break;
                }

                Rectangle row =
                    new Rectangle(
                        bodyLeft,
                        top,
                        bodyWidth,
                        Math.Min(
                            rowHeight,
                            panel.Bottom -
                            top -
                            8));

                DrawBreakerRow(
                    context,
                    row,
                    columns,
                    definitions[index],
                    distribution);
            }
        }

        private static void DrawFeedStrip(
            MissionRenderContext context,
            Rectangle area,
            FeedDefinition[] feeds,
            SyntheticElectricalDistributionModel distribution)
        {
            if (feeds == null ||
                feeds.Length == 0 ||
                area.Width <= 0 ||
                area.Height <= 0)
            {
                return;
            }

            int halfWidth =
                area.Width / 2;

            for (int index = 0;
                 index < feeds.Length &&
                 index < 2;
                 index++)
            {
                FeedDefinition definition =
                    feeds[index];

                Rectangle item =
                    new Rectangle(
                        area.Left + index * halfWidth,
                        area.Top,
                        index == 1
                            ? area.Right - (area.Left + halfWidth)
                            : halfWidth,
                        area.Height);

                SyntheticElectricalSwitch feed =
                    distribution != null
                        ? distribution.FindSwitch(
                            definition.SwitchId)
                        : null;

                string command =
                    feed != null
                        ? feed.CommandedClosed
                            ? "CL"
                            : "OP"
                        : "--";

                string indication =
                    feed != null
                        ? feed.IndicatedClosed
                            ? "CL"
                            : "OP"
                        : "--";

                Color color =
                    feed == null
                        ? context.DimPhosphorColor
                        : feed.CommandedClosed !=
                          feed.IndicatedClosed
                            ? Critical
                            : feed.IndicatedClosed
                                ? Healthy
                                : Advisory;

                // COMPACT POWER FEED STRIP
                // Keep NAME / CMD / IND grouped together.
                int nameWidth =
                    item.Width * 22 / 100;

                int commandWidth =
                    item.Width * 27 / 100;

                int indicationWidth =
                    item.Width * 27 / 100;

                // RIGHT-ALIGN SECOND FEED GROUP
                int groupWidth =
                    nameWidth +
                    commandWidth +
                    indicationWidth;

                int groupLeft =
                    index == 1
                        ? item.Right -
                          groupWidth
                        : item.Left;

                DrawText(
                    context,
                    new Rectangle(
                        groupLeft,
                        item.Top,
                        nameWidth,
                        item.Height),
                    definition.Name,
                    color,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                DrawText(
                    context,
                    new Rectangle(
                        groupLeft + nameWidth,
                        item.Top,
                        commandWidth,
                        item.Height),
                    "CMD " + command,
                    color,
                    CenterFlags());

                DrawText(
                    context,
                    new Rectangle(
                        groupLeft + nameWidth + commandWidth,
                        item.Top,
                        indicationWidth,
                        item.Height),
                    "IND " + indication,
                    color,
                    CenterFlags());
            }

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        72,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawLine(
                    divider,
                    area.Left,
                    area.Bottom + 2,
                    area.Right,
                    area.Bottom + 2);
            }
        }

        private static void DrawColumnHeader(
            MissionRenderContext context,
            int top,
            ColumnLayout columns)
        {
            DrawText(
                context,
                new Rectangle(
                    columns.CommandLeft,
                    top,
                    columns.CommandWidth,
                    28),
                "CMD",
                context.DimPhosphorColor,
                CenterFlags());

            DrawText(
                context,
                new Rectangle(
                    columns.IndicationLeft,
                    top,
                    columns.IndicationWidth,
                    28),
                "IND",
                context.DimPhosphorColor,
                CenterFlags());

            DrawText(
                context,
                new Rectangle(
                    columns.StateLeft,
                    top,
                    columns.StateWidth,
                    28),
                "STATE",
                context.DimPhosphorColor,
                CenterFlags());

            DrawText(
                context,
                new Rectangle(
                    columns.LoadLeft,
                    top,
                    columns.LoadWidth,
                    28),
                "LOAD",
                context.DimPhosphorColor,
                CenterFlags());
        }

        private static void DrawBreakerRow(
            MissionRenderContext context,
            Rectangle row,
            ColumnLayout columns,
            BreakerDefinition definition,
            SyntheticElectricalDistributionModel distribution)
        {
            SyntheticElectricalSwitch breaker =
                distribution.FindSwitch(
                    definition.BreakerId);

            SyntheticElectricalLoad load =
                FindLoad(
                    distribution,
                    definition.BreakerId);

            bool known =
                breaker != null &&
                load != null;

            string command =
                known
                    ? breaker.CommandedClosed
                        ? "CLOSED"
                        : "OPEN"
                    : "--";

            string indication =
                known
                    ? breaker.IndicatedClosed
                        ? "CLOSED"
                        : "OPEN"
                    : "--";

            string state =
                known
                    ? breaker.Conducting
                        ? "POWERED"
                        : "UNPOWERED"
                    : "UNKNOWN";

            double displayedAmps =
                known &&
                breaker.Conducting
                    ? Math.Max(
                        0.0,
                        load.DemandAmps)
                    : 0.0;

            string loadText =
                known
                    ? displayedAmps.ToString("0.0") + "A"
                    : "--";

            Color stateColor =
                !known
                    ? context.DimPhosphorColor
                    : breaker.CommandedClosed !=
                      breaker.IndicatedClosed
                        ? Critical
                        : breaker.Conducting
                            ? Healthy
                            : Advisory;

            DrawText(
                context,
                new Rectangle(
                    columns.NameLeft + 4,
                    row.Top,
                    Math.Max(
                        0,
                        columns.NameWidth - 8),
                    row.Height),
                definition.Name,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawValue(
                context,
                row,
                columns.CommandLeft,
                columns.CommandWidth,
                command,
                stateColor);

            DrawValue(
                context,
                row,
                columns.IndicationLeft,
                columns.IndicationWidth,
                indication,
                stateColor);

            DrawValue(
                context,
                row,
                columns.StateLeft,
                columns.StateWidth,
                state,
                stateColor);

            DrawValue(
                context,
                row,
                columns.LoadLeft,
                columns.LoadWidth,
                loadText,
                stateColor);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        48,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawLine(
                    divider,
                    row.Left,
                    row.Bottom - 1,
                    row.Right,
                    row.Bottom - 1);
            }
        }

        private static SyntheticElectricalLoad FindLoad(
            SyntheticElectricalDistributionModel distribution,
            string breakerId)
        {
            if (distribution == null ||
                string.IsNullOrWhiteSpace(
                    breakerId))
            {
                return null;
            }

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    distribution.Loads[index];

                if (load != null &&
                    string.Equals(
                        load.BreakerId,
                        breakerId,
                        StringComparison.Ordinal))
                {
                    return load;
                }
            }

            return null;
        }

        private static void DrawValue(
            MissionRenderContext context,
            Rectangle row,
            int left,
            int width,
            string value,
            Color color)
        {
            DrawText(
                context,
                new Rectangle(
                    left,
                    row.Top,
                    width,
                    row.Height),
                value,
                color,
                CenterFlags());
        }

        private static TextFormatFlags CenterFlags()
        {
            return
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis;
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle area,
            string text)
        {
            DrawText(
                context,
                area,
                text,
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawText(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color,
            TextFormatFlags flags)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            TextRenderer.DrawText(
                context.Graphics,
                text ?? string.Empty,
                context.SmallFont,
                bounds,
                color,
                flags);
        }

        private sealed class FeedDefinition
        {
            public FeedDefinition(
                string name,
                string switchId)
            {
                Name =
                    name ?? string.Empty;

                SwitchId =
                    switchId ?? string.Empty;
            }

            public string Name
            {
                get;
                private set;
            }

            public string SwitchId
            {
                get;
                private set;
            }
        }

        private sealed class BreakerDefinition
        {
            public BreakerDefinition(
                string name,
                string breakerId)
            {
                Name =
                    name ?? string.Empty;

                BreakerId =
                    breakerId ?? string.Empty;
            }

            public string Name
            {
                get;
                private set;
            }

            public string BreakerId
            {
                get;
                private set;
            }
        }

        private sealed class ColumnLayout
        {
            public int NameLeft { get; private set; }
            public int NameWidth { get; private set; }
            public int CommandLeft { get; private set; }
            public int CommandWidth { get; private set; }
            public int IndicationLeft { get; private set; }
            public int IndicationWidth { get; private set; }
            public int StateLeft { get; private set; }
            public int StateWidth { get; private set; }
            public int LoadLeft { get; private set; }
            public int LoadWidth { get; private set; }

            public static ColumnLayout Create(
                int left,
                int width)
            {
                int nameWidth =
                    width * 42 / 100;

                int commandWidth =
                    width * 13 / 100;

                int indicationWidth =
                    width * 13 / 100;

                int stateWidth =
                    width * 20 / 100;

                int loadWidth =
                    Math.Max(
                        0,
                        width -
                        nameWidth -
                        commandWidth -
                        indicationWidth -
                        stateWidth);

                ColumnLayout layout =
                    new ColumnLayout();

                layout.NameLeft =
                    left;
                layout.NameWidth =
                    nameWidth;

                layout.CommandLeft =
                    layout.NameLeft +
                    layout.NameWidth;
                layout.CommandWidth =
                    commandWidth;

                layout.IndicationLeft =
                    layout.CommandLeft +
                    layout.CommandWidth;
                layout.IndicationWidth =
                    indicationWidth;

                layout.StateLeft =
                    layout.IndicationLeft +
                    layout.IndicationWidth;
                layout.StateWidth =
                    stateWidth;

                layout.LoadLeft =
                    layout.StateLeft +
                    layout.StateWidth;
                layout.LoadWidth =
                    loadWidth;

                return layout;
            }
        }
    }
}