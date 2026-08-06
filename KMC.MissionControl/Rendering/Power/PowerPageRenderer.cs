using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using KMC.MissionControl.Debugging.Electrical;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    public static class PowerPageRenderer
    {
        private sealed class SectionPlacement
        {
            public ElectricalSectionModel Section;
            public Rectangle Bounds;
            public bool IsLeft;
        }

        public static void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry,
            ElectricalTopologyModel model)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            Graphics graphics =
                context.Graphics;

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            MissionPageLayout pageLayout =
                new MissionPageLayout(
                    context);

            pageLayout.DrawHeader(
                "ELECTRICAL POWER",
                "CH 05");

            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 78,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 98);

            int summaryHeight =
                142;

            Rectangle summaryBounds =
                new Rectangle(
                    working.Left,
                    working.Top,
                    working.Width,
                    summaryHeight);

            int detailWidth =
                Math.Max(
                    500,
                    (int)Math.Round(
                        working.Width *
                        0.30));

            Rectangle stackBounds =
                new Rectangle(
                    working.Left,
                    summaryBounds.Bottom + 12,
                    working.Width -
                    detailWidth -
                    12,
                    working.Bottom -
                    summaryBounds.Bottom -
                    12);

            Rectangle detailBounds =
                new Rectangle(
                    stackBounds.Right + 12,
                    stackBounds.Top,
                    detailWidth,
                    stackBounds.Height);

            DrawPanel(
                graphics,
                summaryBounds,
                context,
                "ELECTRICAL SYSTEM SUMMARY");

            DrawPanel(
                graphics,
                stackBounds,
                context,
                "SPACECRAFT POWER MAP");

            DrawPanel(
                graphics,
                detailBounds,
                context,
                "SECTION DETAIL");

            DrawSummary(
                graphics,
                summaryBounds,
                context,
                model);

            if (model == null ||
                model.Sections.Count == 0)
            {
                DrawWaitingState(
                    graphics,
                    stackBounds,
                    context);

                DrawNoDataDetails(
                    graphics,
                    detailBounds,
                    context);

                return;
            }

            List<SectionPlacement> placements =
                CalculatePlacements(
                    stackBounds,
                    model);

            DrawStackConnections(
                graphics,
                placements);

            DrawStackSections(
                graphics,
                placements,
                context);

            ElectricalSectionModel primary =
                SelectPrimarySection(
                    model);

            DrawPrimarySectionDetail(
                graphics,
                detailBounds,
                primary,
                model,
                context);
        }

        private static void DrawSummary(
            Graphics graphics,
            Rectangle bounds,
            MissionRenderContext context,
            ElectricalTopologyModel model)
        {
            Rectangle content =
                new Rectangle(
                    bounds.Left + 16,
                    bounds.Top + 42,
                    bounds.Width - 32,
                    bounds.Height - 52);

            int sectionCount =
                model != null
                    ? model.Sections.Count
                    : 0;

            int batteryCount =
                model != null
                    ? model.Sections.Sum(
                        section =>
                            section.BatteryPartCount)
                    : 0;

            int solarCount =
                model != null
                    ? model.Sections.Sum(
                        section =>
                            section.SolarPartCount)
                    : 0;

            double amount =
                model != null
                    ? model.Sections.Sum(
                        section =>
                            section.ElectricChargeAmount)
                    : 0.0;

            double capacity =
                model != null
                    ? model.Sections.Sum(
                        section =>
                            section.ElectricChargeCapacity)
                    : 0.0;

            double percent =
                capacity > 0.0001
                    ? amount /
                      capacity *
                      100.0
                    : 0.0;

            int cellWidth =
                content.Width /
                4;

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left,
                    content.Top,
                    cellWidth,
                    content.Height),
                "VESSEL CHARGE",
                percent.ToString("0") + "%",
                amount.ToString("0.0") +
                " / " +
                capacity.ToString("0.0") +
                " EC",
                ResolvePowerColor(
                    percent,
                    capacity),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    cellWidth,
                    content.Top,
                    cellWidth,
                    content.Height),
                "STACK SECTIONS",
                sectionCount.ToString("00"),
                "TOPOLOGY GROUPS",
                context.PhosphorColor,
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    cellWidth *
                    2,
                    content.Top,
                    cellWidth,
                    content.Height),
                "BATTERIES",
                batteryCount.ToString("00"),
                "PART LOCATIONS",
                Color.FromArgb(
                    255,
                    190,
                    55),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    cellWidth *
                    3,
                    content.Top,
                    content.Width -
                    cellWidth *
                    3,
                    content.Height),
                "SOLAR ARRAYS",
                solarCount.ToString("00"),
                model != null &&
                !string.IsNullOrEmpty(
                    model.VesselName)
                    ? model.VesselName
                    : "WAITING",
                Color.FromArgb(
                    80,
                    205,
                    255),
                context);
        }

        private static List<SectionPlacement>
            CalculatePlacements(
                Rectangle panel,
                ElectricalTopologyModel model)
        {
            List<ElectricalSectionModel> core =
                model.Sections
                    .Where(
                        section =>
                            !section.IsRadialSection)
                    .OrderByDescending(
                        section =>
                            section.AverageY)
                    .ToList();

            List<ElectricalSectionModel> radial =
                model.Sections
                    .Where(
                        section =>
                            section.IsRadialSection)
                    .OrderByDescending(
                        section =>
                            section.AverageY)
                    .ToList();

            Rectangle content =
                new Rectangle(
                    panel.Left + 34,
                    panel.Top + 62,
                    panel.Width - 68,
                    panel.Height - 86);

            int coreCount =
                Math.Max(
                    1,
                    core.Count);

            int gap =
                coreCount >= 6
                    ? 7
                    : 12;

            int availableHeight =
                content.Height -
                gap *
                Math.Max(
                    0,
                    coreCount - 1);

            int sectionHeight =
                Math.Max(
                    94,
                    Math.Min(
                        150,
                        availableHeight /
                        coreCount));

            int coreWidth =
                Math.Max(
                    300,
                    Math.Min(
                        450,
                        content.Width /
                        3));

            int centerX =
                content.Left +
                content.Width /
                2;

            int totalHeight =
                sectionHeight *
                coreCount +
                gap *
                Math.Max(
                    0,
                    coreCount - 1);

            int startY =
                content.Top +
                Math.Max(
                    0,
                    (content.Height -
                     totalHeight) /
                    2);

            List<SectionPlacement> placements =
                new List<SectionPlacement>();

            for (int index = 0;
                 index < core.Count;
                 index++)
            {
                placements.Add(
                    new SectionPlacement
                    {
                        Section =
                            core[index],

                        Bounds =
                            new Rectangle(
                                centerX -
                                coreWidth /
                                2,
                                startY +
                                index *
                                (sectionHeight +
                                 gap),
                                coreWidth,
                                sectionHeight)
                    });
            }

            Dictionary<int, int> sideUse =
                new Dictionary<int, int>();

            for (int index = 0;
                 index < radial.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    radial[index];

                int coreIndex =
                    FindNearestCoreIndex(
                        core,
                        section);

                Rectangle anchor =
                    placements.Count > 0
                        ? placements[
                            Math.Max(
                                0,
                                Math.Min(
                                    placements.Count - 1,
                                    coreIndex))]
                            .Bounds
                        : new Rectangle(
                            centerX -
                            coreWidth /
                            2,
                            content.Top,
                            coreWidth,
                            sectionHeight);

                int useCount;

                if (!sideUse.TryGetValue(
                        coreIndex,
                        out useCount))
                {
                    useCount =
                        0;
                }

                sideUse[coreIndex] =
                    useCount + 1;

                bool left =
                    useCount %
                    2 ==
                    0;

                int radialWidth =
                    Math.Max(
                        210,
                        coreWidth -
                        105);

                int verticalOffset =
                    (useCount /
                     2) *
                    Math.Max(
                        10,
                        sectionHeight /
                        4);

                Rectangle bounds =
                    new Rectangle(
                        left
                            ? anchor.Left -
                              radialWidth -
                              34
                            : anchor.Right +
                              34,
                        Math.Min(
                            content.Bottom -
                            sectionHeight,
                            anchor.Top +
                            verticalOffset),
                        radialWidth,
                        sectionHeight);

                placements.Add(
                    new SectionPlacement
                    {
                        Section =
                            section,

                        Bounds =
                            bounds,

                        IsLeft =
                            left
                    });
            }

            return placements;
        }

        private static int FindNearestCoreIndex(
            List<ElectricalSectionModel> core,
            ElectricalSectionModel radial)
        {
            if (core.Count == 0)
            {
                return 0;
            }

            int bestIndex =
                0;

            double bestDistance =
                double.MaxValue;

            for (int index = 0;
                 index < core.Count;
                 index++)
            {
                double distance =
                    Math.Abs(
                        core[index].AverageY -
                        radial.AverageY);

                if (distance <
                    bestDistance)
                {
                    bestDistance =
                        distance;

                    bestIndex =
                        index;
                }
            }

            return bestIndex;
        }

        private static void DrawStackConnections(
            Graphics graphics,
            List<SectionPlacement> placements)
        {
            List<SectionPlacement> core =
                placements
                    .Where(
                        placement =>
                            !placement.Section
                                .IsRadialSection)
                    .OrderBy(
                        placement =>
                            placement.Bounds.Top)
                    .ToList();

            using (Pen corePen =
                new Pen(
                    Color.FromArgb(
                        95,
                        160,
                        170),
                    2.0f))
            using (Pen radialPen =
                new Pen(
                    Color.FromArgb(
                        75,
                        125,
                        135),
                    1.4f))
            {
                for (int index = 0;
                     index < core.Count - 1;
                     index++)
                {
                    int centerX =
                        core[index]
                            .Bounds.Left +
                        core[index]
                            .Bounds.Width /
                        2;

                    graphics.DrawLine(
                        corePen,
                        centerX,
                        core[index]
                            .Bounds.Bottom,
                        centerX,
                        core[index + 1]
                            .Bounds.Top);
                }

                for (int index = 0;
                     index < placements.Count;
                     index++)
                {
                    SectionPlacement radial =
                        placements[index];

                    if (!radial.Section
                            .IsRadialSection)
                    {
                        continue;
                    }

                    SectionPlacement nearest =
                        core
                            .OrderBy(
                                placement =>
                                    Math.Abs(
                                        placement.Section
                                            .AverageY -
                                        radial.Section
                                            .AverageY))
                            .FirstOrDefault();

                    if (nearest == null)
                    {
                        continue;
                    }

                    graphics.DrawLine(
                        radialPen,
                        radial.IsLeft
                            ? radial.Bounds.Right
                            : radial.Bounds.Left,
                        radial.Bounds.Top +
                        radial.Bounds.Height /
                        2,
                        radial.IsLeft
                            ? nearest.Bounds.Left
                            : nearest.Bounds.Right,
                        nearest.Bounds.Top +
                        nearest.Bounds.Height /
                        2);
                }
            }
        }

        private static void DrawStackSections(
            Graphics graphics,
            List<SectionPlacement> placements,
            MissionRenderContext context)
        {
            for (int index = 0;
                 index < placements.Count;
                 index++)
            {
                DrawSectionCard(
                    graphics,
                    placements[index].Bounds,
                    placements[index].Section,
                    context);
            }
        }

        private static void DrawSectionCard(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section,
            MissionRenderContext context)
        {
            double percent =
                section.ElectricChargePercent;

            Color state =
                ResolvePowerColor(
                    percent,
                    section.ElectricChargeCapacity);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        13,
                        state)))
            using (Pen outline =
                new Pen(
                    state,
                    section.IsCommandSection
                        ? 2.3f
                        : 1.5f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);
            }

            if (section.IsCommandSection &&
                !section.IsRadialSection)
            {
                Point[] nose =
                    new[]
                    {
                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2,
                            bounds.Top -
                            38),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 -
                            36,
                            bounds.Top),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 +
                            36,
                            bounds.Top)
                    };

                using (Pen nosePen =
                    new Pen(
                        state,
                        2.0f))
                {
                    graphics.DrawPolygon(
                        nosePen,
                        nose);
                }
            }

            string title =
                GetCompactSectionName(
                    section);

            TextRenderer.DrawText(
                graphics,
                title,
                context.LargeFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 8,
                    bounds.Width - 20,
                    28),
                state,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            string stage =
                "SEP " +
                FormatStage(
                    section.SeparationStage) +
                "   ACT " +
                FormatStage(
                    section.ActivationStage);

            TextRenderer.DrawText(
                graphics,
                stage,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 37,
                    bounds.Width - 16,
                    19),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            string charge =
                section.ElectricChargeCapacity >
                    0.0001
                    ? section.ElectricChargeAmount
                        .ToString("0.0") +
                      " EC   " +
                      percent.ToString("0") +
                      "%"
                    : "NO LOCAL STORAGE";

            TextRenderer.DrawText(
                graphics,
                charge,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 58,
                    bounds.Width - 16,
                    22),
                state,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            Rectangle bar =
                new Rectangle(
                    bounds.Left + 18,
                    bounds.Top + 84,
                    bounds.Width - 36,
                    10);

            DrawChargeBar(
                graphics,
                bar,
                percent,
                section.ElectricChargeCapacity,
                state);

            string hardware =
                "B" +
                section.BatteryPartCount.ToString("00") +
                "   S" +
                section.SolarPartCount.ToString("00") +
                "   G" +
                section.GeneratorPartCount.ToString("00") +
                "   P" +
                section.PartCount.ToString("00");

            TextRenderer.DrawText(
                graphics,
                hardware,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Bottom - 27,
                    bounds.Width - 16,
                    18),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            DrawBatteryMarkers(
                graphics,
                bounds,
                section,
                state);

            DrawSolarMarkers(
                graphics,
                bounds,
                section);
        }

        private static void DrawPrimarySectionDetail(
            Graphics graphics,
            Rectangle panel,
            ElectricalSectionModel primary,
            ElectricalTopologyModel model,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 18,
                    panel.Top + 50,
                    panel.Width - 36,
                    panel.Height - 68);

            if (primary == null)
            {
                DrawNoDataDetails(
                    graphics,
                    panel,
                    context);

                return;
            }

            Color state =
                ResolvePowerColor(
                    primary.ElectricChargePercent,
                    primary.ElectricChargeCapacity);

            TextRenderer.DrawText(
                graphics,
                GetCompactSectionName(
                    primary),
                context.LargeFont,
                new Rectangle(
                    content.Left,
                    content.Top,
                    content.Width,
                    34),
                state,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                graphics,
                primary.IsCommandSection
                    ? "PRIMARY COMMAND / POWER SECTION"
                    : primary.IsRadialSection
                        ? "RADIAL POWER SECTION"
                        : "CORE POWER SECTION",
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    content.Top + 36,
                    content.Width,
                    22),
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            Rectangle chargeCard =
                new Rectangle(
                    content.Left,
                    content.Top + 72,
                    content.Width,
                    138);

            DrawDetailCard(
                graphics,
                chargeCard,
                state);

            DrawLabelValue(
                graphics,
                chargeCard,
                "ELECTRIC CHARGE",
                primary.ElectricChargeAmount
                    .ToString("0.0") +
                " / " +
                primary.ElectricChargeCapacity
                    .ToString("0.0") +
                " EC",
                12,
                context,
                state);

            DrawLabelValue(
                graphics,
                chargeCard,
                "RESERVE",
                primary.ElectricChargePercent
                    .ToString("0") +
                "%",
                62,
                context,
                state);

            DrawChargeBar(
                graphics,
                new Rectangle(
                    chargeCard.Left + 16,
                    chargeCard.Bottom - 26,
                    chargeCard.Width - 32,
                    12),
                primary.ElectricChargePercent,
                primary.ElectricChargeCapacity,
                state);

            Rectangle hardwareCard =
                new Rectangle(
                    content.Left,
                    chargeCard.Bottom + 14,
                    content.Width,
                    196);

            DrawDetailCard(
                graphics,
                hardwareCard,
                context.PhosphorColor);

            int columnWidth =
                hardwareCard.Width /
                2;

            DrawDetailMetric(
                graphics,
                new Rectangle(
                    hardwareCard.Left,
                    hardwareCard.Top,
                    columnWidth,
                    82),
                "BATTERIES",
                primary.BatteryPartCount
                    .ToString("00"),
                Color.FromArgb(
                    255,
                    190,
                    55),
                context);

            DrawDetailMetric(
                graphics,
                new Rectangle(
                    hardwareCard.Left +
                    columnWidth,
                    hardwareCard.Top,
                    hardwareCard.Width -
                    columnWidth,
                    82),
                "SOLAR ARRAYS",
                primary.SolarPartCount
                    .ToString("00"),
                Color.FromArgb(
                    80,
                    205,
                    255),
                context);

            DrawDetailMetric(
                graphics,
                new Rectangle(
                    hardwareCard.Left,
                    hardwareCard.Top + 88,
                    columnWidth,
                    82),
                "GENERATORS",
                primary.GeneratorPartCount
                    .ToString("00"),
                context.PhosphorColor,
                context);

            DrawDetailMetric(
                graphics,
                new Rectangle(
                    hardwareCard.Left +
                    columnWidth,
                    hardwareCard.Top + 88,
                    hardwareCard.Width -
                    columnWidth,
                    82),
                "SECTION PARTS",
                primary.PartCount
                    .ToString("00"),
                context.DimPhosphorColor,
                context);

            int directoryTop =
                hardwareCard.Bottom +
                22;

            TextRenderer.DrawText(
                graphics,
                "SECTION DIRECTORY",
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    directoryTop,
                    content.Width,
                    22),
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int y =
                directoryTop +
                32;

            int rowHeight =
                34;

            int maxRows =
                Math.Max(
                    1,
                    (content.Bottom -
                     y) /
                    rowHeight);

            for (int index = 0;
                 index < model.Sections.Count &&
                 index < maxRows;
                 index++)
            {
                ElectricalSectionModel section =
                    model.Sections[index];

                Color rowColor =
                    ResolvePowerColor(
                        section.ElectricChargePercent,
                        section.ElectricChargeCapacity);

                string rowText =
                    GetCompactSectionName(
                        section)
                    .PadRight(18) +
                    " EC " +
                    (section.ElectricChargeCapacity >
                        0.0001
                        ? section.ElectricChargePercent
                            .ToString("0") +
                          "%"
                        : "--");

                TextRenderer.DrawText(
                    graphics,
                    rowText,
                    context.SmallFont,
                    new Rectangle(
                        content.Left,
                        y,
                        content.Width,
                        rowHeight - 4),
                    rowColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                using (Pen divider =
                    new Pen(
                        Color.FromArgb(
                            55,
                            90,
                            100),
                        1.0f))
                {
                    graphics.DrawLine(
                        divider,
                        content.Left,
                        y +
                        rowHeight -
                        3,
                        content.Right,
                        y +
                        rowHeight -
                        3);
                }

                y +=
                    rowHeight;
            }

            if (model.Sections.Count >
                maxRows)
            {
                TextRenderer.DrawText(
                    graphics,
                    "+" +
                    (model.Sections.Count -
                     maxRows) +
                    " MORE — CTRL+SHIFT+F11",
                    context.SmallFont,
                    new Rectangle(
                        content.Left,
                        content.Bottom - 28,
                        content.Width,
                        24),
                    Color.FromArgb(
                        255,
                        190,
                        55),
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding);
            }
        }

        private static ElectricalSectionModel
            SelectPrimarySection(
                ElectricalTopologyModel model)
        {
            if (model == null)
            {
                return null;
            }

            ElectricalSectionModel command =
                model.Sections
                    .Where(
                        section =>
                            section.IsCommandSection)
                    .OrderByDescending(
                        section =>
                            section.ElectricChargeCapacity)
                    .FirstOrDefault();

            if (command != null)
            {
                return command;
            }

            return
                model.Sections
                    .OrderByDescending(
                        section =>
                            section.ElectricChargeCapacity)
                    .ThenByDescending(
                        section =>
                            section.AverageY)
                    .FirstOrDefault();
        }

        private static string GetCompactSectionName(
            ElectricalSectionModel section)
        {
            if (section == null)
            {
                return "--";
            }

            if (section.IsCommandSection)
            {
                return "COMMAND";
            }

            if (section.IsRadialSection)
            {
                return section.Name
                    .Replace(
                        "RADIAL GROUP",
                        "RADIAL");
            }

            return section.Name
                .Replace(
                    "STACK SECTION",
                    "STAGE");
        }

        private static void DrawDetailCard(
            Graphics graphics,
            Rectangle bounds,
            Color outline)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        12,
                        outline)))
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        105,
                        outline),
                    1.2f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);
            }
        }

        private static void DrawLabelValue(
            Graphics graphics,
            Rectangle card,
            string label,
            string value,
            int offsetY,
            MissionRenderContext context,
            Color valueColor)
        {
            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    card.Left + 16,
                    card.Top + offsetY,
                    card.Width - 32,
                    20),
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                value,
                context.LargeFont,
                new Rectangle(
                    card.Left + 16,
                    card.Top + offsetY + 20,
                    card.Width - 32,
                    30),
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawDetailMetric(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            Color color,
            MissionRenderContext context)
        {
            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 7,
                    bounds.Width - 20,
                    22),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                value,
                context.LargeFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 31,
                    bounds.Width - 20,
                    38),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawChargeBar(
            Graphics graphics,
            Rectangle bounds,
            double percent,
            double capacity,
            Color state)
        {
            using (Pen outline =
                new Pen(
                    Color.FromArgb(
                        90,
                        130,
                        140),
                    1.0f))
            {
                graphics.DrawRectangle(
                    outline,
                    bounds);
            }

            if (capacity <=
                0.0001)
            {
                return;
            }

            int fillWidth =
                (int)Math.Round(
                    Math.Max(
                        0.0,
                        Math.Min(
                            100.0,
                            percent)) /
                    100.0 *
                    Math.Max(
                        0,
                        bounds.Width - 4));

            if (fillWidth <= 0)
            {
                return;
            }

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        state)))
            {
                graphics.FillRectangle(
                    fill,
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Top + 2,
                        fillWidth,
                        Math.Max(
                            1,
                            bounds.Height - 3)));
            }
        }

        private static void DrawBatteryMarkers(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section,
            Color state)
        {
            int count =
                Math.Min(
                    3,
                    section.BatteryPartCount);

            for (int index = 0;
                 index < count;
                 index++)
            {
                int markerY =
                    bounds.Top +
                    18 +
                    index *
                    20;

                Rectangle battery =
                    new Rectangle(
                        bounds.Left - 22,
                        markerY,
                        14,
                        10);

                using (Pen pen =
                    new Pen(
                        state,
                        1.2f))
                {
                    graphics.DrawRectangle(
                        pen,
                        battery);

                    graphics.DrawLine(
                        pen,
                        battery.Right,
                        battery.Top + 3,
                        battery.Right + 3,
                        battery.Top + 3);

                    graphics.DrawLine(
                        pen,
                        battery.Right,
                        battery.Bottom - 3,
                        battery.Right + 3,
                        battery.Bottom - 3);
                }
            }
        }

        private static void DrawSolarMarkers(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section)
        {
            int count =
                Math.Min(
                    3,
                    section.SolarPartCount);

            Color solar =
                Color.FromArgb(
                    75,
                    195,
                    255);

            for (int index = 0;
                 index < count;
                 index++)
            {
                int markerY =
                    bounds.Top +
                    18 +
                    index *
                    20;

                Rectangle panel =
                    new Rectangle(
                        bounds.Right + 8,
                        markerY,
                        20,
                        11);

                using (Pen pen =
                    new Pen(
                        solar,
                        1.2f))
                {
                    graphics.DrawRectangle(
                        pen,
                        panel);

                    graphics.DrawLine(
                        pen,
                        panel.Left +
                        panel.Width /
                        2,
                        panel.Top,
                        panel.Left +
                        panel.Width /
                        2,
                        panel.Bottom);

                    graphics.DrawLine(
                        pen,
                        panel.Left,
                        panel.Top +
                        panel.Height /
                        2,
                        panel.Right,
                        panel.Top +
                        panel.Height /
                        2);
                }
            }
        }

        private static void DrawWaitingState(
            Graphics graphics,
            Rectangle panel,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 20,
                    panel.Top + 52,
                    panel.Width - 40,
                    panel.Height - 72);

            DrawCenteredText(
                graphics,
                "WAITING FOR VESSEL TOPOLOGY",
                new Rectangle(
                    content.Left,
                    content.Top +
                    content.Height /
                    2 -
                    24,
                    content.Width,
                    24),
                context.LargeFont,
                context.PhosphorColor);

            DrawCenteredText(
                graphics,
                "LOAD OR LAUNCH A VESSEL",
                new Rectangle(
                    content.Left,
                    content.Top +
                    content.Height /
                    2 +
                    8,
                    content.Width,
                    22),
                context.SmallFont,
                context.DimPhosphorColor);
        }

        private static void DrawNoDataDetails(
            Graphics graphics,
            Rectangle panel,
            MissionRenderContext context)
        {
            DrawCenteredText(
                graphics,
                "NO SECTION DATA",
                new Rectangle(
                    panel.Left + 12,
                    panel.Top + 60,
                    panel.Width - 24,
                    30),
                context.SmallFont,
                context.DimPhosphorColor);
        }

        private static void DrawPanel(
            Graphics graphics,
            Rectangle bounds,
            MissionRenderContext context,
            string title)
        {
            using (Pen outline =
                new Pen(
                    Color.FromArgb(
                        95,
                        145,
                        155),
                    1.2f))
            {
                graphics.DrawRectangle(
                    outline,
                    bounds);

                graphics.DrawLine(
                    outline,
                    bounds.Left + 10,
                    bounds.Top + 34,
                    bounds.Right - 10,
                    bounds.Top + 34);
            }

            TextRenderer.DrawText(
                graphics,
                title,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 7,
                    bounds.Width - 20,
                    22),
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawSummaryCell(
            Graphics graphics,
            Rectangle bounds,
            string title,
            string value,
            string detail,
            Color color,
            MissionRenderContext context)
        {
            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        75,
                        110,
                        120),
                    1.0f))
            {
                graphics.DrawLine(
                    divider,
                    bounds.Right,
                    bounds.Top + 6,
                    bounds.Right,
                    bounds.Bottom - 6);
            }

            DrawCenteredText(
                graphics,
                title,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 1,
                    bounds.Width - 8,
                    18),
                context.SmallFont,
                context.DimPhosphorColor);

            DrawCenteredText(
                graphics,
                value,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 23,
                    bounds.Width - 8,
                    34),
                context.LargeFont,
                color);

            DrawCenteredText(
                graphics,
                detail,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Bottom - 22,
                    bounds.Width - 8,
                    18),
                context.SmallFont,
                color);
        }

        private static Color ResolvePowerColor(
            double percent,
            double capacity)
        {
            if (capacity <=
                0.0001)
            {
                return Color.FromArgb(
                    105,
                    140,
                    150);
            }

            if (percent <=
                5.0)
            {
                return Color.FromArgb(
                    255,
                    75,
                    55);
            }

            if (percent <=
                15.0)
            {
                return Color.FromArgb(
                    255,
                    190,
                    55);
            }

            return Color.FromArgb(
                75,
                235,
                105);
        }

        private static string FormatStage(
            int stage)
        {
            return
                stage >= 0
                    ? stage.ToString("00")
                    : "--";
        }

        private static void DrawCenteredText(
            Graphics graphics,
            string text,
            Rectangle bounds,
            Font font,
            Color color)
        {
            TextRenderer.DrawText(
                graphics,
                text,
                font,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }
    }
}
