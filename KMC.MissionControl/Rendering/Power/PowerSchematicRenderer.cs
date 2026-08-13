using System;
using System.Drawing;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.11.3 switched-source EECOM POWER 1/2 schematic.
    ///
    /// The renderer intentionally uses simple GDI primitives and direct model
    /// access only. It does not call the legacy POWER renderer, build LINQ
    /// lists, measure text every frame, or draw hidden panels.
    ///
    /// Current synthetic distribution truth is shown as:
    /// SOURCE -> CONTACTOR -> SOURCE TRANSFER -> MAIN BUS -> ESS FEEDS -> LOAD BREAKERS -> LOADS.
    ///
    /// Contactors/breakers are visualized as the future switching layer, but
    /// this build does not invent separate hardware truth. Current source/feed
    /// command state is labeled CMD OPEN/CLOSED. Hardware/indication truth is
    /// intentionally reserved for the next electrical-switching milestone.
    /// </summary>
    public static class PowerSchematicRenderer
    {
        private static readonly Color Healthy =
            Color.FromArgb(112, 202, 154);

        private static readonly Color Advisory =
            Color.FromArgb(232, 188, 84);

        private static readonly Color Warning =
            Color.FromArgb(236, 142, 66);

        private static readonly Color Critical =
            Color.FromArgb(236, 92, 76);

        private static readonly Color Dead =
            Color.FromArgb(110, 125, 120);

        private static readonly Color Panel =
            Color.FromArgb(255, 5, 13, 12);

        public static void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Graphics g = context.Graphics;

            MissionPageLayout page =
                new MissionPageLayout(context);

            page.DrawHeader(
                "ELECTRICAL POWER / ONE-LINE",
                "CH 05");

            Rectangle area =
                new Rectangle(
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 70,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 92);

            DrawPageSelector(
                g,
                area,
                context);

            if (engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.SpacecraftSystems == null ||
                engineering.Snapshot.SpacecraftSystems.ElectricalDistribution == null)
            {
                DrawWaiting(g, area, context);
                return;
            }

            SyntheticElectricalDistributionModel distribution =
                engineering.Snapshot
                    .SpacecraftSystems
                    .ElectricalDistribution;

            DrawSchematic(
                g,
                area,
                distribution,
                engineering,
                context);
        }

        private static void DrawSchematic(
            Graphics g,
            Rectangle area,
            SyntheticElectricalDistributionModel distribution,
            AnalysisPipelineResult engineering,
            MissionRenderContext context)
        {
            int left = area.Left + 36;
            int right = area.Right - 36;
            int width = right - left;

            int top = area.Top + 42;
            int sourceY = top + 40;
            int sourceH = 124;

            // Dedicated source-contactor lane.
            int deviceY = sourceY + sourceH + 30;

            // Dedicated transfer-selector lane.
            int transferY = deviceY + 116;

            // Main buses are below the transfer row.
            int busY = transferY + 100;
            int busH = 124;
            int feedY = busY + busH + 46;
            int essY = feedY + 92;
            int essH = 118;
            int loadY = essY + essH + 52;
            int summaryY = area.Bottom - 140;

            int colGap = Math.Max(28, width / 40);
            int halfW = (width - colGap) / 2;
            int aLeft = left;
            int bLeft = left + halfW + colGap;

            SyntheticElectricalBus mainA =
                distribution.FindBus("BUS_MAIN_A");

            SyntheticElectricalBus mainB =
                distribution.FindBus("BUS_MAIN_B");

            SyntheticElectricalBus ess =
                distribution.FindBus("BUS_ESS");

            SyntheticElectricalSource genA =
                distribution.FindSource("SRC_GEN_A");

            SyntheticElectricalSource batA =
                distribution.FindSource("SRC_BAT_A");

            SyntheticElectricalSource genB =
                distribution.FindSource("SRC_GEN_B");

            SyntheticElectricalSource batB =
                distribution.FindSource("SRC_BAT_B");

            SyntheticElectricalSource feedA =
                distribution.FindSource("FEED_ESS_A");

            SyntheticElectricalSource feedB =
                distribution.FindSource("FEED_ESS_B");

            DrawSectionCaption(
                g,
                new Rectangle(left, top - 8, width, 34),
                "SOURCES",
                context);

            Rectangle genABox =
                new Rectangle(aLeft, sourceY, halfW / 2 - 10, sourceH);

            Rectangle batABox =
                new Rectangle(genABox.Right + 20, sourceY, halfW / 2 - 10, sourceH);

            Rectangle genBBox =
                new Rectangle(bLeft, sourceY, halfW / 2 - 10, sourceH);

            Rectangle batBBox =
                new Rectangle(genBBox.Right + 20, sourceY, halfW / 2 - 10, sourceH);

            DrawSourceBox(g, genABox, genA, context);
            DrawSourceBox(g, batABox, batA, context);
            DrawSourceBox(g, genBBox, genB, context);
            DrawSourceBox(g, batBBox, batB, context);

            int aCenter = aLeft + halfW / 2;
            int bCenter = bLeft + halfW / 2;

            DrawVerticalWire(
                g,
                genABox.Bottom,
                deviceY,
                genABox.Left + genABox.Width / 2,
                SourceColor(genA, context));

            DrawVerticalWire(
                g,
                batABox.Bottom,
                deviceY,
                batABox.Left + batABox.Width / 2,
                SourceColor(batA, context));

            DrawVerticalWire(
                g,
                genBBox.Bottom,
                deviceY,
                genBBox.Left + genBBox.Width / 2,
                SourceColor(genB, context));

            DrawVerticalWire(
                g,
                batBBox.Bottom,
                deviceY,
                batBBox.Left + batBBox.Width / 2,
                SourceColor(batB, context));

            DrawContactor(
                g,
                new Point(genABox.Left + genABox.Width / 2, deviceY + 24),
                SwitchActualClosed(distribution, "CONT_GEN_A"),
                "GEN A",
                context);

            DrawContactor(
                g,
                new Point(batABox.Left + batABox.Width / 2, deviceY + 24),
                SwitchActualClosed(distribution, "CONT_BAT_A"),
                "BAT A",
                context);

            DrawContactor(
                g,
                new Point(genBBox.Left + genBBox.Width / 2, deviceY + 24),
                SwitchActualClosed(distribution, "CONT_GEN_B"),
                "GEN B",
                context);

            DrawContactor(
                g,
                new Point(batBBox.Left + batBBox.Width / 2, deviceY + 24),
                SwitchActualClosed(distribution, "CONT_BAT_B"),
                "BAT B",
                context);

            DrawTransferSelector(
                g,
                new Point(aCenter, transferY),
                distribution.FindSwitch("XFER_MAIN_A"),
                mainA,
                "MAIN A XFER",
                context);

            DrawTransferSelector(
                g,
                new Point(bCenter, transferY),
                distribution.FindSwitch("XFER_MAIN_B"),
                mainB,
                "MAIN B XFER",
                context);

            DrawSectionCaption(
                g,
                new Rectangle(left, busY - 42, width, 34),
                "MAIN DISTRIBUTION BUSES",
                context);

            Rectangle mainABox =
                new Rectangle(aLeft, busY, halfW, busH);

            Rectangle mainBBox =
                new Rectangle(bLeft, busY, halfW, busH);

            DrawBusBox(g, mainABox, mainA, context);
            DrawBusBox(g, mainBBox, mainB, context);

            DrawSourceMerge(
                g,
                genABox,
                batABox,
                deviceY + 48,
                mainABox.Top,
                mainA,
                context);

            DrawSourceMerge(
                g,
                genBBox,
                batBBox,
                deviceY + 48,
                mainBBox.Top,
                mainB,
                context);

            DrawSectionCaption(
                g,
                new Rectangle(left, feedY - 36, width, 34),
                "BUS TIE / ESSENTIAL FEEDS",
                context);

            int essWidth = Math.Min(620, width * 44 / 100);
            Rectangle essBox =
                new Rectangle(
                    left + (width - essWidth) / 2,
                    essY,
                    essWidth,
                    essH);

            int feedAX = essBox.Left + essBox.Width / 3;
            int feedBX = essBox.Right - essBox.Width / 3;

            DrawFeedPath(
                g,
                mainABox,
                new Point(feedAX, feedY + 28),
                essBox,
                feedA,
                "ESS A",
                context);

            DrawFeedPath(
                g,
                mainBBox,
                new Point(feedBX, feedY + 28),
                essBox,
                feedB,
                "ESS B",
                context);

            DrawBusBox(g, essBox, ess, context);

            DrawSectionCaption(
                g,
                new Rectangle(left, loadY - 38, width, 34),
                "MAJOR LOAD BRANCHES",
                context);

            int loadGap = 14;
            int loadWidth = (width - loadGap * 6) / 7;
            int loadH = 124;

            DrawLoadBranch(g, distribution, "GUID_A", aLeft + 0 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, context);
            DrawLoadBranch(g, distribution, "COMM_A", aLeft + 1 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, context);
            DrawLoadBranch(g, distribution, "PUMP_A", aLeft + 2 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, context);
            DrawLoadBranch(g, distribution, "FLIGHT_COMPUTER", aLeft + 3 * (loadWidth + loadGap), loadY, loadWidth, loadH, essBox, context);
            DrawLoadBranch(g, distribution, "GUID_B", aLeft + 4 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, context);
            DrawLoadBranch(g, distribution, "COMM_B", aLeft + 5 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, context);
            DrawLoadBranch(g, distribution, "PUMP_B", aLeft + 6 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, context);

            DrawRealEcSummary(
                g,
                new Rectangle(left, summaryY, width, 102),
                engineering,
                context);

            DrawFooter(
                g,
                new Rectangle(left, area.Bottom - 38, width, 34),
                context);
        }

        private static void DrawSourceBox(
            Graphics g,
            Rectangle box,
            SyntheticElectricalSource source,
            MissionRenderContext context)
        {
            Color color = SourceColor(source, context);
            DrawBox(g, box, color);

            string name = source != null
                ? source.DisplayName
                : "SOURCE --";

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 8, box.Width - 28, 34), name, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            string state = source == null
                ? "UNAVAILABLE"
                : !source.CommandedAvailable
                    ? "CMD OFF"
                    : source.State == SyntheticElectricalSourceState.Offline
                        ? "OFFLINE"
                        : source.Conducting
                            ? "ACTIVE / FEEDING"
                            : source.Kind == SyntheticElectricalSourceKind.Battery
                                ? "STANDBY / AVAILABLE"
                                : "AVAILABLE / NOT FEEDING";

            string amps = source != null
                ? "RATED " + source.RatedAvailableCurrentAmps.ToString("0.0") + " A"
                : "--";

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 44, box.Width - 28, 32), state, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 80, box.Width - 28, 32), amps, context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawBusBox(
            Graphics g,
            Rectangle box,
            SyntheticElectricalBus bus,
            MissionRenderContext context)
        {
            Color color = BusColor(bus, context);
            DrawBox(g, box, color);

            string name = bus != null ? bus.DisplayName : "BUS --";
            string state = bus != null ? SplitWords(bus.State.ToString()) : "UNAVAILABLE";
            string voltage = bus != null ? bus.Voltage.ToString("0.0") + " V" : "--";
            string load = bus != null ? bus.DemandAmps.ToString("0.0") + " A / " + bus.AvailableCurrentAmps.ToString("0.0") + " A" : "--";
            string percent = bus != null ? FormatPercent(bus.LoadPercent) : "--";
            string activeSource = bus != null && !string.IsNullOrWhiteSpace(bus.ActiveSourceId)
                ? bus.ActiveSourceId.Replace("SRC_", string.Empty)
                : "NONE";

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 8, box.Width - 28, 34),
                name, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 44, box.Width - 28, 32),
                state + "   " + voltage, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 82, box.Width - 28, 32),
                "SOURCE " + activeSource + "   DEMAND " + load + "   LOAD " + percent,
                context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static void DrawContactor(
            Graphics g,
            Point center,
            bool closed,
            string label,
            MissionRenderContext context)
        {
            int r = 8;
            Color color = closed ? Healthy : Advisory;

            using (Pen pen = new Pen(color, 2.0f))
            {
                g.DrawEllipse(pen, center.X - r, center.Y - r, r * 2, r * 2);
                g.DrawLine(pen, center.X, center.Y - 26, center.X, center.Y - r);
                g.DrawLine(pen, center.X, center.Y + r, center.X, center.Y + 26);

                if (closed)
                {
                    g.DrawLine(pen, center.X - 5, center.Y, center.X + 5, center.Y);
                }
                else
                {
                    g.DrawLine(pen, center.X - 5, center.Y + 4, center.X + 5, center.Y - 5);
                }
            }

            Rectangle labelBox =
                new Rectangle(
                    center.X - 118,
                    center.Y + 30,
                    236,
                    34);

            using (SolidBrush labelBackground =
                new SolidBrush(Panel))
            {
                g.FillRectangle(
                    labelBackground,
                    labelBox);
            }

            DrawText(g, labelBox,
                label + "  " + (closed ? "CLOSED" : "OPEN"),
                context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static bool SwitchActualClosed(
            SyntheticElectricalDistributionModel distribution,
            string id)
        {
            SyntheticElectricalSwitch item =
                distribution != null ? distribution.FindSwitch(id) : null;
            return item != null && item.ActualClosed;
        }

        private static void DrawTransferSelector(
            Graphics g,
            Point center,
            SyntheticElectricalSwitch transfer,
            SyntheticElectricalBus bus,
            string label,
            MissionRenderContext context)
        {
            bool closed = transfer != null && transfer.ActualClosed;
            Color color = closed && bus != null && !string.IsNullOrWhiteSpace(bus.ActiveSourceId)
                ? Healthy
                : Advisory;

            Rectangle box =
                new Rectangle(
                    center.X - 210,
                    center.Y - 34,
                    420,
                    76);

            DrawBox(g, box, color);

            string source =
                bus != null &&
                !string.IsNullOrWhiteSpace(bus.ActiveSourceId)
                    ? bus.ActiveSourceId.Replace("SRC_", string.Empty)
                    : "NONE";

            DrawText(
                g,
                new Rectangle(
                    box.Left + 12,
                    box.Top + 8,
                    box.Width - 24,
                    28),
                label + "  AUTO",
                context.SmallFont,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            DrawText(
                g,
                new Rectangle(
                    box.Left + 12,
                    box.Top + 40,
                    box.Width - 24,
                    28),
                "SELECTED " + source,
                context.SmallFont,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawFeedPath(
            Graphics g,
            Rectangle sourceBus,
            Point device,
            Rectangle essBus,
            SyntheticElectricalSource feed,
            string label,
            MissionRenderContext context)
        {
            bool closed =
                feed != null &&
                feed.CommandedAvailable;
            Color color = SourceColor(feed, context);
            int sourceX = sourceBus.Left + sourceBus.Width / 2;
            int sourceY = sourceBus.Bottom;

            /*
             * Keep the horizontal feed conductor out of the contactor label band.
             * The path drops from the source bus, jogs horizontally above the
             * device symbol, then continues vertically through the contactor to
             * the ESS bus. This preserves a clean text lane under the symbol.
             */
            int horizontalY = device.Y - 34;

            using (Pen pen = new Pen(color, 2.0f))
            {
                g.DrawLine(pen, sourceX, sourceY, sourceX, horizontalY);
                g.DrawLine(pen, sourceX, horizontalY, device.X, horizontalY);
                g.DrawLine(pen, device.X, horizontalY, device.X, device.Y - 26);
                g.DrawLine(pen, device.X, device.Y + 26, device.X, essBus.Top);
            }

            DrawContactor(g, device, closed, label, context);
        }

        private static void DrawSourceMerge(
            Graphics g,
            Rectangle first,
            Rectangle second,
            int mergeY,
            int busTop,
            SyntheticElectricalBus bus,
            MissionRenderContext context)
        {
            Color color = BusColor(bus, context);
            int x1 = first.Left + first.Width / 2;
            int x2 = second.Left + second.Width / 2;
            int mid = (x1 + x2) / 2;

            using (Pen pen = new Pen(color, 2.0f))
            {
                g.DrawLine(pen, x1, mergeY, x1, mergeY + 8);
                g.DrawLine(pen, x2, mergeY, x2, mergeY + 8);
                g.DrawLine(pen, x1, mergeY + 8, x2, mergeY + 8);
                g.DrawLine(pen, mid, mergeY + 8, mid, busTop);
            }
        }

        private static void DrawLoadBranch(
            Graphics g,
            SyntheticElectricalDistributionModel distribution,
            string id,
            int x,
            int y,
            int width,
            int height,
            Rectangle parentBus,
            MissionRenderContext context)
        {
            SyntheticElectricalLoad load = FindLoad(distribution, id);
            SyntheticElectricalSwitch breaker =
                load != null ? distribution.FindSwitch(load.BreakerId) : null;
            bool commanded = load != null && load.CommandedOn;
            bool actualClosed = breaker != null ? breaker.ActualClosed : commanded;
            Color color = actualClosed ? Healthy : Dead;

            Rectangle box = new Rectangle(x, y, width, height);
            int center = box.Left + box.Width / 2;
            int parentCenter = Math.Max(parentBus.Left + 8, Math.Min(parentBus.Right - 8, center));

            using (Pen wire = new Pen(color, 1.6f))
            {
                g.DrawLine(wire, parentCenter, parentBus.Bottom, parentCenter, y - 18);
                g.DrawLine(wire, parentCenter, y - 18, center, y - 18);
                g.DrawLine(wire, center, y - 18, center, y);
            }

            DrawBox(g, box, color);

            string name = load != null ? load.DisplayName : id;
            string demand = load != null ? load.DemandAmps.ToString("0.0") + " A" : "--";
            string state = load != null
                ? "BRK " + (actualClosed ? "CLOSED" : "OPEN")
                : "--";
            string priority = load != null ? "PRI " + load.Priority.ToString() : "--";

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 8, box.Width - 24, 34), name, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 46, box.Width - 24, 32), state, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 84, box.Width - 24, 30), demand + "  " + priority, context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static SyntheticElectricalLoad FindLoad(
            SyntheticElectricalDistributionModel distribution,
            string id)
        {
            if (distribution == null ||
                distribution.Loads == null ||
                string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int index = 0; index < distribution.Loads.Count; index++)
            {
                SyntheticElectricalLoad load = distribution.Loads[index];

                if (load != null &&
                    string.Equals(load.EquipmentId, id, StringComparison.Ordinal))
                {
                    return load;
                }
            }

            return null;
        }

        private static void DrawRealEcSummary(
            Graphics g,
            Rectangle box,
            AnalysisPipelineResult engineering,
            MissionRenderContext context)
        {
            DrawBox(g, box, context.DimPhosphorColor);

            PowerModel power = engineering != null && engineering.Snapshot != null
                ? engineering.Snapshot.Power
                : null;

            string storage = "--";
            string reserve = "--";
            string net = "--";
            string endurance = "--";

            if (power != null && power.Diagnostic != null && power.Diagnostic.TelemetryAvailable)
            {
                storage = power.Diagnostic.StoredEc.ToString("0.0") + "/" + power.Diagnostic.CapacityEc.ToString("0.0") + " EC";
                reserve = power.Diagnostic.ReservePercent.ToString("0.0") + "%";

                if (power.Diagnostic.HasEndurance)
                {
                    endurance = FormatDuration(power.Diagnostic.EnduranceSeconds);
                }
            }

            if (power != null && power.Flow != null && power.Flow.HasMeasuredNetStorageRate)
            {
                net = power.Flow.NetStorageRateEcPerSecond.ToString("+0.00;-0.00;0.00") + " EC/S";
            }

            DrawText(g, new Rectangle(box.Left + 16, box.Top + 6, box.Width - 32, 34),
                "REAL KSP ELECTRICCHARGE / OBSERVED TELEMETRY",
                context.SmallFont, context.PhosphorColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int col = (box.Width - 32) / 4;
            DrawValueCell(g, new Rectangle(box.Left + 16, box.Top + 42, col, 72), "STORAGE", storage, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col, box.Top + 42, col, 72), "RESERVE", reserve, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col * 2, box.Top + 42, col, 72), "NET FLOW", net, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col * 3, box.Top + 42, col, 72), "ENDURANCE", endurance, context);
        }

        private static void DrawValueCell(
            Graphics g,
            Rectangle box,
            string label,
            string value,
            MissionRenderContext context)
        {
            DrawText(g, new Rectangle(box.Left, box.Top, box.Width, 32), label, context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawText(g, new Rectangle(box.Left, box.Top + 36, box.Width, 32), value, context.SmallFont, context.PhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static void DrawPageSelector(
            Graphics g,
            Rectangle area,
            MissionRenderContext context)
        {
            Rectangle selector =
                new Rectangle(area.Right - 390, area.Top + 2, 378, 40);

            Rectangle one =
                new Rectangle(selector.Left, selector.Top, 180, selector.Height);

            Rectangle two =
                new Rectangle(one.Right + 18, selector.Top, 180, selector.Height);

            DrawBox(g, one, Healthy);
            DrawBox(g, two, context.DimPhosphorColor);

            DrawText(g, one, "POWER  1/2", context.SmallFont, Healthy,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawText(g, two, "2/2  DETAIL", context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawSectionCaption(
            Graphics g,
            Rectangle box,
            string text,
            MissionRenderContext context)
        {
            DrawText(g, box, text, context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawFooter(
            Graphics g,
            Rectangle box,
            MissionRenderContext context)
        {
            DrawText(g, box,
                "SWITCHED DISTRIBUTION / CMD + ACTUAL + CONDUCTION / GENERATOR PRIMARY / BATTERY AUTO-TRANSFER",
                context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static void DrawWaiting(
            Graphics g,
            Rectangle area,
            MissionRenderContext context)
        {
            DrawText(g,
                new Rectangle(area.Left + 30, area.Top + 120, area.Width - 60, 60),
                "WAITING FOR ENGINE-OWNED ELECTRICAL DISTRIBUTION",
                context.LargeFont,
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static void DrawBox(
            Graphics g,
            Rectangle box,
            Color color)
        {
            using (SolidBrush fill = new SolidBrush(Panel))
            using (Pen border = new Pen(Color.FromArgb(165, color), 1.5f))
            {
                g.FillRectangle(fill, box);
                g.DrawRectangle(border, box);
            }
        }

        private static void DrawVerticalWire(
            Graphics g,
            int y1,
            int y2,
            int x,
            Color color)
        {
            using (Pen pen = new Pen(color, 1.8f))
            {
                g.DrawLine(pen, x, y1, x, y2);
            }
        }

        private static Color SourceColor(
            SyntheticElectricalSource source,
            MissionRenderContext context)
        {
            if (source == null)
                return context.DimPhosphorColor;
            if (!source.CommandedAvailable)
                return Advisory;
            if (source.State == SyntheticElectricalSourceState.Offline)
                return Critical;
            if (source.State == SyntheticElectricalSourceState.Degraded)
                return Warning;
            return source.Conducting ? Healthy : Dead;
        }

        private static Color BusColor(
            SyntheticElectricalBus bus,
            MissionRenderContext context)
        {
            if (bus == null)
            {
                return context.DimPhosphorColor;
            }

            switch (bus.State)
            {
                case SyntheticElectricalBusState.Nominal:
                    return Healthy;

                case SyntheticElectricalBusState.HighLoad:
                    return Advisory;

                case SyntheticElectricalBusState.Overloaded:
                case SyntheticElectricalBusState.Undervoltage:
                    return Critical;

                default:
                    return Dead;
            }
        }

        private static string FormatPercent(double value)
        {
            if (double.IsNaN(value))
            {
                return "--";
            }

            if (double.IsInfinity(value) || value >= 999.0)
            {
                return ">999%";
            }

            return value.ToString("0") + "%";
        }

        private static string FormatDuration(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.0)
            {
                return "--";
            }

            if (seconds < 60.0)
            {
                return seconds.ToString("0") + " S";
            }

            if (seconds < 3600.0)
            {
                return (seconds / 60.0).ToString("0.0") + " MIN";
            }

            return (seconds / 3600.0).ToString("0.0") + " HR";
        }

        private static string SplitWords(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "--";
            }

            string result = value[0].ToString();

            for (int index = 1; index < value.Length; index++)
            {
                char current = value[index];
                char previous = value[index - 1];

                if (char.IsUpper(current) && char.IsLower(previous))
                {
                    result += " ";
                }

                result += current;
            }

            return result.ToUpperInvariant();
        }

        private static void DrawText(
            Graphics g,
            Rectangle bounds,
            string text,
            Font font,
            Color color,
            TextFormatFlags flags)
        {
            TextRenderer.DrawText(
                g,
                text ?? string.Empty,
                font,
                bounds,
                color,
                flags);
        }
    }
}
