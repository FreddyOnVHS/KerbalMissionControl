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
        private enum GroupKind
        {
            Command,
            Core,
            Radial
        }

        private sealed class PowerGroup
        {
            public PowerGroup()
            {
                Name =
                    string.Empty;

                Sections =
                    new List<
                        ElectricalSectionModel>();
            }

            public string Name;
            public GroupKind Kind;
            public int SeparationStage;
            public int ActivationStage;
            public double AverageY;
            public int BatteryCount;
            public int SolarCount;
            public int GeneratorCount;
            public int PartCount;
            public double ChargeAmount;
            public double ChargeCapacity;

            public List<
                ElectricalSectionModel> Sections;

            public double ChargePercent
            {
                get
                {
                    if (ChargeCapacity <=
                        0.0001)
                    {
                        return 0.0;
                    }

                    return
                        ChargeAmount /
                        ChargeCapacity *
                        100.0;
                }
            }

            public bool HasLocalStorage
            {
                get
                {
                    return
                        ChargeCapacity >
                        0.0001;
                }
            }
        }

        private sealed class StackRow
        {
            public PowerGroup Core;
            public List<PowerGroup> RadialBanks;
            public Rectangle CoreBounds;
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

            MissionPageLayout layout =
                new MissionPageLayout(
                    context);

            layout.DrawHeader(
                "ELECTRICAL POWER",
                "CH 05");

            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 82);

            Rectangle summaryBounds =
                new Rectangle(
                    working.Left,
                    working.Top,
                    working.Width,
                    94);

            int detailWidth =
                Math.Max(
                    390,
                    working.Width /
                    3);

            Rectangle mapBounds =
                new Rectangle(
                    working.Left,
                    summaryBounds.Bottom + 10,
                    working.Width -
                    detailWidth -
                    10,
                    working.Bottom -
                    summaryBounds.Bottom -
                    10);

            Rectangle detailBounds =
                new Rectangle(
                    mapBounds.Right + 10,
                    mapBounds.Top,
                    detailWidth,
                    mapBounds.Height);

            DrawPanel(
                graphics,
                summaryBounds,
                "VESSEL ELECTRICAL STATUS",
                context);

            DrawPanel(
                graphics,
                mapBounds,
                "STAGE POWER MAP",
                context);

            DrawPanel(
                graphics,
                detailBounds,
                "COMMAND POWER DETAIL",
                context);

            List<PowerGroup> groups =
                BuildGroups(
                    model);

            DrawSummary(
                graphics,
                summaryBounds,
                groups,
                model,
                context);

            if (groups.Count == 0)
            {
                DrawWaiting(
                    graphics,
                    mapBounds,
                    context);

                DrawWaiting(
                    graphics,
                    detailBounds,
                    context);

                return;
            }

            List<StackRow> rows =
                BuildRows(
                    groups);

            DrawStack(
                graphics,
                mapBounds,
                rows,
                groups,
                context);

            PowerGroup selected =
                groups.FirstOrDefault(
                    group =>
                        group.Kind ==
                        GroupKind.Command) ??
                groups.First();

            DrawDetail(
                graphics,
                detailBounds,
                selected,
                groups,
                context);
        }

        private static List<PowerGroup>
            BuildGroups(
                ElectricalTopologyModel model)
        {
            List<PowerGroup> groups =
                new List<PowerGroup>();

            if (model == null)
            {
                return groups;
            }

            List<ElectricalSectionModel> command =
                model.Sections
                    .Where(
                        section =>
                            section.IsCommandSection)
                    .ToList();

            if (command.Count > 0)
            {
                groups.Add(
                    CreateGroup(
                        "COMMAND",
                        GroupKind.Command,
                        command));
            }

            foreach (
                IGrouping<
                    int,
                    ElectricalSectionModel> stage
                in model.Sections
                    .Where(
                        section =>
                            !section.IsCommandSection &&
                            !section.IsRadialSection)
                    .GroupBy(
                        section =>
                            section.SeparationStage))
            {
                groups.Add(
                    CreateGroup(
                        "STAGE " +
                        FormatStage(
                            stage.Key),
                        GroupKind.Core,
                        stage.ToList()));
            }

            foreach (
                IGrouping<
                    int,
                    ElectricalSectionModel> bank
                in model.Sections
                    .Where(
                        section =>
                            !section.IsCommandSection &&
                            section.IsRadialSection)
                    .GroupBy(
                        section =>
                            section.SeparationStage))
            {
                groups.Add(
                    CreateGroup(
                        "RADIAL ×" +
                        bank.Count(),
                        GroupKind.Radial,
                        bank.ToList()));
            }

            return groups
                .OrderByDescending(
                    group =>
                        group.Kind ==
                        GroupKind.Command)
                .ThenByDescending(
                    group =>
                        group.AverageY)
                .ThenByDescending(
                    group =>
                        group.SeparationStage)
                .ToList();
        }

        private static PowerGroup CreateGroup(
            string name,
            GroupKind kind,
            List<ElectricalSectionModel> sections)
        {
            PowerGroup group =
                new PowerGroup();

            group.Name =
                name;

            group.Kind =
                kind;

            group.Sections.AddRange(
                sections);

            group.SeparationStage =
                MostCommonStage(
                    sections.Select(
                        section =>
                            section.SeparationStage));

            group.ActivationStage =
                MostCommonStage(
                    sections.Select(
                        section =>
                            section.ActivationStage));

            group.AverageY =
                sections.Count > 0
                    ? sections.Average(
                        section =>
                            section.AverageY)
                    : 0.0;

            group.BatteryCount =
                sections.Sum(
                    section =>
                        section.BatteryPartCount);

            group.SolarCount =
                sections.Sum(
                    section =>
                        section.SolarPartCount);

            group.GeneratorCount =
                sections.Sum(
                    section =>
                        section.GeneratorPartCount);

            group.PartCount =
                sections.Sum(
                    section =>
                        section.PartCount);

            group.ChargeAmount =
                sections.Sum(
                    section =>
                        section.ElectricChargeAmount);

            group.ChargeCapacity =
                sections.Sum(
                    section =>
                        section.ElectricChargeCapacity);

            return group;
        }

        private static int MostCommonStage(
            IEnumerable<int> values)
        {
            List<int> valid =
                values
                    .Where(
                        value =>
                            value >= 0)
                    .ToList();

            if (valid.Count == 0)
            {
                return -1;
            }

            return valid
                .GroupBy(
                    value =>
                        value)
                .OrderByDescending(
                    group =>
                        group.Count())
                .ThenByDescending(
                    group =>
                        group.Key)
                .First()
                .Key;
        }

        private static List<StackRow> BuildRows(
            List<PowerGroup> groups)
        {
            List<PowerGroup> vertical =
                groups
                    .Where(
                        group =>
                            group.Kind !=
                            GroupKind.Radial)
                    .OrderByDescending(
                        group =>
                            group.AverageY)
                    .ToList();

            List<PowerGroup> radial =
                groups
                    .Where(
                        group =>
                            group.Kind ==
                            GroupKind.Radial)
                    .OrderByDescending(
                        group =>
                            group.AverageY)
                    .ToList();

            List<StackRow> rows =
                new List<StackRow>();

            foreach (
                PowerGroup core
                in vertical)
            {
                rows.Add(
                    new StackRow
                    {
                        Core =
                            core,

                        RadialBanks =
                            new List<
                                PowerGroup>()
                    });
            }

            foreach (
                PowerGroup bank
                in radial)
            {
                StackRow nearest =
                    rows
                        .OrderBy(
                            row =>
                                Math.Abs(
                                    row.Core.AverageY -
                                    bank.AverageY))
                        .FirstOrDefault();

                if (nearest != null)
                {
                    nearest.RadialBanks.Add(
                        bank);
                }
            }

            return rows;
        }

        private static void DrawStack(
            Graphics graphics,
            Rectangle panel,
            List<StackRow> rows,
            List<PowerGroup> groups,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 30,
                    panel.Top + 44,
                    panel.Width - 60,
                    panel.Height - 62);

            int rowCount =
                Math.Max(
                    1,
                    rows.Count);

            int gap =
                12;

            int availableHeight =
                content.Height -
                gap *
                Math.Max(
                    0,
                    rowCount - 1);

            int rowHeight =
                Math.Max(
                    80,
                    Math.Min(
                        112,
                        availableHeight /
                        rowCount));

            int coreWidth =
                Math.Max(
                    260,
                    Math.Min(
                        360,
                        content.Width /
                        2));

            int centerX =
                content.Left +
                content.Width /
                2;

            int totalHeight =
                rowHeight *
                rowCount +
                gap *
                Math.Max(
                    0,
                    rowCount - 1);

            int startY =
                content.Top +
                Math.Max(
                    0,
                    (content.Height -
                     totalHeight) /
                    2);

            for (int index = 0;
                 index < rows.Count;
                 index++)
            {
                StackRow row =
                    rows[index];

                row.CoreBounds =
                    new Rectangle(
                        centerX -
                        coreWidth /
                        2,
                        startY +
                        index *
                        (rowHeight +
                         gap),
                        coreWidth,
                        rowHeight);
            }

            using (Pen connector =
                new Pen(
                    Color.FromArgb(
                        90,
                        145,
                        155),
                    2.0f))
            {
                for (int index = 0;
                     index < rows.Count - 1;
                     index++)
                {
                    int x =
                        rows[index]
                            .CoreBounds.Left +
                        rows[index]
                            .CoreBounds.Width /
                        2;

                    graphics.DrawLine(
                        connector,
                        x,
                        rows[index]
                            .CoreBounds.Bottom,
                        x,
                        rows[index + 1]
                            .CoreBounds.Top);
                }
            }

            double vesselAmount =
                groups.Sum(
                    group =>
                        group.ChargeAmount);

            double vesselCapacity =
                groups.Sum(
                    group =>
                        group.ChargeCapacity);

            bool vesselPowered =
                vesselAmount >
                0.0001;

            double vesselPercent =
                vesselCapacity >
                0.0001
                    ? vesselAmount /
                      vesselCapacity *
                      100.0
                    : 0.0;

            foreach (
                StackRow row
                in rows)
            {
                DrawCoreCard(
                    graphics,
                    row.CoreBounds,
                    row.Core,
                    vesselPowered,
                    vesselPercent,
                    context);

                DrawRadialBadges(
                    graphics,
                    content,
                    row,
                    vesselPowered,
                    vesselPercent,
                    context);
            }
        }

        private static void DrawCoreCard(
            Graphics graphics,
            Rectangle bounds,
            PowerGroup group,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            string status =
                ResolveStatus(
                    group,
                    vesselPowered,
                    vesselPercent);

            Color color =
                ResolveStatusColor(
                    status);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        11,
                        color)))
            using (Pen outline =
                new Pen(
                    color,
                    group.Kind ==
                    GroupKind.Command
                        ? 2.2f
                        : 1.5f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);
            }

            if (group.Kind ==
                GroupKind.Command)
            {
                Point[] nose =
                    new[]
                    {
                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2,
                            bounds.Top -
                            28),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 -
                            28,
                            bounds.Top),

                        new Point(
                            bounds.Left +
                            bounds.Width /
                            2 +
                            28,
                            bounds.Top)
                    };

                using (Pen nosePen =
                    new Pen(
                        color,
                        1.8f))
                {
                    graphics.DrawPolygon(
                        nosePen,
                        nose);
                }
            }

            TextRenderer.DrawText(
                graphics,
                group.Name,
                context.LargeFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 12,
                    bounds.Width - 16,
                    30),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                graphics,
                status,
                context.LargeFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 48,
                    bounds.Width - 16,
                    30),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            if (group.HasLocalStorage)
            {
                DrawBar(
                    graphics,
                    new Rectangle(
                        bounds.Left + 24,
                        bounds.Bottom - 18,
                        bounds.Width - 48,
                        8),
                    group.ChargePercent,
                    color);
            }

            DrawHardwareSymbols(
                graphics,
                bounds,
                group);
        }

        private static void DrawRadialBadges(
            Graphics graphics,
            Rectangle content,
            StackRow row,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            if (row.RadialBanks == null ||
                row.RadialBanks.Count == 0)
            {
                return;
            }

            int badgeWidth =
                Math.Max(
                    150,
                    Math.Min(
                        210,
                        row.CoreBounds.Width -
                        70));

            int badgeHeight =
                58;

            for (int index = 0;
                 index < row.RadialBanks.Count;
                 index++)
            {
                PowerGroup bank =
                    row.RadialBanks[index];

                bool left =
                    index %
                    2 ==
                    0;

                Rectangle badge =
                    new Rectangle(
                        left
                            ? Math.Max(
                                content.Left,
                                row.CoreBounds.Left -
                                badgeWidth -
                                20)
                            : Math.Min(
                                content.Right -
                                badgeWidth,
                                row.CoreBounds.Right +
                                20),
                        row.CoreBounds.Top +
                        Math.Max(
                            0,
                            (row.CoreBounds.Height -
                             badgeHeight) /
                            2),
                        badgeWidth,
                        badgeHeight);

                string status =
                    ResolveStatus(
                        bank,
                        vesselPowered,
                        vesselPercent);

                Color color =
                    ResolveStatusColor(
                        status);

                using (Pen connector =
                    new Pen(
                        Color.FromArgb(
                            80,
                            125,
                            135),
                        1.2f))
                {
                    graphics.DrawLine(
                        connector,
                        left
                            ? badge.Right
                            : badge.Left,
                        badge.Top +
                        badge.Height /
                        2,
                        left
                            ? row.CoreBounds.Left
                            : row.CoreBounds.Right,
                        row.CoreBounds.Top +
                        row.CoreBounds.Height /
                        2);
                }

                using (SolidBrush fill =
                    new SolidBrush(
                        Color.FromArgb(
                            10,
                            color)))
                using (Pen outline =
                    new Pen(
                        color,
                        1.3f))
                {
                    graphics.FillRectangle(
                        fill,
                        badge);

                    graphics.DrawRectangle(
                        outline,
                        badge);
                }

                TextRenderer.DrawText(
                    graphics,
                    bank.Name,
                    context.SmallFont,
                    new Rectangle(
                        badge.Left + 6,
                        badge.Top + 5,
                        badge.Width - 12,
                        22),
                    color,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                TextRenderer.DrawText(
                    graphics,
                    status,
                    context.SmallFont,
                    new Rectangle(
                        badge.Left + 6,
                        badge.Top + 29,
                        badge.Width - 12,
                        20),
                    color,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);
            }
        }

        private static void DrawHardwareSymbols(
            Graphics graphics,
            Rectangle bounds,
            PowerGroup group)
        {
            if (group.BatteryCount > 0)
            {
                Color battery =
                    Color.FromArgb(
                        255,
                        190,
                        55);

                Rectangle marker =
                    new Rectangle(
                        bounds.Left - 18,
                        bounds.Top +
                        bounds.Height /
                        2 -
                        6,
                        11,
                        10);

                using (Pen pen =
                    new Pen(
                        battery,
                        1.2f))
                {
                    graphics.DrawRectangle(
                        pen,
                        marker);
                }
            }

            if (group.SolarCount > 0)
            {
                Color solar =
                    Color.FromArgb(
                        80,
                        205,
                        255);

                Rectangle marker =
                    new Rectangle(
                        bounds.Right + 7,
                        bounds.Top +
                        bounds.Height /
                        2 -
                        6,
                        17,
                        11);

                using (Pen pen =
                    new Pen(
                        solar,
                        1.2f))
                {
                    graphics.DrawRectangle(
                        pen,
                        marker);

                    graphics.DrawLine(
                        pen,
                        marker.Left +
                        marker.Width /
                        2,
                        marker.Top,
                        marker.Left +
                        marker.Width /
                        2,
                        marker.Bottom);

                    graphics.DrawLine(
                        pen,
                        marker.Left,
                        marker.Top +
                        marker.Height /
                        2,
                        marker.Right,
                        marker.Top +
                        marker.Height /
                        2);
                }
            }
        }

        private static void DrawDetail(
            Graphics graphics,
            Rectangle panel,
            PowerGroup selected,
            List<PowerGroup> groups,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 20,
                    panel.Top + 46,
                    panel.Width - 40,
                    panel.Height - 62);

            double vesselAmount =
                groups.Sum(
                    group =>
                        group.ChargeAmount);

            double vesselCapacity =
                groups.Sum(
                    group =>
                        group.ChargeCapacity);

            bool vesselPowered =
                vesselAmount >
                0.0001;

            double vesselPercent =
                vesselCapacity >
                0.0001
                    ? vesselAmount /
                      vesselCapacity *
                      100.0
                    : 0.0;

            string status =
                ResolveStatus(
                    selected,
                    vesselPowered,
                    vesselPercent);

            Color color =
                ResolveStatusColor(
                    status);

            TextRenderer.DrawText(
                graphics,
                selected.Name,
                context.LargeFont,
                new Rectangle(
                    content.Left,
                    content.Top,
                    content.Width,
                    34),
                color,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            TextRenderer.DrawText(
                graphics,
                status,
                context.LargeFont,
                new Rectangle(
                    content.Left,
                    content.Top + 42,
                    content.Width,
                    32),
                color,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int y =
                content.Top +
                102;

            DrawLargeDetailRow(
                graphics,
                content,
                ref y,
                "POWER SOURCE",
                selected.HasLocalStorage
                    ? "LOCAL STORAGE"
                    : vesselPowered
                        ? "VESSEL BUS"
                        : "NONE",
                context);

            DrawLargeDetailRow(
                graphics,
                content,
                ref y,
                "ELECTRIC CHARGE",
                selected.HasLocalStorage
                    ? selected.ChargeAmount
                        .ToString("0.0") +
                      " / " +
                      selected.ChargeCapacity
                        .ToString("0.0") +
                      " EC"
                    : "SHARED",
                context);

            DrawLargeDetailRow(
                graphics,
                content,
                ref y,
                "BATTERIES",
                selected.BatteryCount
                    .ToString("00"),
                context);

            DrawLargeDetailRow(
                graphics,
                content,
                ref y,
                "SOLAR PANELS",
                selected.SolarCount
                    .ToString("00"),
                context);

            y +=
                18;

            DrawFooterValue(
                graphics,
                content,
                y,
                "SEP",
                FormatStage(
                    selected.SeparationStage),
                context);

            DrawFooterValue(
                graphics,
                content,
                y + 42,
                "ACT",
                FormatStage(
                    selected.ActivationStage),
                context);

            DrawFooterValue(
                graphics,
                content,
                y + 84,
                "PARTS",
                selected.PartCount
                    .ToString("00"),
                context);

            DrawFooterValue(
                graphics,
                content,
                y + 126,
                "GENERATORS",
                selected.GeneratorCount
                    .ToString("00"),
                context);
        }

        private static void DrawLargeDetailRow(
            Graphics graphics,
            Rectangle content,
            ref int y,
            string label,
            string value,
            MissionRenderContext context)
        {
            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    y,
                    content.Width,
                    22),
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                value,
                context.LargeFont,
                new Rectangle(
                    content.Left,
                    y + 23,
                    content.Width,
                    34),
                context.PhosphorColor,
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
                    y + 61,
                    content.Right,
                    y + 61);
            }

            y +=
                74;
        }

        private static void DrawFooterValue(
            Graphics graphics,
            Rectangle content,
            int y,
            string label,
            string value,
            MissionRenderContext context)
        {
            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    y,
                    content.Width /
                    2,
                    28),
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                value,
                context.SmallFont,
                new Rectangle(
                    content.Left +
                    content.Width /
                    2,
                    y,
                    content.Width /
                    2,
                    28),
                context.PhosphorColor,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawSummary(
            Graphics graphics,
            Rectangle panel,
            List<PowerGroup> groups,
            ElectricalTopologyModel model,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 12,
                    panel.Top + 34,
                    panel.Width - 24,
                    panel.Height - 40);

            double amount =
                groups.Sum(
                    group =>
                        group.ChargeAmount);

            double capacity =
                groups.Sum(
                    group =>
                        group.ChargeCapacity);

            double percent =
                capacity >
                0.0001
                    ? amount /
                      capacity *
                      100.0
                    : 0.0;

            int width =
                content.Width /
                4;

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left,
                    content.Top,
                    width,
                    content.Height),
                "EC RESERVE",
                percent.ToString("0") + "%",
                ResolveVesselColor(
                    percent,
                    capacity),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    width,
                    content.Top,
                    width,
                    content.Height),
                "BUS STATUS",
                amount > 0.0001
                    ? "ONLINE"
                    : "OFFLINE",
                amount > 0.0001
                    ? Color.FromArgb(
                        75,
                        235,
                        105)
                    : Color.FromArgb(
                        255,
                        75,
                        55),
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    width *
                    2,
                    content.Top,
                    width,
                    content.Height),
                "POWER GROUPS",
                groups.Count
                    .ToString("00"),
                context.PhosphorColor,
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    width *
                    3,
                    content.Top,
                    content.Width -
                    width *
                    3,
                    content.Height),
                "VESSEL",
                model != null &&
                !string.IsNullOrEmpty(
                    model.VesselName)
                    ? model.VesselName
                    : "--",
                context.DimPhosphorColor,
                context);
        }

        private static void DrawSummaryCell(
            Graphics graphics,
            Rectangle bounds,
            string label,
            string value,
            Color color,
            MissionRenderContext context)
        {
            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        70,
                        105,
                        115),
                    1.0f))
            {
                graphics.DrawLine(
                    divider,
                    bounds.Right,
                    bounds.Top,
                    bounds.Right,
                    bounds.Bottom);
            }

            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top,
                    bounds.Width - 8,
                    18),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                value,
                context.LargeFont,
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 19,
                    bounds.Width - 8,
                    bounds.Height - 19),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static string ResolveStatus(
            PowerGroup group,
            bool vesselPowered,
            double vesselPercent)
        {
            if (group.HasLocalStorage)
            {
                if (group.ChargePercent <=
                    5.0)
                {
                    return "NO POWER";
                }

                if (group.ChargePercent <=
                    15.0)
                {
                    return "LOW " +
                           group.ChargePercent
                               .ToString("0") +
                           "%";
                }

                return "LOCAL " +
                       group.ChargePercent
                           .ToString("0") +
                       "%";
            }

            if (!vesselPowered ||
                vesselPercent <=
                0.1)
            {
                return "NO POWER";
            }

            return "BUS POWER";
        }

        private static Color ResolveStatusColor(
            string status)
        {
            if (status.StartsWith(
                    "NO POWER",
                    StringComparison.Ordinal))
            {
                return Color.FromArgb(
                    255,
                    75,
                    55);
            }

            if (status.StartsWith(
                    "LOW",
                    StringComparison.Ordinal))
            {
                return Color.FromArgb(
                    255,
                    190,
                    55);
            }

            if (status.StartsWith(
                    "BUS",
                    StringComparison.Ordinal))
            {
                return Color.FromArgb(
                    115,
                    195,
                    225);
            }

            return Color.FromArgb(
                75,
                235,
                105);
        }

        private static Color ResolveVesselColor(
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

        private static void DrawBar(
            Graphics graphics,
            Rectangle bounds,
            double percent,
            Color color)
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

            int width =
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

            if (width <= 0)
            {
                return;
            }

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        185,
                        color)))
            {
                graphics.FillRectangle(
                    fill,
                    new Rectangle(
                        bounds.Left + 2,
                        bounds.Top + 2,
                        width,
                        Math.Max(
                            1,
                            bounds.Height - 3)));
            }
        }

        private static void DrawPanel(
            Graphics graphics,
            Rectangle bounds,
            string title,
            MissionRenderContext context)
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
                    bounds.Left + 8,
                    bounds.Top + 28,
                    bounds.Right - 8,
                    bounds.Top + 28);
            }

            TextRenderer.DrawText(
                graphics,
                title,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 3,
                    bounds.Width - 16,
                    21),
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawWaiting(
            Graphics graphics,
            Rectangle panel,
            MissionRenderContext context)
        {
            TextRenderer.DrawText(
                graphics,
                "WAITING FOR VESSEL TOPOLOGY",
                context.LargeFont,
                new Rectangle(
                    panel.Left + 16,
                    panel.Top + 38,
                    panel.Width - 32,
                    panel.Height - 54),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static string FormatStage(
            int stage)
        {
            return
                stage >= 0
                    ? stage.ToString("00")
                    : "--";
        }
    }
}
