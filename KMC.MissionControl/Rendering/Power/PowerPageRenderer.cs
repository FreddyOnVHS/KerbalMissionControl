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
        private enum NodeKind
        {
            Command,
            CoreStage,
            RadialBank
        }

        private enum PowerState
        {
            Local,
            Bus,
            Low,
            Dead
        }

        private sealed class PowerNode
        {
            public PowerNode()
            {
                Name = string.Empty;
                Sections =
                    new List<ElectricalSectionModel>();
            }

            public string Name;
            public NodeKind Kind;
            public int SeparationStage;
            public int ActivationStage;
            public double AverageY;
            public int BatteryCount;
            public int SolarCount;
            public int GeneratorCount;
            public int PartCount;
            public double ChargeAmount;
            public double ChargeCapacity;
            public List<ElectricalSectionModel> Sections;

            public bool HasLocalStorage
            {
                get { return ChargeCapacity > 0.0001; }
            }

            public double ChargePercent
            {
                get
                {
                    if (!HasLocalStorage)
                    {
                        return 0.0;
                    }

                    return ChargeAmount /
                           ChargeCapacity *
                           100.0;
                }
            }
        }

        private sealed class StackRow
        {
            public PowerNode Core;
            public PowerNode Radial;
            public Rectangle Bounds;
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

            Graphics graphics = context.Graphics;
            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            MissionPageLayout layout =
                new MissionPageLayout(context);

            layout.DrawHeader(
                "ELECTRICAL POWER",
                "CH 05");

            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 14,
                    context.ContentBounds.Top + 66,
                    context.ContentBounds.Width - 28,
                    context.ContentBounds.Height - 82);

            Rectangle summary =
                new Rectangle(
                    working.Left,
                    working.Top,
                    working.Width,
                    84);

            int inspectorWidth =
                Math.Max(
                    370,
                    working.Width / 3);

            Rectangle map =
                new Rectangle(
                    working.Left,
                    summary.Bottom + 10,
                    working.Width -
                    inspectorWidth -
                    10,
                    working.Bottom -
                    summary.Bottom -
                    10);

            Rectangle inspector =
                new Rectangle(
                    map.Right + 10,
                    map.Top,
                    inspectorWidth,
                    map.Height);

            DrawPanel(
                graphics,
                summary,
                "VESSEL POWER",
                context);

            DrawPanel(
                graphics,
                map,
                "SPACECRAFT ELECTRICAL TOPOLOGY",
                context);

            DrawPanel(
                graphics,
                inspector,
                "SECTION INSPECTOR",
                context);

            List<PowerNode> nodes =
                BuildNodes(model);

            DrawSummary(
                graphics,
                summary,
                nodes,
                model,
                context);

            if (nodes.Count == 0)
            {
                DrawWaiting(
                    graphics,
                    map,
                    context);

                DrawWaiting(
                    graphics,
                    inspector,
                    context);

                return;
            }

            double vesselAmount =
                nodes.Sum(
                    node => node.ChargeAmount);

            double vesselCapacity =
                nodes.Sum(
                    node => node.ChargeCapacity);

            bool vesselPowered =
                vesselAmount > 0.0001;

            double vesselPercent =
                vesselCapacity > 0.0001
                    ? vesselAmount /
                      vesselCapacity *
                      100.0
                    : 0.0;

            List<StackRow> rows =
                BuildRows(nodes);

            DrawTopology(
                graphics,
                map,
                rows,
                vesselPowered,
                vesselPercent,
                context);

            PowerNode selected =
                nodes.FirstOrDefault(
                    node =>
                        node.Kind ==
                        NodeKind.Command) ??
                nodes.First();

            DrawInspector(
                graphics,
                inspector,
                selected,
                vesselPowered,
                vesselPercent,
                context);
        }

        private static List<PowerNode> BuildNodes(
            ElectricalTopologyModel model)
        {
            List<PowerNode> nodes =
                new List<PowerNode>();

            if (model == null)
            {
                return nodes;
            }

            List<ElectricalSectionModel> command =
                model.Sections
                    .Where(
                        section =>
                            section.IsCommandSection)
                    .ToList();

            if (command.Count > 0)
            {
                nodes.Add(
                    CreateNode(
                        "COMMAND",
                        NodeKind.Command,
                        command));
            }

            foreach (
                IGrouping<int, ElectricalSectionModel> group
                in model.Sections
                    .Where(
                        section =>
                            !section.IsCommandSection &&
                            !section.IsRadialSection)
                    .GroupBy(
                        section =>
                            section.SeparationStage))
            {
                nodes.Add(
                    CreateNode(
                        "STAGE " +
                        FormatStage(group.Key),
                        NodeKind.CoreStage,
                        group.ToList()));
            }

            foreach (
                IGrouping<int, ElectricalSectionModel> group
                in model.Sections
                    .Where(
                        section =>
                            !section.IsCommandSection &&
                            section.IsRadialSection)
                    .GroupBy(
                        section =>
                            section.SeparationStage))
            {
                nodes.Add(
                    CreateNode(
                        "RADIAL ×" +
                        group.Count(),
                        NodeKind.RadialBank,
                        group.ToList()));
            }

            return nodes
                .OrderByDescending(
                    node =>
                        node.Kind ==
                        NodeKind.Command)
                .ThenByDescending(
                    node => node.AverageY)
                .ThenByDescending(
                    node => node.SeparationStage)
                .ToList();
        }

        private static PowerNode CreateNode(
            string name,
            NodeKind kind,
            List<ElectricalSectionModel> sections)
        {
            PowerNode node =
                new PowerNode();

            node.Name = name;
            node.Kind = kind;
            node.Sections.AddRange(sections);

            node.SeparationStage =
                MostCommon(
                    sections.Select(
                        section =>
                            section.SeparationStage));

            node.ActivationStage =
                MostCommon(
                    sections.Select(
                        section =>
                            section.ActivationStage));

            node.AverageY =
                sections.Count > 0
                    ? sections.Average(
                        section =>
                            section.AverageY)
                    : 0.0;

            node.BatteryCount =
                sections.Sum(
                    section =>
                        section.BatteryPartCount);

            node.SolarCount =
                sections.Sum(
                    section =>
                        section.SolarPartCount);

            node.GeneratorCount =
                sections.Sum(
                    section =>
                        section.GeneratorPartCount);

            node.PartCount =
                sections.Sum(
                    section =>
                        section.PartCount);

            node.ChargeAmount =
                sections.Sum(
                    section =>
                        section.ElectricChargeAmount);

            node.ChargeCapacity =
                sections.Sum(
                    section =>
                        section.ElectricChargeCapacity);

            return node;
        }

        private static int MostCommon(
            IEnumerable<int> values)
        {
            List<int> valid =
                values
                    .Where(value => value >= 0)
                    .ToList();

            if (valid.Count == 0)
            {
                return -1;
            }

            return valid
                .GroupBy(value => value)
                .OrderByDescending(
                    group => group.Count())
                .ThenByDescending(
                    group => group.Key)
                .First()
                .Key;
        }

        private static List<StackRow> BuildRows(
            List<PowerNode> nodes)
        {
            List<PowerNode> cores =
                nodes
                    .Where(
                        node =>
                            node.Kind !=
                            NodeKind.RadialBank)
                    .OrderByDescending(
                        node => node.AverageY)
                    .ToList();

            List<PowerNode> radial =
                nodes
                    .Where(
                        node =>
                            node.Kind ==
                            NodeKind.RadialBank)
                    .ToList();

            List<StackRow> rows =
                cores.Select(
                    core =>
                        new StackRow
                        {
                            Core = core
                        })
                    .ToList();

            foreach (PowerNode bank in radial)
            {
                StackRow nearest =
                    rows
                        .OrderBy(
                            row =>
                                Math.Abs(
                                    row.Core.AverageY -
                                    bank.AverageY))
                        .FirstOrDefault();

                if (nearest == null)
                {
                    continue;
                }

                if (nearest.Radial == null)
                {
                    nearest.Radial = bank;
                }
                else
                {
                    MergeNode(
                        nearest.Radial,
                        bank);
                }
            }

            return rows;
        }

        private static void MergeNode(
            PowerNode target,
            PowerNode source)
        {
            target.Sections.AddRange(
                source.Sections);

            target.Name =
                "RADIAL ×" +
                target.Sections.Count;

            target.BatteryCount +=
                source.BatteryCount;

            target.SolarCount +=
                source.SolarCount;

            target.GeneratorCount +=
                source.GeneratorCount;

            target.PartCount +=
                source.PartCount;

            target.ChargeAmount +=
                source.ChargeAmount;

            target.ChargeCapacity +=
                source.ChargeCapacity;
        }

        private static void DrawTopology(
            Graphics graphics,
            Rectangle panel,
            List<StackRow> rows,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 26,
                    panel.Top + 42,
                    panel.Width - 52,
                    panel.Height - 58);

            int count =
                Math.Max(1, rows.Count);

            int gap =
                count > 9
                    ? 5
                    : 10;

            int available =
                content.Height -
                gap *
                Math.Max(0, count - 1);

            int rowHeight =
                Math.Max(
                    44,
                    Math.Min(
                        92,
                        available / count));

            int centerWidth =
                Math.Max(
                    230,
                    Math.Min(
                        340,
                        content.Width *
                        44 /
                        100));

            int radialWidth =
                Math.Max(
                    120,
                    Math.Min(
                        180,
                        content.Width *
                        22 /
                        100));

            int centerX =
                content.Left +
                content.Width / 2;

            int totalHeight =
                rowHeight *
                count +
                gap *
                Math.Max(0, count - 1);

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
                rows[index].Bounds =
                    new Rectangle(
                        centerX -
                        centerWidth / 2,
                        startY +
                        index *
                        (rowHeight + gap),
                        centerWidth,
                        rowHeight);
            }

            DrawCoreConnectors(
                graphics,
                rows);

            for (int index = 0;
                 index < rows.Count;
                 index++)
            {
                StackRow row = rows[index];

                DrawCoreNode(
                    graphics,
                    row.Bounds,
                    row.Core,
                    vesselPowered,
                    vesselPercent,
                    context);

                if (row.Radial != null)
                {
                    DrawRadialLaneNode(
                        graphics,
                        row,
                        radialWidth,
                        vesselPowered,
                        vesselPercent,
                        context);
                }
            }
        }

        private static void DrawCoreConnectors(
            Graphics graphics,
            List<StackRow> rows)
        {
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        80,
                        135,
                        145),
                    2.0f))
            {
                for (int index = 0;
                     index < rows.Count - 1;
                     index++)
                {
                    int x =
                        rows[index]
                            .Bounds.Left +
                        rows[index]
                            .Bounds.Width / 2;

                    graphics.DrawLine(
                        pen,
                        x,
                        rows[index].Bounds.Bottom,
                        x,
                        rows[index + 1].Bounds.Top);
                }
            }
        }

        private static void DrawCoreNode(
            Graphics graphics,
            Rectangle bounds,
            PowerNode node,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            PowerState state =
                ResolveState(
                    node,
                    vesselPowered,
                    vesselPercent);

            Color color =
                ResolveStateColor(state);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        10,
                        color)))
            using (Pen outline =
                new Pen(
                    color,
                    node.Kind ==
                    NodeKind.Command
                        ? 2.2f
                        : 1.4f))
            {
                graphics.FillRectangle(
                    fill,
                    bounds);

                graphics.DrawRectangle(
                    outline,
                    bounds);
            }

            if (node.Kind ==
                NodeKind.Command)
            {
                Point[] nose =
                    new[]
                    {
                        new Point(
                            bounds.Left +
                            bounds.Width / 2,
                            bounds.Top - 24),

                        new Point(
                            bounds.Left +
                            bounds.Width / 2 - 24,
                            bounds.Top),

                        new Point(
                            bounds.Left +
                            bounds.Width / 2 + 24,
                            bounds.Top)
                    };

                using (Pen pen =
                    new Pen(
                        color,
                        1.7f))
                {
                    graphics.DrawPolygon(
                        pen,
                        nose);
                }
            }

            Font font =
                bounds.Height >= 70
                    ? context.LargeFont
                    : context.SmallFont;

            TextRenderer.DrawText(
                graphics,
                node.Name,
                font,
                new Rectangle(
                    bounds.Left + 36,
                    bounds.Top + 4,
                    bounds.Width - 110,
                    bounds.Height - 8),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawStateLamp(
                graphics,
                new Point(
                    bounds.Left + 18,
                    bounds.Top +
                    bounds.Height / 2),
                color);

            DrawHardwareIcons(
                graphics,
                bounds,
                node,
                context);
        }

        private static void DrawRadialLaneNode(
            Graphics graphics,
            StackRow row,
            int width,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            Rectangle core = row.Bounds;

            int height =
                Math.Max(
                    34,
                    Math.Min(
                        52,
                        core.Height - 8));

            Rectangle badge =
                new Rectangle(
                    core.Left -
                    width -
                    22,
                    core.Top +
                    (core.Height -
                     height) / 2,
                    width,
                    height);

            PowerState state =
                ResolveState(
                    row.Radial,
                    vesselPowered,
                    vesselPercent);

            Color color =
                ResolveStateColor(state);

            using (Pen connector =
                new Pen(
                    Color.FromArgb(
                        70,
                        115,
                        125),
                    1.2f))
            {
                graphics.DrawLine(
                    connector,
                    badge.Right,
                    badge.Top +
                    badge.Height / 2,
                    core.Left,
                    core.Top +
                    core.Height / 2);
            }

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        9,
                        color)))
            using (Pen outline =
                new Pen(
                    color,
                    1.2f))
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
                row.Radial.Name,
                context.SmallFont,
                new Rectangle(
                    badge.Left + 6,
                    badge.Top + 3,
                    badge.Width - 12,
                    badge.Height - 6),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawStateLamp(
            Graphics graphics,
            Point center,
            Color color)
        {
            Rectangle lamp =
                new Rectangle(
                    center.X - 6,
                    center.Y - 6,
                    12,
                    12);

            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        150,
                        color)))
            using (Pen outline =
                new Pen(
                    color,
                    1.0f))
            {
                graphics.FillEllipse(
                    fill,
                    lamp);

                graphics.DrawEllipse(
                    outline,
                    lamp);
            }
        }

        private static void DrawHardwareIcons(
            Graphics graphics,
            Rectangle bounds,
            PowerNode node,
            MissionRenderContext context)
        {
            int x = bounds.Right - 14;
            int centerY =
                bounds.Top +
                bounds.Height / 2;

            if (node.GeneratorCount > 0)
            {
                DrawIcon(
                    graphics,
                    "G",
                    ref x,
                    centerY,
                    Color.FromArgb(
                        155,
                        235,
                        155),
                    context);
            }

            if (node.SolarCount > 0)
            {
                DrawIcon(
                    graphics,
                    "S",
                    ref x,
                    centerY,
                    Color.FromArgb(
                        80,
                        205,
                        255),
                    context);
            }

            if (node.BatteryCount > 0)
            {
                DrawIcon(
                    graphics,
                    "B",
                    ref x,
                    centerY,
                    Color.FromArgb(
                        255,
                        190,
                        55),
                    context);
            }
        }

        private static void DrawIcon(
            Graphics graphics,
            string label,
            ref int x,
            int centerY,
            Color color,
            MissionRenderContext context)
        {
            Rectangle box =
                new Rectangle(
                    x - 18,
                    centerY - 10,
                    18,
                    20);

            using (Pen pen =
                new Pen(
                    color,
                    1.0f))
            {
                graphics.DrawRectangle(
                    pen,
                    box);
            }

            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                box,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            x -= 23;
        }

        private static PowerState ResolveState(
            PowerNode node,
            bool vesselPowered,
            double vesselPercent)
        {
            if (node.HasLocalStorage)
            {
                if (node.ChargePercent <= 5.0)
                {
                    return PowerState.Dead;
                }

                if (node.ChargePercent <= 15.0)
                {
                    return PowerState.Low;
                }

                return PowerState.Local;
            }

            if (!vesselPowered ||
                vesselPercent <= 0.1)
            {
                return PowerState.Dead;
            }

            return PowerState.Bus;
        }

        private static Color ResolveStateColor(
            PowerState state)
        {
            switch (state)
            {
                case PowerState.Local:
                    return Color.FromArgb(
                        75,
                        235,
                        105);

                case PowerState.Bus:
                    return Color.FromArgb(
                        115,
                        195,
                        225);

                case PowerState.Low:
                    return Color.FromArgb(
                        255,
                        190,
                        55);

                default:
                    return Color.FromArgb(
                        255,
                        75,
                        55);
            }
        }

        private static string GetStateText(
            PowerState state,
            PowerNode node)
        {
            switch (state)
            {
                case PowerState.Local:
                    return "LOCAL " +
                           node.ChargePercent
                               .ToString("0") +
                           "%";

                case PowerState.Bus:
                    return "BUS POWER";

                case PowerState.Low:
                    return "LOW " +
                           node.ChargePercent
                               .ToString("0") +
                           "%";

                default:
                    return "NO POWER";
            }
        }

        private static void DrawInspector(
            Graphics graphics,
            Rectangle panel,
            PowerNode selected,
            bool vesselPowered,
            double vesselPercent,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 20,
                    panel.Top + 44,
                    panel.Width - 40,
                    panel.Height - 60);

            PowerState state =
                ResolveState(
                    selected,
                    vesselPowered,
                    vesselPercent);

            Color color =
                ResolveStateColor(state);

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
                GetStateText(
                    state,
                    selected),
                context.LargeFont,
                new Rectangle(
                    content.Left,
                    content.Top + 40,
                    content.Width,
                    32),
                color,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int y =
                content.Top + 96;

            DrawInspectorRow(
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

            DrawInspectorRow(
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

            DrawInspectorRow(
                graphics,
                content,
                ref y,
                "BATTERIES",
                selected.BatteryCount
                    .ToString("00"),
                context);

            DrawInspectorRow(
                graphics,
                content,
                ref y,
                "SOLAR PANELS",
                selected.SolarCount
                    .ToString("00"),
                context);

            DrawInspectorRow(
                graphics,
                content,
                ref y,
                "GENERATORS",
                selected.GeneratorCount
                    .ToString("00"),
                context);

            DrawInspectorRow(
                graphics,
                content,
                ref y,
                "PARTS",
                selected.PartCount
                    .ToString("00"),
                context);

            DrawInspectorRow(
                graphics,
                content,
                ref y,
                "SEP / ACT",
                FormatStage(
                    selected.SeparationStage) +
                " / " +
                FormatStage(
                    selected.ActivationStage),
                context);

            TextRenderer.DrawText(
                graphics,
                "PHASE 2: CLICK A SECTION TO INSPECT",
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    content.Bottom - 32,
                    content.Width,
                    24),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawInspectorRow(
            Graphics graphics,
            Rectangle content,
            ref int y,
            string label,
            string value,
            MissionRenderContext context)
        {
            if (y + 56 >
                content.Bottom - 36)
            {
                return;
            }

            TextRenderer.DrawText(
                graphics,
                label,
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    y,
                    content.Width,
                    17),
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
                    y + 18,
                    content.Width,
                    28),
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        50,
                        85,
                        95),
                    1.0f))
            {
                graphics.DrawLine(
                    divider,
                    content.Left,
                    y + 50,
                    content.Right,
                    y + 50);
            }

            y += 57;
        }

        private static void DrawSummary(
            Graphics graphics,
            Rectangle panel,
            List<PowerNode> nodes,
            ElectricalTopologyModel model,
            MissionRenderContext context)
        {
            Rectangle content =
                new Rectangle(
                    panel.Left + 12,
                    panel.Top + 31,
                    panel.Width - 24,
                    panel.Height - 36);

            double amount =
                nodes.Sum(
                    node => node.ChargeAmount);

            double capacity =
                nodes.Sum(
                    node => node.ChargeCapacity);

            double percent =
                capacity > 0.0001
                    ? amount /
                      capacity *
                      100.0
                    : 0.0;

            int width =
                content.Width / 4;

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
                    content.Left + width,
                    content.Top,
                    width,
                    content.Height),
                "BUS",
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
                    width * 2,
                    content.Top,
                    width,
                    content.Height),
                "SECTIONS",
                nodes.Count.ToString("00"),
                context.PhosphorColor,
                context);

            DrawSummaryCell(
                graphics,
                new Rectangle(
                    content.Left +
                    width * 3,
                    content.Top,
                    content.Width -
                    width * 3,
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

        private static Color ResolveVesselColor(
            double percent,
            double capacity)
        {
            if (capacity <= 0.0001)
            {
                return Color.FromArgb(
                    105,
                    140,
                    150);
            }

            if (percent <= 5.0)
            {
                return Color.FromArgb(
                    255,
                    75,
                    55);
            }

            if (percent <= 15.0)
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
                        65,
                        100,
                        110),
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
                    17),
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
                    bounds.Top + 18,
                    bounds.Width - 8,
                    bounds.Height - 18),
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
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
                    bounds.Top + 27,
                    bounds.Right - 8,
                    bounds.Top + 27);
            }

            TextRenderer.DrawText(
                graphics,
                title,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 8,
                    bounds.Top + 3,
                    bounds.Width - 16,
                    20),
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
                    panel.Top + 36,
                    panel.Width - 32,
                    panel.Height - 52),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static string FormatStage(
            int stage)
        {
            return stage >= 0
                ? stage.ToString("00")
                : "--";
        }
    }
}
