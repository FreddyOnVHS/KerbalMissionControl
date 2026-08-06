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

            Rectangle summaryBounds =
                new Rectangle(
                    working.Left,
                    working.Top,
                    working.Width,
                    160);

            Rectangle stackBounds =
                new Rectangle(
                    working.Left,
                    summaryBounds.Bottom + 12,
                    (int)Math.Round(
                        working.Width * 0.64),
                    working.Bottom -
                    summaryBounds.Bottom -
                    12);

            Rectangle detailBounds =
                new Rectangle(
                    stackBounds.Right + 12,
                    stackBounds.Top,
                    working.Right -
                    stackBounds.Right -
                    12,
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
                "SECTION STATUS");

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

                DrawSummary(
                    graphics,
                    summaryBounds,
                    context,
                    model);

                return;
            }

            DrawSummary(
                graphics,
                summaryBounds,
                context,
                model);

            List<SectionPlacement> placements =
                CalculatePlacements(
                    stackBounds,
                    model);

            DrawStackConnections(
                graphics,
                placements,
                context);

            DrawStackSections(
                graphics,
                placements,
                context);

            DrawSectionDetails(
                graphics,
                detailBounds,
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
                    bounds.Left + 18,
                    bounds.Top + 42,
                    bounds.Width - 36,
                    bounds.Height - 52);

            int totalSections =
                model != null
                    ? model.Sections.Count
                    : 0;

            int poweredSections =
                model != null
                    ? model.Sections.Count(
                        section =>
                            section.ElectricChargeCapacity >
                                0.0001 &&
                            section.ElectricChargeAmount >
                                0.0001)
                    : 0;

            int batteries =
                model != null
                    ? model.Sections.Sum(
                        section =>
                            section.BatteryPartCount)
                    : 0;

            int solar =
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

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left,
                    content.Top,
                    content.Width / 5,
                    content.Height),
                "TOTAL CHARGE",
                amount.ToString("0.0") +
                " / " +
                capacity.ToString("0.0"),
                percent.ToString("0") + "%",
                ResolvePowerColor(
                    percent,
                    capacity),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    content.Width / 5,
                    content.Top,
                    content.Width / 5,
                    content.Height),
                "STACK SECTIONS",
                totalSections.ToString("00"),
                poweredSections.ToString("00") +
                " POWERED",
                context.PhosphorColor,
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    content.Width * 2 / 5,
                    content.Top,
                    content.Width / 5,
                    content.Height),
                "BATTERY PARTS",
                batteries.ToString("00"),
                "LOCATED",
                Color.FromArgb(
                    255,
                    190,
                    55),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    content.Width * 3 / 5,
                    content.Top,
                    content.Width / 5,
                    content.Height),
                "SOLAR PARTS",
                solar.ToString("00"),
                "LOCATED",
                Color.FromArgb(
                    80,
                    205,
                    255),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    content.Width * 4 / 5,
                    content.Top,
                    content.Width -
                    content.Width * 4 / 5,
                    content.Height),
                "TOPOLOGY REV",
                model != null
                    ? model.TopologyRevision.ToString()
                    : "--",
                model != null &&
                !string.IsNullOrEmpty(
                    model.VesselName)
                    ? model.VesselName
                    : "WAITING",
                context.DimPhosphorColor,
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
                    panel.Top + 52,
                    panel.Width - 68,
                    panel.Height - 74);

            int coreCount =
                Math.Max(
                    1,
                    core.Count);

            int gap =
                12;

            int availableHeight =
                content.Height -
                gap *
                Math.Max(
                    0,
                    coreCount - 1);

            int sectionHeight =
                Math.Max(
                    74,
                    Math.Min(
                        150,
                        availableHeight /
                        coreCount));

            int coreWidth =
                Math.Max(
                    260,
                    Math.Min(
                        420,
                        content.Width /
                        3));

            int centerX =
                content.Left +
                content.Width /
                2;

            int totalCoreHeight =
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
                     totalCoreHeight) /
                    2);

            List<SectionPlacement> placements =
                new List<SectionPlacement>();

            for (int index = 0;
                 index < core.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    core[index];

                Rectangle bounds =
                    new Rectangle(
                        centerX -
                        coreWidth /
                        2,
                        startY +
                        index *
                        (sectionHeight + gap),
                        coreWidth,
                        sectionHeight);

                placements.Add(
                    new SectionPlacement
                    {
                        Section =
                            section,

                        Bounds =
                            bounds
                    });
            }

            for (int index = 0;
                 index < radial.Count;
                 index++)
            {
                ElectricalSectionModel section =
                    radial[index];

                int nearestCore =
                    FindNearestCoreIndex(
                        core,
                        section);

                Rectangle coreBounds =
                    placements.Count > 0
                        ? placements[
                            Math.Max(
                                0,
                                Math.Min(
                                    placements.Count - 1,
                                    nearestCore))]
                            .Bounds
                        : new Rectangle(
                            centerX -
                            coreWidth /
                            2,
                            content.Top +
                            content.Height /
                            2 -
                            sectionHeight /
                            2,
                            coreWidth,
                            sectionHeight);

                bool left =
                    index %
                    2 ==
                    0;

                int radialWidth =
                    Math.Max(
                        180,
                        coreWidth -
                        90);

                Rectangle bounds =
                    new Rectangle(
                        left
                            ? coreBounds.Left -
                              radialWidth -
                              34
                            : coreBounds.Right +
                              34,
                        coreBounds.Top +
                        Math.Max(
                            0,
                            (coreBounds.Height -
                             sectionHeight) /
                            2),
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
            List<SectionPlacement> placements,
            MissionRenderContext context)
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
                        105,
                        165,
                        175),
                    2.0f))
            using (Pen radialPen =
                new Pen(
                    Color.FromArgb(
                        105,
                        145,
                        155),
                    1.5f))
            {
                for (int index = 0;
                     index < core.Count - 1;
                     index++)
                {
                    Point start =
                        new Point(
                            core[index]
                                .Bounds.Left +
                            core[index]
                                .Bounds.Width /
                            2,
                            core[index]
                                .Bounds.Bottom);

                    Point end =
                        new Point(
                            core[index + 1]
                                .Bounds.Left +
                            core[index + 1]
                                .Bounds.Width /
                            2,
                            core[index + 1]
                                .Bounds.Top);

                    graphics.DrawLine(
                        corePen,
                        start,
                        end);
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

                    Point start =
                        new Point(
                            radial.IsLeft
                                ? radial.Bounds.Right
                                : radial.Bounds.Left,
                            radial.Bounds.Top +
                            radial.Bounds.Height /
                            2);

                    Point end =
                        new Point(
                            radial.IsLeft
                                ? nearest.Bounds.Left
                                : nearest.Bounds.Right,
                            nearest.Bounds.Top +
                            nearest.Bounds.Height /
                            2);

                    graphics.DrawLine(
                        radialPen,
                        start,
                        end);
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
                SectionPlacement placement =
                    placements[index];

                DrawSection(
                    graphics,
                    placement.Bounds,
                    placement.Section,
                    context);
            }
        }

        private static void DrawSection(
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
                        18,
                        state)))
            using (Pen outline =
                new Pen(
                    state,
                    section.IsCommandSection
                        ? 2.4f
                        : 1.6f))
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
                            42),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 -
                            38,
                            bounds.Top),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 +
                            38,
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

            Rectangle title =
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 8,
                    bounds.Width - 16,
                    24);

            TextRenderer.DrawText(
                graphics,
                section.Name,
                context.SmallFont,
                title,
                state,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            string stageText =
                "SEP " +
                FormatStage(
                    section.SeparationStage) +
                "  ACT " +
                FormatStage(
                    section.ActivationStage);

            DrawCenteredText(
                graphics,
                stageText,
                new Rectangle(
                    bounds.Left + 6,
                    bounds.Top + 34,
                    bounds.Width - 12,
                    18),
                context.SmallFont,
                context.DimPhosphorColor);

            string chargeText =
                section.ElectricChargeCapacity >
                    0.0001
                    ? "EC " +
                      section.ElectricChargeAmount
                          .ToString("0.0") +
                      " / " +
                      section.ElectricChargeCapacity
                          .ToString("0.0") +
                      "  " +
                      percent.ToString("0") +
                      "%"
                    : "NO LOCAL STORAGE";

            DrawCenteredText(
                graphics,
                chargeText,
                new Rectangle(
                    bounds.Left + 6,
                    bounds.Top + 55,
                    bounds.Width - 12,
                    22),
                context.SmallFont,
                state);

            string hardware =
                "BAT " +
                section.BatteryPartCount.ToString("00") +
                "  SOL " +
                section.SolarPartCount.ToString("00") +
                "  GEN " +
                section.GeneratorPartCount.ToString("00");

            DrawCenteredText(
                graphics,
                hardware,
                new Rectangle(
                    bounds.Left + 6,
                    bounds.Bottom - 28,
                    bounds.Width - 12,
                    18),
                context.SmallFont,
                context.DimPhosphorColor);

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

        private static void DrawBatteryMarkers(
            Graphics graphics,
            Rectangle bounds,
            ElectricalSectionModel section,
            Color state)
        {
            int count =
                Math.Min(
                    4,
                    section.BatteryPartCount);

            for (int index = 0;
                 index < count;
                 index++)
            {
                int markerY =
                    bounds.Top +
                    18 +
                    index *
                    19;

                Rectangle battery =
                    new Rectangle(
                        bounds.Left - 22,
                        markerY,
                        15,
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
                    4,
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
                    19;

                Rectangle panel =
                    new Rectangle(
                        bounds.Right + 7,
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

        private static void DrawSectionDetails(
            Graphics graphics,
            Rectangle panel,
            ElectricalTopologyModel model,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 16,
                    panel.Top + 46,
                    panel.Width - 32,
                    panel.Height - 60);

            int rowHeight =
                Math.Max(
                    58,
                    Math.Min(
                        86,
                        content.Height /
                        Math.Max(
                            1,
                            model.Sections.Count)));

            int y =
                content.Top;

            for (int index = 0;
                 index < model.Sections.Count &&
                 y + rowHeight <=
                    content.Bottom;
                 index++)
            {
                ElectricalSectionModel section =
                    model.Sections[index];

                Color state =
                    ResolvePowerColor(
                        section.ElectricChargePercent,
                        section.ElectricChargeCapacity);

                Rectangle row =
                    new Rectangle(
                        content.Left,
                        y,
                        content.Width,
                        rowHeight - 6);

                using (Pen pen =
                    new Pen(
                        Color.FromArgb(
                            90,
                            125,
                            135),
                        1.0f))
                {
                    graphics.DrawRectangle(
                        pen,
                        row);
                }

                TextRenderer.DrawText(
                    graphics,
                    section.Name,
                    context.SmallFont,
                    new Rectangle(
                        row.Left + 7,
                        row.Top + 5,
                        row.Width - 14,
                        18),
                    state,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                string line =
                    "EC " +
                    section.ElectricChargePercent
                        .ToString("0") +
                    "%  PRT " +
                    section.PartCount.ToString("00") +
                    "  ELEC " +
                    section.ElectricalPartCount
                        .ToString("00");

                TextRenderer.DrawText(
                    graphics,
                    line,
                    context.SmallFont,
                    new Rectangle(
                        row.Left + 7,
                        row.Top + 26,
                        row.Width - 14,
                        18),
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                string hardware =
                    "BAT " +
                    section.BatteryPartCount.ToString("00") +
                    " SOL " +
                    section.SolarPartCount.ToString("00") +
                    " GEN " +
                    section.GeneratorPartCount.ToString("00") +
                    " CMD " +
                    section.CommandPartCount.ToString("00");

                TextRenderer.DrawText(
                    graphics,
                    hardware,
                    context.SmallFont,
                    new Rectangle(
                        row.Left + 7,
                        row.Top + 45,
                        row.Width - 14,
                        18),
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                y +=
                    rowHeight;
            }

            if (model.Sections.Count *
                rowHeight >
                content.Height)
            {
                DrawCenteredText(
                    graphics,
                    "ADDITIONAL SECTIONS IN DEBUGGER",
                    new Rectangle(
                        content.Left,
                        content.Bottom - 22,
                        content.Width,
                        18),
                    context.SmallFont,
                    Color.FromArgb(
                        255,
                        190,
                        55));
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
                "LOAD OR LAUNCH A VESSEL — TOPOLOGY MAP WILL BUILD AUTOMATICALLY",
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
                    bounds.Top + 2,
                    bounds.Width - 8,
                    18),
                context.SmallFont,
                context.DimPhosphorColor);

            DrawCenteredText(
                graphics,
                value,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 24,
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
                    110,
                    145,
                    155);
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
