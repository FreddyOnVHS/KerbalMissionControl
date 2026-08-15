using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.Engine.SpacecraftSystems;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Rendering.Power
{
    /// <summary>
    /// Build 14.14.2C consolidated POWER 2/2 renderer.
    ///
    /// Design goals:
    /// - fixed two-column layout rather than many cramped dashboard cells
    /// - scalable, paged real-KSP producer inventory
    /// - grouped producer summaries for immediate situational awareness
    /// - current A/B/ESS distribution truth
    /// - live KSP EC truth kept separate from topology and KMC synthetic truth
    /// - short MOCR-style controller evidence instead of diagnostic answers
    ///
    /// The old POWER 2/2 base renderer/overlays remain compiled for now, but
    /// PowerPage no longer calls them. This avoids hidden duplicate rendering.
    /// </summary>
    internal static class PowerDetailConsolidatedRenderer
    {
        private const int SourcesPerPage = 6;

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

        private static readonly Color PanelFill =
            Color.FromArgb(255, 7, 18, 20);

        public static void Draw(
            MissionRenderContext context,
            AnalysisPipelineResult engineering,
            int requestedSourcePage,
            out Rectangle sourcePreviousButton,
            out Rectangle sourceNextButton,
            out int sourcePageCount,
            out int effectiveSourcePage)
        {
            sourcePreviousButton =
                Rectangle.Empty;

            sourceNextButton =
                Rectangle.Empty;

            sourcePageCount =
                1;

            effectiveSourcePage =
                0;

            if (context == null)
            {
                return;
            }

            Graphics g =
                context.Graphics;

            Rectangle content =
                context.ContentBounds;

            Rectangle area =
                new Rectangle(
                    content.Left + 14,
                    content.Top + 66,
                    Math.Max(0, content.Width - 28),
                    Math.Max(0, content.Height - 82));

            using (SolidBrush clear =
                new SolidBrush(
                    Color.FromArgb(255, 4, 15, 18)))
            {
                g.FillRectangle(
                    clear,
                    area);
            }

            DrawText(
                g,
                new Rectangle(
                    area.Left + 10,
                    area.Top - 42,
                    area.Width - 20,
                    34),
                "ELECTRICAL SYSTEMS / DETAIL",
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            if (engineering == null ||
                engineering.Snapshot == null ||
                engineering.Snapshot.Power == null)
            {
                DrawCentered(
                    context,
                    area,
                    "ELECTRICAL ENGINEERING DATA WAITING",
                    context.DimPhosphorColor);

                return;
            }

            var power =
                engineering.Snapshot.Power;

            SyntheticElectricalDistributionModel distribution =
                engineering.Snapshot.SpacecraftSystems != null
                    ? engineering.Snapshot.SpacecraftSystems.ElectricalDistribution
                    : null;

            const int columnGap = 18;

            int leftWidth =
                area.Width * 40 / 100;

            Rectangle left =
                new Rectangle(
                    area.Left,
                    area.Top,
                    leftWidth,
                    area.Height);

            Rectangle right =
                new Rectangle(
                    left.Right + columnGap,
                    area.Top,
                    Math.Max(
                        0,
                        area.Right -
                        left.Right -
                        columnGap),
                    area.Height);

            const int panelGap = 14;

            int sourceHeight =
                left.Height * 66 / 100;

            Rectangle sourcePanel =
                new Rectangle(
                    left.Left,
                    left.Top,
                    left.Width,
                    sourceHeight);

            Rectangle storagePanel =
                new Rectangle(
                    left.Left,
                    sourcePanel.Bottom + panelGap,
                    left.Width,
                    Math.Max(
                        0,
                        left.Bottom -
                        sourcePanel.Bottom -
                        panelGap));

            int busHeight =
                right.Height * 29 / 100;

            /*
             * Build 14.14.4A:
             * Rebalance the lower-right stack so the 60-second trend panel
             * has room for all three evidence rows (A/B voltage, ESS/net EC,
             * samples/window) without changing the overall two-column layout.
             */
            /*
             * Build 14.14.4B:
             * Final right-column balance. Restore enough height for all
             * Native KSP Consumer rows while preserving all three trend rows.
             */
            int actionHeight =
                right.Height * 16 / 100;

            int nativeHeight =
                right.Height * 19 / 100;

            int currentHeight =
                right.Height * 20 / 100;

            Rectangle busPanel =
                new Rectangle(
                    right.Left,
                    right.Top,
                    right.Width,
                    busHeight);

            Rectangle actionPanel =
                new Rectangle(
                    right.Left,
                    busPanel.Bottom + panelGap,
                    right.Width,
                    actionHeight);

            Rectangle nativePanel =
                new Rectangle(
                    right.Left,
                    actionPanel.Bottom + panelGap,
                    right.Width,
                    nativeHeight);

            Rectangle currentPanel =
                new Rectangle(
                    right.Left,
                    nativePanel.Bottom + panelGap,
                    right.Width,
                    currentHeight);

            Rectangle stagePanel =
                new Rectangle(
                    right.Left,
                    currentPanel.Bottom + panelGap,
                    right.Width,
                    Math.Max(
                        0,
                        right.Bottom -
                        currentPanel.Bottom -
                        panelGap));

            DrawSourcePanel(
                context,
                sourcePanel,
                power.Attribution,
                requestedSourcePage,
                out sourcePreviousButton,
                out sourceNextButton,
                out sourcePageCount,
                out effectiveSourcePage);

            DrawLiveStoragePanel(
                context,
                storagePanel,
                power.Diagnostic,
                power.Flow,
                power.ElectricalNetwork != null
                    ? power.ElectricalNetwork.Storage
                    : null);

            DrawBusPanel(
                context,
                busPanel,
                distribution);

            DrawEventHistoryPanel(
                context,
                actionPanel,
                power.DistributionEvents);

            DrawNativeConsumerPanel(
                context,
                nativePanel,
                power.Attribution,
                power.LoadShedding,
                distribution);

            DrawTrendPanel(
                context,
                currentPanel,
                power.DistributionTrend);

            DrawStagePanel(
                context,
                stagePanel,
                power.Diagnostic,
                power.ElectricalNetwork != null
                    ? power.ElectricalNetwork.Storage
                    : null);
        }

        private static void DrawSourcePanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalAttributionModel attribution,
            int requestedPage,
            out Rectangle previousButton,
            out Rectangle nextButton,
            out int pageCount,
            out int effectivePage)
        {
            previousButton =
                Rectangle.Empty;

            nextButton =
                Rectangle.Empty;

            pageCount =
                1;

            effectivePage =
                0;

            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "REAL KSP GENERATION SOURCES");

            List<ElectricalAttributionEntry> producers =
                attribution != null
                    ? attribution.Entries
                        .Where(
                            entry =>
                                entry != null &&
                                entry.Kind ==
                                    ElectricalAttributionKind.Producer)
                        .OrderBy(
                            entry =>
                                string.IsNullOrWhiteSpace(entry.Category)
                                    ? "ZZZ"
                                    : entry.Category)
                        .ThenBy(
                            entry =>
                                string.IsNullOrWhiteSpace(entry.PartTitle)
                                    ? string.Empty
                                    : entry.PartTitle)
                        .ThenBy(
                            entry =>
                                entry.PartId)
                        .ToList()
                    : new List<ElectricalAttributionEntry>();

            pageCount =
                Math.Max(
                    1,
                    (producers.Count +
                     SourcesPerPage - 1) /
                    SourcesPerPage);

            effectivePage =
                Math.Max(
                    0,
                    Math.Min(
                        requestedPage,
                        pageCount - 1));

            int row =
                RowHeight(
                    context);

            int y =
                body.Top;

            int active =
                producers.Count(
                    IsProducing);

            double knownCurrent =
                producers
                    .Where(
                        entry =>
                            entry.CurrentRateKnown)
                    .Sum(
                        entry =>
                            Math.Max(
                                0.0,
                                entry.CurrentRateEcPerSecond));

            double knownMax =
                producers
                    .Where(
                        entry =>
                            entry.MaximumRateKnown)
                    .Sum(
                        entry =>
                            Math.Max(
                                0.0,
                                entry.MaximumRateEcPerSecond));

            y = DrawPair(
                context,
                body,
                y,
                "INSTALLED",
                producers.Count.ToString(),
                "PRODUCING",
                active.ToString(),
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "CURRENT",
                knownCurrent.ToString("0.###") +
                " EC/S",
                "KNOWN MAX",
                knownMax.ToString("0.###") +
                " EC/S",
                context.PhosphorColor);

            List<IGrouping<string, ElectricalAttributionEntry>> groups =
                producers
                    .GroupBy(
                        entry =>
                            string.IsNullOrWhiteSpace(
                                entry.Category)
                                ? "OTHER"
                                : entry.Category)
                    .OrderBy(
                        group => group.Key)
                    .ToList();

            int groupRows =
                Math.Min(
                    4,
                    groups.Count);

            for (int index = 0;
                 index < groupRows;
                 index++)
            {
                IGrouping<string, ElectricalAttributionEntry> group =
                    groups[index];

                int groupActive =
                    group.Count(
                        IsProducing);

                double groupCurrent =
                    group
                        .Where(
                            entry =>
                                entry.CurrentRateKnown)
                        .Sum(
                            entry =>
                                Math.Max(
                                    0.0,
                                    entry.CurrentRateEcPerSecond));

                string summary =
                    group.Count().ToString() +
                    " INST / " +
                    groupActive.ToString() +
                    " PROD / " +
                    groupCurrent.ToString("0.###") +
                    " EC/S";

                y = DrawSingle(
                    context,
                    body,
                    y,
                    group.Key,
                    summary,
                    context.DimPhosphorColor,
                    context.PhosphorColor);
            }

            if (groups.Count > groupRows)
            {
                y = DrawSingle(
                    context,
                    body,
                    y,
                    "GROUPS",
                    "+" +
                    (groups.Count - groupRows).ToString() +
                    " MORE",
                    context.DimPhosphorColor,
                    context.DimPhosphorColor);
            }

            int navigationHeight =
                row + 8;

            Rectangle nav =
                new Rectangle(
                    body.Left,
                    body.Bottom -
                    navigationHeight,
                    body.Width,
                    navigationHeight);

            Rectangle listArea =
                new Rectangle(
                    body.Left,
                    y + 6,
                    body.Width,
                    Math.Max(
                        0,
                        nav.Top -
                        y -
                        10));

            DrawProducerList(
                context,
                listArea,
                producers,
                effectivePage);

            DrawSourceNavigation(
                context,
                nav,
                producers.Count,
                effectivePage,
                pageCount,
                out previousButton,
                out nextButton);
        }

        private static void DrawProducerList(
            MissionRenderContext context,
            Rectangle area,
            List<ElectricalAttributionEntry> producers,
            int page)
        {
            if (area.Width <= 0 ||
                area.Height <= 0)
            {
                return;
            }

            if (producers.Count == 0)
            {
                DrawCentered(
                    context,
                    area,
                    "NO REAL EC PRODUCERS DISCOVERED",
                    context.DimPhosphorColor);

                return;
            }

            int start =
                page *
                SourcesPerPage;

            int count =
                Math.Min(
                    SourcesPerPage,
                    producers.Count -
                    start);

            int rowHeight =
                Math.Max(
                    48,
                    area.Height /
                    SourcesPerPage);

            for (int index = 0;
                 index < count;
                 index++)
            {
                ElectricalAttributionEntry entry =
                    producers[start + index];

                Rectangle row =
                    new Rectangle(
                        area.Left,
                        area.Top +
                        index *
                        rowHeight,
                        area.Width,
                        Math.Min(
                            rowHeight,
                            area.Bottom -
                            (area.Top +
                             index *
                             rowHeight)));

                if (row.Height <= 0)
                {
                    break;
                }

                DrawProducerRow(
                    context,
                    row,
                    start + index + 1,
                    entry);
            }
        }

        private static void DrawProducerRow(
            MissionRenderContext context,
            Rectangle row,
            int sequence,
            ElectricalAttributionEntry entry)
        {
            int small =
                SmallHeight(
                    context);

            int titleHeight =
                Math.Min(
                    row.Height / 2,
                    small * 2 + 6);

            string title =
                sequence.ToString("000") +
                "  " +
                (string.IsNullOrWhiteSpace(
                    entry.PartTitle)
                    ? "PART #" +
                      entry.PartId.ToString()
                    : entry.PartTitle);

            DrawText(
                context.Graphics,
                new Rectangle(
                    row.Left + 4,
                    row.Top + 2,
                    row.Width - 8,
                    titleHeight),
                title,
                context,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoClipping);

            string category =
                string.IsNullOrWhiteSpace(
                    entry.Category)
                    ? "OTHER"
                    : entry.Category;

            string state =
                SourceState(
                    entry);

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

            string detail =
                category +
                "   #" +
                entry.PartId.ToString() +
                "   " +
                state +
                "   " +
                current +
                "/" +
                maximum +
                " EC/S";

            DrawText(
                context.Graphics,
                new Rectangle(
                    row.Left + 4,
                    row.Top +
                    titleHeight,
                    row.Width - 8,
                    Math.Max(
                        0,
                        row.Height -
                        titleHeight -
                        2)),
                detail,
                context,
                SourceStateColor(
                    entry,
                    context),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        55,
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

        private static void DrawSourceNavigation(
            MissionRenderContext context,
            Rectangle nav,
            int producerCount,
            int page,
            int pageCount,
            out Rectangle previousButton,
            out Rectangle nextButton)
        {
            const int buttonWidth = 110;
            const int buttonGap = 10;

            previousButton =
                new Rectangle(
                    nav.Right -
                    buttonWidth * 2 -
                    buttonGap,
                    nav.Top + 3,
                    buttonWidth,
                    Math.Max(
                        0,
                        nav.Height - 6));

            nextButton =
                new Rectangle(
                    nav.Right -
                    buttonWidth,
                    nav.Top + 3,
                    buttonWidth,
                    Math.Max(
                        0,
                        nav.Height - 6));

            int start =
                producerCount == 0
                    ? 0
                    : page *
                      SourcesPerPage +
                      1;

            int end =
                Math.Min(
                    producerCount,
                    (page + 1) *
                    SourcesPerPage);

            DrawText(
                context.Graphics,
                new Rectangle(
                    nav.Left,
                    nav.Top,
                    Math.Max(
                        0,
                        previousButton.Left -
                        nav.Left -
                        10),
                    nav.Height),
                "SRC " +
                start.ToString() +
                "-" +
                end.ToString() +
                " OF " +
                producerCount.ToString() +
                "   PAGE " +
                (page + 1).ToString() +
                "/" +
                pageCount.ToString(),
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawButton(
                context,
                previousButton,
                "< PREV",
                page > 0);

            DrawButton(
                context,
                nextButton,
                "NEXT >",
                page <
                    pageCount - 1);
        }

        private static void DrawLiveStoragePanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalFlowModel flow,
            ElectricalStorageModel topology)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "LIVE EC STORAGE / TOPOLOGY");

            int y =
                body.Top;

            bool live =
                diagnostic != null &&
                diagnostic.TelemetryAvailable;

            y = DrawPair(
                context,
                body,
                y,
                "LIVE EC",
                live
                    ? diagnostic.StoredEc.ToString("0.0") +
                      "/" +
                      diagnostic.CapacityEc.ToString("0.0")
                    : "--",
                "RESERVE",
                live
                    ? diagnostic.ReservePercent.ToString("0.0") +
                      "%"
                    : "--",
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "NET FLOW",
                flow != null &&
                flow.HasMeasuredNetStorageRate
                    ? SignedRate(
                        flow.NetStorageRateEcPerSecond)
                    : "--",
                "ENDURANCE",
                diagnostic != null &&
                diagnostic.HasEndurance
                    ? Duration(
                        diagnostic.EnduranceSeconds)
                    : "--",
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "STORAGE PARTS",
                topology != null
                    ? topology.Parts.Count.ToString()
                    : "--",
                "BRANCHES",
                topology != null
                    ? topology.BranchSections.Count.ToString()
                    : "--",
                context.DimPhosphorColor);

            double lostCapacity =
                topology != null
                    ? topology.NextStageLostCapacityEc
                    : 0.0;

            y = DrawPair(
                context,
                body,
                y,
                "NEXT STG CAP LOSS",
                lostCapacity.ToString("0.0") +
                " EC",
                "LIVE SECTION EC",
                "UNALLOCATED",
                lostCapacity > 0.000001
                    ? Advisory
                    : context.DimPhosphorColor);

            DrawText(
                context.Graphics,
                new Rectangle(
                    body.Left,
                    y + 4,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        y -
                        4)),
                "TOPOLOGY = CAPACITY / STAGING. LIVE EC TOTAL REMAINS THE CURRENT TELEMETRY SOURCE.",
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.Top |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawBusPanel(
            MissionRenderContext context,
            Rectangle panel,
            SyntheticElectricalDistributionModel distribution)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "DISTRIBUTION BUSES / A-B-ESS");

            int rowHeight =
                Math.Max(
                    RowHeight(context) * 2,
                    body.Height / 3);

            DrawBusRow(
                context,
                new Rectangle(
                    body.Left,
                    body.Top,
                    body.Width,
                    rowHeight),
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_A")
                    : null);

            DrawBusRow(
                context,
                new Rectangle(
                    body.Left,
                    body.Top + rowHeight,
                    body.Width,
                    rowHeight),
                distribution != null
                    ? distribution.FindBus("BUS_MAIN_B")
                    : null);

            DrawBusRow(
                context,
                new Rectangle(
                    body.Left,
                    body.Top + rowHeight * 2,
                    body.Width,
                    Math.Max(
                        0,
                        body.Bottom -
                        body.Top -
                        rowHeight * 2)),
                distribution != null
                    ? distribution.FindBus("BUS_ESS")
                    : null);
        }

        private static void DrawBusRow(
            MissionRenderContext context,
            Rectangle row,
            SyntheticElectricalBus bus)
        {
            if (row.Width <= 0 ||
                row.Height <= 0)
            {
                return;
            }

            int line =
                Math.Max(
                    20,
                    row.Height / 2);

            string name =
                bus != null &&
                !string.IsNullOrWhiteSpace(
                    bus.DisplayName)
                    ? bus.DisplayName
                    : "BUS --";

            DrawText(
                context.Graphics,
                new Rectangle(
                    row.Left,
                    row.Top,
                    row.Width,
                    line),
                name +
                "   " +
                (bus != null
                    ? SplitWords(
                        bus.State.ToString())
                    : "UNAVAILABLE") +
                "   " +
                (bus != null
                    ? bus.Voltage.ToString("0.0") +
                      " V"
                    : "--"),
                context,
                BusColor(
                    bus,
                    context),
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

            string loadPercent =
                bus == null ||
                bus.AvailableCurrentAmps <= 0.000001 ||
                IsDead(bus)
                    ? "--"
                    : bus.LoadPercent.ToString("0") +
                      "%";

            string detail =
                "SRC " +
                source +
                "   DEM " +
                (bus != null
                    ? bus.DemandAmps.ToString("0.0") +
                      "/" +
                      bus.AvailableCurrentAmps.ToString("0.0") +
                      " A"
                    : "--") +
                "   LOAD " +
                loadPercent +
                "   AUTO " +
                (bus != null &&
                 bus.ShedDemandAmps > 0.01
                    ? bus.ShedDemandAmps.ToString("0.0") +
                      " A"
                    : "--") +
                "   MAN " +
                (bus != null &&
                 bus.ManualShedDemandAmps > 0.01
                    ? bus.ManualShedDemandAmps.ToString("0.0") +
                      " A"
                    : "--");

            DrawText(
                context.Graphics,
                new Rectangle(
                    row.Left,
                    row.Top + line,
                    row.Width,
                    Math.Max(
                        0,
                        row.Height -
                        line)),
                detail,
                context,
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            using (Pen divider =
                new Pen(
                    Color.FromArgb(
                        55,
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

        private static void DrawEventHistoryPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalDistributionEventHistoryModel history)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "DISTRIBUTION EVENT HISTORY");

            if (history == null ||
                history.Events == null ||
                history.Events.Count == 0)
            {
                DrawCentered(
                    context,
                    body,
                    "NO DISTRIBUTION TRANSITIONS SINCE BASELINE",
                    context.DimPhosphorColor);

                return;
            }

            int visible =
                Math.Min(
                    4,
                    history.Events.Count);

            int rowHeight =
                Math.Max(
                    1,
                    body.Height /
                    visible);

            for (int index = 0;
                 index < visible;
                 index++)
            {
                ElectricalDistributionEventRecord item =
                    history.Events[
                        history.Events.Count -
                        1 -
                        index];

                Rectangle row =
                    new Rectangle(
                        body.Left,
                        body.Top +
                        index * rowHeight,
                        body.Width,
                        Math.Min(
                            rowHeight,
                            body.Bottom -
                            (body.Top +
                             index * rowHeight)));

                if (row.Height <= 0)
                {
                    break;
                }

                int timeWidth =
                    Math.Min(
                        150,
                        row.Width * 14 / 100);

                int busWidth =
                    Math.Min(
                        205,
                        row.Width * 20 / 100);

                DrawText(
                    context.Graphics,
                    new Rectangle(
                        row.Left,
                        row.Top,
                        timeWidth,
                        row.Height),
                    item.TimestampUtc.ToString("HH:mm:ss") +
                    "Z",
                    context,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                DrawText(
                    context.Graphics,
                    new Rectangle(
                        row.Left + timeWidth,
                        row.Top,
                        busWidth,
                        row.Height),
                    ShortBusName(
                        item.BusName,
                        item.BusId),
                    context,
                    EventSeverityColor(
                        item.Severity,
                        context),
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                string evidence =
                    !string.IsNullOrWhiteSpace(
                        item.Message)
                        ? item.Message
                        : item.Code;

                DrawText(
                    context.Graphics,
                    new Rectangle(
                        row.Left +
                        timeWidth +
                        busWidth,
                        row.Top,
                        Math.Max(
                            0,
                            row.Width -
                            timeWidth -
                            busWidth),
                        row.Height),
                    evidence,
                    context,
                    context.PhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                if (index < visible - 1)
                {
                    using (Pen divider =
                        new Pen(
                            Color.FromArgb(
                                45,
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
            }
        }

        private static void DrawNativeConsumerPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalAttributionModel attribution,
            ElectricalLoadSheddingModel shedding,
            SyntheticElectricalDistributionModel distribution)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "NATIVE KSP CONSUMERS / POTENTIAL");

            int y =
                body.Top;

            y = DrawPair(
                context,
                body,
                y,
                "DISCOVERED",
                attribution != null
                    ? attribution.ConsumerCount.ToString()
                    : "--",
                "KNOWN CURRENT",
                attribution != null
                    ? attribution.KnownCurrentConsumptionEcPerSecond
                        .ToString("0.###") +
                      " EC/S"
                    : "--",
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "DECLARED MAX",
                attribution != null
                    ? attribution.DeclaredMaximumConsumptionEcPerSecond
                        .ToString("0.###") +
                      " EC/S"
                    : "--",
                "NATIVE CAND",
                shedding != null
                    ? shedding.CandidateCount.ToString()
                    : "--",
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "POTENTIAL MAX",
                shedding != null
                    ? shedding.PotentialMaximumRecoverableEcPerSecond
                        .ToString("0.###") +
                      " EC/S"
                    : "--",
                "KMC LOAD (SEP)",
                distribution != null
                    ? distribution.KmcOwnedActiveLoadEcPerSecond
                        .ToString("0.000") +
                      " EC/S"
                    : "--",
                Advisory);

            /*
             * Build 14.14.2C2:
             * This fixed-height panel has room for exactly three data rows.
             * Do not render a fourth footer row below them. The native/KMC
             * domain distinction is carried by the existing "KMC LOAD (SEP)"
             * field instead.
             */
        }

        private static void DrawTrendPanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalDistributionTrendHistoryModel trend)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "60 SEC ELECTRICAL TREND");

            if (trend == null ||
                trend.Samples == null ||
                trend.Samples.Count == 0 ||
                trend.Latest == null)
            {
                DrawCentered(
                    context,
                    body,
                    "TREND DATA BUILDING",
                    context.DimPhosphorColor);

                return;
            }

            ElectricalDistributionTrendSample oldest =
                trend.Oldest;

            ElectricalDistributionTrendSample latest =
                trend.Latest;

            string window =
                trend.WindowSeconds >= 1.0
                    ? trend.WindowSeconds.ToString("0") +
                      " S"
                    : "<1 S";

            int y =
                body.Top;

            y = DrawPair(
                context,
                body,
                y,
                "MAIN A V",
                TrendValue(
                    oldest.MainAVoltage,
                    latest.MainAVoltage,
                    "0.0",
                    " V"),
                "MAIN B V",
                TrendValue(
                    oldest.MainBVoltage,
                    latest.MainBVoltage,
                    "0.0",
                    " V"),
                context.PhosphorColor);

            y = DrawPair(
                context,
                body,
                y,
                "ESS V",
                TrendValue(
                    oldest.EssentialVoltage,
                    latest.EssentialVoltage,
                    "0.0",
                    " V"),
                "NET EC",
                TrendNetFlow(
                    oldest,
                    latest),
                context.PhosphorColor);

            DrawPair(
                context,
                body,
                y,
                "SAMPLES",
                trend.Count.ToString(),
                "WINDOW",
                window,
                context.DimPhosphorColor);
        }

        private static string TrendValue(
            double oldest,
            double latest,
            string format,
            string suffix)
        {
            return
                oldest.ToString(format) +
                " -> " +
                latest.ToString(format) +
                suffix;
        }

        private static string TrendNetFlow(
            ElectricalDistributionTrendSample oldest,
            ElectricalDistributionTrendSample latest)
        {
            if (oldest == null ||
                latest == null ||
                !oldest.NetFlowKnown ||
                !latest.NetFlowKnown)
            {
                return "--";
            }

            return
                SignedRate(
                    oldest.NetFlowEcPerSecond) +
                " -> " +
                SignedRate(
                    latest.NetFlowEcPerSecond);
        }

        private static string ShortBusName(
            string busName,
            string busId)
        {
            string value =
                !string.IsNullOrWhiteSpace(busName)
                    ? busName
                    : busId;

            if (string.IsNullOrWhiteSpace(value))
            {
                return "BUS";
            }

            return
                value
                    .Replace(
                        "MAIN BUS ",
                        "MAIN ")
                    .Replace(
                        "ESSENTIAL BUS",
                        "ESS")
                    .Replace(
                        "BUS_",
                        string.Empty)
                    .Replace(
                        '_',
                        ' ')
                    .Trim()
                    .ToUpperInvariant();
        }

        private static Color EventSeverityColor(
            ElectricalEventSeverity severity,
            MissionRenderContext context)
        {
            switch (severity)
            {
                case ElectricalEventSeverity.Info:
                    return Healthy;

                case ElectricalEventSeverity.Advisory:
                    return Advisory;

                case ElectricalEventSeverity.Warning:
                    return Warning;

                case ElectricalEventSeverity.Critical:
                    return Critical;

                default:
                    return context.DimPhosphorColor;
            }
        }

        private static void DrawStagePanel(
            MissionRenderContext context,
            Rectangle panel,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalStorageModel topology)
        {
            Rectangle body =
                BeginPanel(
                    context,
                    panel,
                    "NEXT STAGE STORAGE CAPACITY RISK");

            bool live =
                diagnostic != null &&
                diagnostic.TelemetryAvailable;

            bool loss =
                topology != null &&
                topology.HasStorageLossOnNextStage;

            bool all =
                topology != null &&
                topology.LosesAllStorageOnNextStage;

            double lostCapacity =
                topology != null
                    ? topology.NextStageLostCapacityEc
                    : 0.0;

            string state =
                all
                    ? "CRITICAL / ALL STORAGE LOST"
                    : loss
                        ? "CAUTION / PARTIAL CAPACITY LOSS"
                        : "NO STORAGE CAPACITY HAZARD";

            Color color =
                all
                    ? Critical
                    : loss
                        ? Warning
                        : Healthy;

            int y =
                body.Top;

            y = DrawSingle(
                context,
                body,
                y,
                "STATE",
                state,
                context.DimPhosphorColor,
                color);

            y = DrawPair(
                context,
                body,
                y,
                "CAP LOSS",
                lostCapacity.ToString("0.0") +
                " EC",
                "LIVE TOTAL",
                live
                    ? diagnostic.StoredEc.ToString("0.0") +
                      "/" +
                      diagnostic.CapacityEc.ToString("0.0") +
                      " EC"
                    : "--",
                color);

            string allocation =
                !loss
                    ? "NOT REQUIRED"
                    : all
                        ? "ALL STORAGE"
                        : "SECTION UNKNOWN";

            DrawPair(
                context,
                body,
                y,
                "LIVE ALLOCATION",
                allocation,
                "RESERVE",
                live
                    ? diagnostic.ReservePercent.ToString("0.0") +
                      "%"
                    : "--",
                color);
        }

        private static Rectangle BeginPanel(
            MissionRenderContext context,
            Rectangle panel,
            string title)
        {
            int small =
                SmallHeight(
                    context);

            int titleHeight =
                small + 12;

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
                context.Graphics.FillRectangle(
                    fill,
                    panel);

                context.Graphics.DrawRectangle(
                    border,
                    panel);

                DrawText(
                    context.Graphics,
                    new Rectangle(
                        panel.Left + 12,
                        panel.Top + 5,
                        panel.Width - 24,
                        small + 4),
                    title,
                    context,
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                context.Graphics.DrawLine(
                    border,
                    panel.Left + 10,
                    panel.Top + titleHeight,
                    panel.Right - 10,
                    panel.Top + titleHeight);
            }

            return
                new Rectangle(
                    panel.Left + 14,
                    panel.Top + titleHeight + 7,
                    Math.Max(
                        0,
                        panel.Width - 28),
                    Math.Max(
                        0,
                        panel.Height -
                        titleHeight -
                        18));
        }

        private static int DrawPair(
            MissionRenderContext context,
            Rectangle body,
            int y,
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue,
            Color valueColor)
        {
            int row =
                RowHeight(
                    context);

            if (y + row >
                body.Bottom)
            {
                return y;
            }

            int gap =
                14;

            int half =
                (body.Width - gap) /
                2;

            DrawField(
                context,
                new Rectangle(
                    body.Left,
                    y,
                    half,
                    row),
                leftLabel,
                leftValue,
                valueColor);

            DrawField(
                context,
                new Rectangle(
                    body.Left + half + gap,
                    y,
                    half,
                    row),
                rightLabel,
                rightValue,
                valueColor);

            return
                y + row;
        }

        private static int DrawSingle(
            MissionRenderContext context,
            Rectangle body,
            int y,
            string label,
            string value,
            Color labelColor,
            Color valueColor)
        {
            int row =
                RowHeight(
                    context);

            if (y + row >
                body.Bottom)
            {
                return y;
            }

            int labelWidth =
                Math.Max(
                    115,
                    body.Width * 22 / 100);

            DrawText(
                context.Graphics,
                new Rectangle(
                    body.Left,
                    y,
                    labelWidth,
                    row),
                label,
                context,
                labelColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                context.Graphics,
                new Rectangle(
                    body.Left + labelWidth,
                    y,
                    Math.Max(
                        0,
                        body.Width -
                        labelWidth),
                    row),
                value,
                context,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            return
                y + row;
        }

        private static void DrawField(
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            Color valueColor)
        {
            int labelWidth =
                Math.Max(
                    90,
                    bounds.Width * 42 / 100);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        0,
                        labelWidth - 4),
                    bounds.Height),
                label,
                context,
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawText(
                context.Graphics,
                new Rectangle(
                    bounds.Left + labelWidth,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width -
                        labelWidth),
                    bounds.Height),
                value,
                context,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawButton(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            bool enabled)
        {
            Color color =
                enabled
                    ? context.PhosphorColor
                    : context.DimPhosphorColor;

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        enabled
                            ? 180
                            : 75,
                        color),
                    enabled
                        ? 1.5f
                        : 1.0f))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);
            }

            DrawText(
                context.Graphics,
                bounds,
                text,
                context,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
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
                return "UNKNOWN";
            }

            if (!entry.Enabled)
            {
                return "DISABLED";
            }

            if (IsProducing(entry))
            {
                return "PRODUCING";
            }

            if (entry.ActiveStateKnown &&
                !entry.Active)
            {
                return "INACTIVE";
            }

            if (entry.CurrentRateKnown)
            {
                return "NO OUTPUT";
            }

            if (entry.ActiveStateKnown &&
                entry.Active)
            {
                return "ACTIVE";
            }

            if (entry.MaximumRateKnown)
            {
                return "AVAILABLE";
            }

            return "STATE UNKNOWN";
        }

        private static Color SourceStateColor(
            ElectricalAttributionEntry entry,
            MissionRenderContext context)
        {
            if (entry == null)
            {
                return Advisory;
            }

            if (IsProducing(entry))
            {
                return Healthy;
            }

            if (!entry.Enabled ||
                (entry.ActiveStateKnown &&
                 !entry.Active))
            {
                return context.DimPhosphorColor;
            }

            if (!entry.CurrentRateKnown &&
                !entry.ActiveStateKnown)
            {
                return Advisory;
            }

            return context.PhosphorColor;
        }

        private static bool IsDead(
            SyntheticElectricalBus bus)
        {
            return
                bus == null ||
                bus.State ==
                    SyntheticElectricalBusState.Unpowered ||
                bus.State ==
                    SyntheticElectricalBusState.Failed;
        }

        private static bool IsDegraded(
            SyntheticElectricalBus bus)
        {
            if (bus == null)
            {
                return true;
            }

            return
                bus.State ==
                    SyntheticElectricalBusState.HighLoad ||
                bus.State ==
                    SyntheticElectricalBusState.Overloaded ||
                bus.State ==
                    SyntheticElectricalBusState.Undervoltage ||
                bus.State ==
                    SyntheticElectricalBusState.Failed ||
                bus.State ==
                    SyntheticElectricalBusState.Unpowered;
        }

        private static bool IsBatteryFed(
            SyntheticElectricalBus bus)
        {
            return
                bus != null &&
                !string.IsNullOrWhiteSpace(
                    bus.ActiveSourceId) &&
                bus.ActiveSourceId.IndexOf(
                    "BAT_",
                    StringComparison.OrdinalIgnoreCase) >= 0;
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
                    return Warning;

                case SyntheticElectricalBusState.Undervoltage:
                case SyntheticElectricalBusState.Failed:
                    return Critical;

                case SyntheticElectricalBusState.Unpowered:
                    return Dead;

                default:
                    return context.DimPhosphorColor;
            }
        }

        private static int SmallHeight(
            MissionRenderContext context)
        {
            return
                Math.Max(
                    1,
                    TextRenderer.MeasureText(
                        context.Graphics,
                        "Ag",
                        context.SmallFont,
                        new Size(
                            int.MaxValue,
                            int.MaxValue),
                        TextFormatFlags.NoPadding)
                    .Height);
        }

        private static int RowHeight(
            MissionRenderContext context)
        {
            return
                SmallHeight(context) +
                12;
        }

        private static string SignedRate(
            double value)
        {
            return
                (value > 0.000001
                    ? "+"
                    : string.Empty) +
                value.ToString("0.###") +
                " EC/S";
        }

        private static string Duration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds) ||
                seconds < 0.0)
            {
                return "--";
            }

            TimeSpan time =
                TimeSpan.FromSeconds(
                    seconds);

            if (time.TotalHours >= 1.0)
            {
                return
                    ((int)time.TotalHours).ToString("00") +
                    ":" +
                    time.Minutes.ToString("00") +
                    ":" +
                    time.Seconds.ToString("00");
            }

            return
                time.Minutes.ToString("00") +
                ":" +
                time.Seconds.ToString("00");
        }

        private static string SplitWords(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
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
                    !char.IsUpper(value[index - 1]))
                {
                    builder.Append(' ');
                }

                builder.Append(current);
            }

            return
                builder.ToString()
                    .ToUpperInvariant();
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            Color color)
        {
            DrawText(
                context.Graphics,
                bounds,
                text,
                context,
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
            MissionRenderContext context,
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
                string.IsNullOrWhiteSpace(text)
                    ? "---"
                    : text.Trim().ToUpperInvariant(),
                context.SmallFont,
                bounds,
                color,
                flags);
        }
    }
}
