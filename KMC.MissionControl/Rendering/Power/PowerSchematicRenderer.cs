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
            int summaryY = area.Bottom - 154;

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
                distribution.FindSwitch("CONT_GEN_A"),
                "GEN A",
                context);

            DrawContactor(
                g,
                new Point(batABox.Left + batABox.Width / 2, deviceY + 24),
                distribution.FindSwitch("CONT_BAT_A"),
                "BAT A",
                context);

            DrawContactor(
                g,
                new Point(genBBox.Left + genBBox.Width / 2, deviceY + 24),
                distribution.FindSwitch("CONT_GEN_B"),
                "GEN B",
                context);

            DrawContactor(
                g,
                new Point(batBBox.Left + batBBox.Width / 2, deviceY + 24),
                distribution.FindSwitch("CONT_BAT_B"),
                "BAT B",
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

            /*
             * Draw transfer selectors after the source-merge conductors.
             * Their opaque panel fill masks the feeder inside the selector
             * rectangle, making the switch appear electrically in-series:
             * conductor -> selector box -> conductor.
             */
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
                new Rectangle(left, feedY - 36, width, 34),
                "BUS TIE / ESSENTIAL FEEDS",
                context);

            int essWidth = Math.Min(820, width * 52 / 100);
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
                distribution,
                feedA,
                "ESS A",
                context);

            DrawFeedPath(
                g,
                mainBBox,
                new Point(feedBX, feedY + 28),
                essBox,
                distribution,
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

            DrawLoadBranch(g, distribution, "GUID_A", aLeft + 0 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, essBox, context);
            DrawLoadBranch(g, distribution, "COMM_A", aLeft + 1 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, essBox, context);
            DrawLoadBranch(g, distribution, "PUMP_A", aLeft + 2 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainABox, essBox, context);
            DrawLoadBranch(g, distribution, "FLIGHT_COMPUTER", aLeft + 3 * (loadWidth + loadGap), loadY, loadWidth, loadH, essBox, Rectangle.Empty, context);
            DrawLoadBranch(g, distribution, "GUID_B", aLeft + 4 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, essBox, context);
            DrawLoadBranch(g, distribution, "COMM_B", aLeft + 5 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, essBox, context);
            DrawLoadBranch(g, distribution, "PUMP_B", aLeft + 6 * (loadWidth + loadGap), loadY, loadWidth, loadH, mainBBox, essBox, context);

            DrawRealEcSummary(
                g,
                new Rectangle(left, summaryY, width, 102),
                engineering,
                context);

            DrawFooter(
                g,
                new Rectangle(left, area.Bottom - 42, width, 34),
                distribution,
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
                    : source.State == SyntheticElectricalSourceState.Unknown
                        ? "REAL STATE UNKNOWN"
                        : source.State == SyntheticElectricalSourceState.Offline
                            ? "OFFLINE"
                            : source.Kind == SyntheticElectricalSourceKind.Battery &&
                              source.Supplementing &&
                              source.Conducting
                                ? "ACTIVE / SUPPLEMENTING"
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
            string percent =
                bus == null ||
                bus.State == SyntheticElectricalBusState.Unpowered ||
                bus.State == SyntheticElectricalBusState.Failed ||
                bus.AvailableCurrentAmps <= 0.000001
                    ? "--"
                    : FormatPercent(bus.LoadPercent);
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

            string automaticShed =
                bus != null &&
                bus.ShedDemandAmps > 0.01
                    ? "   AUTO " +
                      bus.ShedDemandAmps.ToString("0.0") +
                      " A"
                    : string.Empty;

            string manualShed =
                bus != null &&
                bus.ManualShedDemandAmps > 0.01
                    ? "   MAN " +
                      bus.ManualShedDemandAmps.ToString("0.0") +
                      " A"
                    : string.Empty;

            DrawText(g, new Rectangle(box.Left + 14, box.Top + 82, box.Width - 28, 32),
                "SOURCE " + activeSource + "   DEMAND " + load + "   LOAD " + percent +
                automaticShed + manualShed,
                context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private static void DrawContactor(
            Graphics g,
            Point center,
            SyntheticElectricalSwitch item,
            string label,
            MissionRenderContext context)
        {
            bool commanded =
                item != null &&
                item.CommandedClosed;

            bool indicated =
                item != null &&
                item.IndicatedClosed;

            bool conducting =
                item != null &&
                item.Conducting;

            int radius = 8;

            Color color =
                conducting
                    ? Healthy
                    : indicated
                        ? Advisory
                        : Dead;

            using (Pen pen = new Pen(color, 2.0f))
            {
                g.DrawEllipse(
                    pen,
                    center.X - radius,
                    center.Y - radius,
                    radius * 2,
                    radius * 2);

                g.DrawLine(
                    pen,
                    center.X,
                    center.Y - 26,
                    center.X,
                    center.Y - radius);

                g.DrawLine(
                    pen,
                    center.X,
                    center.Y + radius,
                    center.X,
                    center.Y + 26);

                if (indicated)
                {
                    g.DrawLine(
                        pen,
                        center.X - 5,
                        center.Y,
                        center.X + 5,
                        center.Y);
                }
                else
                {
                    g.DrawLine(
                        pen,
                        center.X - 5,
                        center.Y + 4,
                        center.X + 5,
                        center.Y - 5);
                }
            }

            Rectangle labelBox =
                new Rectangle(
                    center.X - 190,
                    center.Y + 30,
                    380,
                    34);

            using (SolidBrush labelBackground =
                new SolidBrush(Panel))
            {
                g.FillRectangle(
                    labelBackground,
                    labelBox);
            }

            string evidence =
                label +
                "   CMD " +
                (commanded ? "CL" : "OP") +
                "   IND " +
                (indicated ? "CL" : "OP");

            DrawText(
                g,
                labelBox,
                evidence,
                context.SmallFont,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawTransferSelector(
            Graphics g,
            Point center,
            SyntheticElectricalSwitch transfer,
            SyntheticElectricalBus bus,
            string label,
            MissionRenderContext context)
        {
            bool closed = transfer != null && transfer.IndicatedClosed;
            Color color = closed && bus != null && !string.IsNullOrWhiteSpace(bus.ActiveSourceId)
                ? Healthy
                : Advisory;

            Rectangle box =
                new Rectangle(
                    center.X - 280,
                    center.Y - 34,
                    560,
                    76);

            using (SolidBrush fill =
                new SolidBrush(Panel))
            {
                g.FillRectangle(
                    fill,
                    box);
            }

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
                label + "   AUTO   CMD " +
                (transfer != null && transfer.CommandedClosed ? "CL" : "OP") +
                "   IND " +
                (closed ? "CL" : "OP"),
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
            SyntheticElectricalDistributionModel distribution,
            SyntheticElectricalSource feed,
            string label,
            MissionRenderContext context)
        {
            SyntheticElectricalSwitch feedSwitch =
                feed != null
                    ? distribution.FindSwitch(
                        feed.ContactorId)
                    : null;

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

            DrawContactor(g, device, feedSwitch, label, context);
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
            Rectangle avoidBox,
            MissionRenderContext context)
        {
            SyntheticElectricalLoad load = FindLoad(distribution, id);
            SyntheticElectricalSwitch breaker =
                load != null ? distribution.FindSwitch(load.BreakerId) : null;
            bool commanded =
                load != null &&
                load.CommandedOn;

            bool indicatedClosed =
                breaker != null
                    ? breaker.IndicatedClosed
                    : commanded;

            bool conducting =
                breaker != null
                    ? breaker.Conducting
                    : commanded;

            SyntheticElectricalBus upstreamBus =
                load != null
                    ? distribution.FindBus(
                        load.BusId)
                    : null;

            bool upstreamEnergized =
                upstreamBus != null &&
                upstreamBus.State !=
                    SyntheticElectricalBusState.Unpowered &&
                upstreamBus.State !=
                    SyntheticElectricalBusState.Failed &&
                upstreamBus.Voltage >
                    0.000001;

            bool energized =
                conducting &&
                upstreamEnergized;

            Color color =
                energized
                    ? Healthy
                    : Dead;

            Rectangle box = new Rectangle(x, y, width, height);
            int center = box.Left + box.Width / 2;
            int parentCenter = Math.Max(parentBus.Left + 8, Math.Min(parentBus.Right - 8, center));

            const int avoidClearance = 24;

            bool branchTouchesAvoidZone =
                !avoidBox.IsEmpty &&
                (parentCenter >=
                    avoidBox.Left - avoidClearance &&
                 parentCenter <=
                    avoidBox.Right + avoidClearance);

            bool routeAroundAvoidBox =
                branchTouchesAvoidZone &&
                parentBus.Bottom <
                    avoidBox.Bottom &&
                y - 18 >
                    avoidBox.Top;

            int breakerX =
                parentCenter;

            int breakerY =
                parentBus.Bottom +
                Math.Max(
                    24,
                    (y - 18 - parentBus.Bottom) / 2);

            using (Pen wire = new Pen(color, 1.6f))
            {
                if (routeAroundAvoidBox)
                {
                    bool routeLeft =
                        center <
                            avoidBox.Left +
                            avoidBox.Width / 2;

                    int routeX =
                        routeLeft
                            ? avoidBox.Left -
                              avoidClearance
                            : avoidBox.Right +
                              avoidClearance;

                    /*
                     * Leave the parent bus and jog sideways immediately.
                     * The long vertical leg therefore stays completely clear
                     * of the ESS box and its border instead of riding an edge.
                     */
                    int topRouteY =
                        parentBus.Bottom +
                        12;

                    breakerX =
                        routeX;

                    breakerY =
                        topRouteY +
                        Math.Max(
                            24,
                            (y - 18 - topRouteY) / 2);

                    g.DrawLine(
                        wire,
                        parentCenter,
                        parentBus.Bottom,
                        parentCenter,
                        topRouteY);

                    g.DrawLine(
                        wire,
                        parentCenter,
                        topRouteY,
                        routeX,
                        topRouteY);

                    g.DrawLine(
                        wire,
                        routeX,
                        topRouteY,
                        routeX,
                        y - 18);

                    g.DrawLine(
                        wire,
                        routeX,
                        y - 18,
                        center,
                        y - 18);

                    g.DrawLine(
                        wire,
                        center,
                        y - 18,
                        center,
                        y);
                }
                else
                {
                    g.DrawLine(wire, parentCenter, parentBus.Bottom, parentCenter, y - 18);
                    g.DrawLine(wire, parentCenter, y - 18, center, y - 18);
                    g.DrawLine(wire, center, y - 18, center, y);
                }
            }

            /*
             * Build 14.13.3A:
             * Show the physical load breaker in-series on the six Main Bus
             * branch feeders marked on the POWER one-line. The Primary Flight
             * Computer retains its compact ESS breaker evidence in the load
             * box because the ESS-to-load vertical space is intentionally
             * tight on POWER 1/2.
             */
            if (!string.Equals(
                    id,
                    "FLIGHT_COMPUTER",
                    StringComparison.Ordinal))
            {
                DrawLoadBreaker(
                    g,
                    new Point(
                        breakerX,
                        breakerY),
                    breaker,
                    id,
                    context);
            }

            DrawBox(g, box, color);

            string name = load != null ? load.DisplayName : id;
            string demand = load != null ? load.DemandAmps.ToString("0.0") + " A" : "--";
            string state = load != null
                ? "CMD " + (commanded ? "ON" : "OFF") +
                  " / IND " + (indicatedClosed ? "CLOSED" : "OPEN")
                : "--";
            string priority = load != null ? "PRI " + load.Priority.ToString() : "--";

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 8, box.Width - 24, 34), name, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 46, box.Width - 24, 32), state, context.SmallFont, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawText(g, new Rectangle(box.Left + 12, box.Top + 84, box.Width - 24, 30), demand + "  " + priority, context.SmallFont, context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// Compact one-line breaker symbol for individual major-load feeders.
        ///
        /// CMD and IND are intentionally shown separately:
        /// - CMD = commanded mechanical position
        /// - IND = operator-facing indicated position
        /// - symbol color/conduction remains actual electrical-path truth
        ///
        /// A breaker can therefore read CMD CL / IND CL while the downstream
        /// load is unpowered because the upstream bus itself is dead.
        /// </summary>
        private static void DrawLoadBreaker(
            Graphics g,
            Point center,
            SyntheticElectricalSwitch breaker,
            string loadId,
            MissionRenderContext context)
        {
            bool commanded =
                breaker != null &&
                breaker.CommandedClosed;

            bool indicated =
                breaker != null &&
                breaker.IndicatedClosed;

            bool conducting =
                breaker != null &&
                breaker.Conducting;

            Color color =
                conducting
                    ? Healthy
                    : indicated
                        ? Advisory
                        : Dead;

            const int radius = 7;

            using (SolidBrush background =
                new SolidBrush(Panel))
            {
                g.FillEllipse(
                    background,
                    center.X - radius - 2,
                    center.Y - radius - 2,
                    radius * 2 + 4,
                    radius * 2 + 4);
            }

            using (Pen pen =
                new Pen(
                    color,
                    1.8f))
            {
                g.DrawEllipse(
                    pen,
                    center.X - radius,
                    center.Y - radius,
                    radius * 2,
                    radius * 2);

                if (indicated)
                {
                    g.DrawLine(
                        pen,
                        center.X - 5,
                        center.Y,
                        center.X + 5,
                        center.Y);
                }
                else
                {
                    g.DrawLine(
                        pen,
                        center.X - 5,
                        center.Y + 4,
                        center.X + 5,
                        center.Y - 5);
                }
            }

            /*
             * 14.13.3A1:
             * Use a compact stacked evidence block instead of left/right text.
             * Six adjacent feeders do not have enough horizontal room for
             * side-by-side CMD/IND labels at the mission-display font size.
             */
            /*
             * 14.13.3A3:
             * 86 px was too narrow for the mission-display SmallFont at the
             * live render scale; TextRenderer clipped the final stroke of CL.
             * Give the evidence block enough horizontal breathing room.
             */
            const int evidenceWidth = 112;
            const int evidenceLineHeight = 22;
            const int evidenceOffsetX = 20;
            const int evidenceInnerPad = 6;

            bool placeEvidenceLeft =
                string.Equals(
                    loadId,
                    "PUMP_A",
                    StringComparison.Ordinal);

            int evidenceX =
                placeEvidenceLeft
                    ? center.X -
                        evidenceOffsetX -
                        evidenceWidth
                    : center.X +
                        evidenceOffsetX;

            Rectangle evidenceBox =
                new Rectangle(
                    evidenceX,
                    center.Y -
                        evidenceLineHeight,
                    evidenceWidth,
                    evidenceLineHeight * 2);

            Rectangle commandBox =
                new Rectangle(
                    evidenceBox.Left +
                        evidenceInnerPad,
                    evidenceBox.Top,
                    evidenceBox.Width -
                        evidenceInnerPad * 2,
                    evidenceLineHeight);

            Rectangle indicationBox =
                new Rectangle(
                    evidenceBox.Left +
                        evidenceInnerPad,
                    evidenceBox.Top +
                        evidenceLineHeight,
                    evidenceBox.Width -
                        evidenceInnerPad * 2,
                    evidenceLineHeight);

            using (SolidBrush labelBackground =
                new SolidBrush(Panel))
            {
                g.FillRectangle(
                    labelBackground,
                    evidenceBox);
            }

            /*
             * 14.13.3A4:
             * The breaker evidence labels were still clipping the final glyph
             * stroke (CL looked like CI) because TextRenderer was using
             * NoPadding in a very tight evidence line box. For these compact
             * breaker labels we want stable legibility more than ultra-tight
             * edge alignment, so allow normal glyph padding and disable
             * clipping on the text itself.
             */
            TextFormatFlags evidenceAlignment =
                (placeEvidenceLeft
                    ? TextFormatFlags.Right
                    : TextFormatFlags.Left) |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoClipping;

            DrawText(
                g,
                commandBox,
                "CMD " +
                    (commanded
                        ? "CL"
                        : "OP"),
                context.SmallFont,
                color,
                evidenceAlignment);

            DrawText(
                g,
                indicationBox,
                "IND " +
                    (indicated
                        ? "CL"
                        : "OP"),
                context.SmallFont,
                color,
                evidenceAlignment);
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

            DrawText(g, new Rectangle(box.Left + 16, box.Top + 2, box.Width - 32, 32),
                "REAL KSP ELECTRICCHARGE / OBSERVED TELEMETRY",
                context.SmallFont, context.PhosphorColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            int col = (box.Width - 32) / 4;
            DrawValueCell(g, new Rectangle(box.Left + 16, box.Top + 34, col, 64), "STORAGE", storage, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col, box.Top + 34, col, 64), "RESERVE", reserve, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col * 2, box.Top + 34, col, 64), "NET FLOW", net, context);
            DrawValueCell(g, new Rectangle(box.Left + 16 + col * 3, box.Top + 34, col, 64), "ENDURANCE", endurance, context);
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

            DrawText(g, new Rectangle(box.Left, box.Top + 32, box.Width, 32), value, context.SmallFont, context.PhosphorColor,
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
            SyntheticElectricalDistributionModel distribution,
            MissionRenderContext context)
        {
            string ownedLoad =
                distribution != null
                    ? distribution.KmcOwnedActiveLoadEcPerSecond
                        .ToString("0.000") +
                      " EC/S"
                    : "--";

            DrawText(g, box,
                "SWITCHED DISTRIBUTION / P3 AUTO-SHED / GENERATOR PRIMARY / BATTERY AUTO-TRANSFER" +
                " / KMC LOAD CMD " + ownedLoad,
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
            if (source.State == SyntheticElectricalSourceState.Unknown)
                return context.DimPhosphorColor;
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
