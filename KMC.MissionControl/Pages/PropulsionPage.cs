using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Engine.Propulsion;
using KMC.MissionControl.Cards;
using KMC.MissionControl.Cards.Propulsion;
using KMC.MissionControl.Controls;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Propulsion;

namespace KMC.MissionControl.Pages
{
    public sealed class PropulsionPage :
        IMissionPage,
        IMissionPageCanvasProvider,
        IMessageFilter
    {
        private const int WmLeftButtonDown =
            0x0201;

        private const int EngineRowsPerBank =
            8;

        private const int FeedRowsPerBank =
            7;

        private readonly EngineClusterCard
            _engineClusterCard =
                new EngineClusterCard();

        private readonly PropulsionPerformanceCard
            _performanceCard =
                new PropulsionPerformanceCard();

        private readonly PropellantFlowCard
            _propellantFlowCard =
                new PropellantFlowCard();

        private readonly PropulsionFooterCard
            _footerCard =
                new PropulsionFooterCard();

        private readonly PropulsionCardChangeTracker
            _changeTracker =
                new PropulsionCardChangeTracker();

        private long _lastTopologyRevision =
            long.MinValue;

        private int _lastStage =
            int.MinValue;

        private int _subpage = 1;
        private int _engineBank;
        private int _feedBank;

        private Rectangle _overviewTab;
        private Rectangle _engineTab;
        private Rectangle _feedTab;
        private Rectangle _previousBank;
        private Rectangle _nextBank;

        public PropulsionPage()
        {
            Application.AddMessageFilter(
                this);
        }

        public string Name
        {
            get { return "PROPULSION"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                /*
                 * Match ORBIT: use the live CRT viewport.
                 */
                return Size.Empty;
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile.DenseEngineering;
            }
        }

        public bool PreFilterMessage(
            ref Message message)
        {
            if (message.Msg !=
                WmLeftButtonDown)
            {
                return false;
            }

            MissionDisplay display =
                Control.FromHandle(
                    message.HWnd) as
                MissionDisplay;

            if (display == null ||
                !string.Equals(
                    display.ScreenTitle,
                    "PROP DATA",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            long packed =
                message.LParam.ToInt64();

            Point clientPoint =
                new Point(
                    (short)(packed & 0xFFFF),
                    (short)((packed >> 16) & 0xFFFF));

            PointF virtualPoint;

            if (!display.TryClientToVirtual(
                    clientPoint,
                    out virtualPoint))
            {
                return false;
            }

            Point point =
                Point.Round(
                    virtualPoint);

            int previousPage =
                _subpage;

            if (_overviewTab.Contains(point))
            {
                _subpage = 1;
            }
            else if (_engineTab.Contains(point))
            {
                _subpage = 2;
            }
            else if (_feedTab.Contains(point))
            {
                _subpage = 3;
            }
            else if (_previousBank.Contains(point))
            {
                if (_subpage == 2)
                {
                    _engineBank =
                        Math.Max(
                            0,
                            _engineBank - 1);
                }
                else if (_subpage == 3)
                {
                    _feedBank =
                        Math.Max(
                            0,
                            _feedBank - 1);
                }
            }
            else if (_nextBank.Contains(point))
            {
                if (_subpage == 2)
                {
                    _engineBank++;
                }
                else if (_subpage == 3)
                {
                    _feedBank++;
                }
            }
            else
            {
                return false;
            }

            if (previousPage !=
                _subpage)
            {
                _previousBank =
                    Rectangle.Empty;

                _nextBank =
                    Rectangle.Empty;
            }

            display.RequestRender();

            return false;
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout pageLayout =
                new MissionPageLayout(
                    context);

            pageLayout.DrawHeader(
                Name,
                "CH 04");

            DrawSubpageTabs(
                context);

            PropulsionRenderGraph graph =
                PropulsionGraphStore.GetCurrent();

            int liveCurrentStage =
                telemetry.CurrentStage;

            PropulsionAnalysis analysis =
                graph != null
                    ? PropulsionAnalysisCache
                        .GetOrBuild(
                            graph,
                            liveCurrentStage)
                    : null;

            PropulsionModel engineering =
                GetCompatibleEngineeringModel(
                    graph);

            PropulsionPageRenderModel model =
                new PropulsionPageRenderModel
                {
                    Graph =
                        graph,

                    Analysis =
                        analysis,

                    Telemetry =
                        telemetry,

                    Engineering =
                        engineering
                };

            if (_subpage == 2)
            {
                DrawEngineChannelsPage(
                    context,
                    model);

                return;
            }

            if (_subpage == 3)
            {
                DrawFeedDetailPage(
                    context,
                    model);

                return;
            }

            DrawOverviewPage(
                context,
                telemetry,
                graph,
                liveCurrentStage,
                model);
        }

        private void DrawOverviewPage(
            MissionRenderContext context,
            MissionTelemetry telemetry,
            PropulsionRenderGraph graph,
            int liveCurrentStage,
            PropulsionPageRenderModel model)
        {
            Rectangle working =
                new Rectangle(
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 78,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 98);

            MissionCardLayout layout =
                MissionCardLayoutEngine
                    .CalculatePropulsion(
                        working);

            bool topologyChanged =
                graph == null
                    ? _lastTopologyRevision !=
                        long.MinValue
                    : graph.TopologyRevision !=
                        _lastTopologyRevision ||
                      liveCurrentStage !=
                        _lastStage;

            if (topologyChanged)
            {
                _changeTracker.Reset();

                MarkAllCardsDirty(
                    CardDirtyState.Static |
                    CardDirtyState.Telemetry);
            }

            PropulsionCardChangeSet changes =
                _changeTracker.Evaluate(
                    telemetry,
                    graph);

            if (changes.EngineClusterChanged)
            {
                _engineClusterCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.PerformanceChanged)
            {
                _performanceCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.FlowChanged)
            {
                _propellantFlowCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            if (changes.FooterChanged)
            {
                _footerCard.MarkDirty(
                    CardDirtyState.Telemetry);
            }

            _performanceCard.MarkDirty(
                CardDirtyState.Telemetry);

            _propellantFlowCard.MarkDirty(
                CardDirtyState.Telemetry);

            _footerCard.MarkDirty(
                CardDirtyState.Telemetry);

            _lastTopologyRevision =
                graph != null
                    ? graph.TopologyRevision
                    : long.MinValue;

            _lastStage =
                graph != null
                    ? liveCurrentStage
                    : int.MinValue;

            _engineClusterCard.Bounds =
                layout.EngineCluster;

            _performanceCard.Bounds =
                layout.Performance;

            _propellantFlowCard.Bounds =
                layout.PropellantFlow;

            _footerCard.Bounds =
                layout.Footer;

            _engineClusterCard.Draw(
                context,
                model);

            _performanceCard.Draw(
                context,
                model);

            _propellantFlowCard.Draw(
                context,
                model);

            _footerCard.Draw(
                context,
                model);
        }

        private void DrawEngineChannelsPage(
            MissionRenderContext context,
            PropulsionPageRenderModel model)
        {
            Rectangle content =
                GetSubpageContent(
                    context);

            DrawPageTitle(
                context,
                content,
                "ENGINE CHANNELS / INDIVIDUAL PROPULSION HEALTH");

            PropulsionStatusModel status =
                model != null &&
                model.Engineering != null
                    ? model.Engineering.Status
                    : null;

            if (status == null)
            {
                DrawCentered(
                    context,
                    content,
                    "AWAITING ENGINEERING MODEL");

                return;
            }

            Rectangle health =
                new Rectangle(
                    content.Left,
                    content.Top + 46,
                    content.Width,
                    112);

            DrawEngineHealthSummary(
                context,
                health,
                status);

            Rectangle table =
                new Rectangle(
                    content.Left,
                    health.Bottom + 12,
                    content.Width,
                    Math.Max(
                        1,
                        content.Bottom -
                        health.Bottom -
                        62));

            int total =
                status.EngineChannels.Count;

            int bankCount =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        total /
                        (double)EngineRowsPerBank));

            if (_engineBank >=
                bankCount)
            {
                _engineBank =
                    bankCount - 1;
            }

            DrawEngineTable(
                context,
                table,
                status,
                _engineBank);

            DrawBankNavigation(
                context,
                content,
                _engineBank,
                bankCount,
                "ENGINES");
        }

        private void DrawFeedDetailPage(
            MissionRenderContext context,
            PropulsionPageRenderModel model)
        {
            Rectangle content =
                GetSubpageContent(
                    context);

            DrawPageTitle(
                context,
                content,
                "PROPELLANT FEED / STAGE DETAIL");

            if (model == null ||
                model.Engineering == null ||
                model.Engineering.Feed == null ||
                !model.Engineering.Feed.Available)
            {
                DrawCentered(
                    context,
                    content,
                    "ENGINE FEED MODEL UNAVAILABLE");

                return;
            }

            PropulsionFeedModel feed =
                model.Engineering.Feed;

            Rectangle summary =
                new Rectangle(
                    content.Left,
                    content.Top + 46,
                    content.Width,
                    112);

            DrawFeedSummary(
                context,
                summary,
                feed);

            int schematicHeight =
                Math.Max(
                    150,
                    (int)(
                        content.Height *
                        0.34));

            Rectangle schematic =
                new Rectangle(
                    content.Left,
                    summary.Bottom + 12,
                    content.Width,
                    schematicHeight);

            DrawPanelFrame(
                context,
                schematic,
                "SYSTEM FLOW / TOPOLOGY SNAPSHOT");

            if (model.Analysis != null &&
                model.Analysis.SystemModel != null)
            {
                PropulsionDisplayRenderer.DrawSystemFlow(
                    context.Graphics,
                    Rectangle.Inflate(
                        schematic,
                        -12,
                        -34),
                    model.Analysis.SystemModel,
                    model.Telemetry,
                    context.SmallFont,
                    context.SmallFont,
                    context.PhosphorColor,
                    context.DimPhosphorColor);
            }
            else
            {
                DrawCentered(
                    context,
                    schematic,
                    "AWAITING PROPULSION FLOW GRAPH");
            }

            Rectangle table =
                new Rectangle(
                    content.Left,
                    schematic.Bottom + 12,
                    content.Width,
                    Math.Max(
                        1,
                        content.Bottom -
                        schematic.Bottom -
                        62));

            int total =
                feed.Engines.Count;

            int bankCount =
                Math.Max(
                    1,
                    (int)Math.Ceiling(
                        total /
                        (double)FeedRowsPerBank));

            if (_feedBank >=
                bankCount)
            {
                _feedBank =
                    bankCount - 1;
            }

            DrawFeedTable(
                context,
                table,
                feed,
                _feedBank);

            DrawBankNavigation(
                context,
                content,
                _feedBank,
                bankCount,
                "FEEDS");
        }

        private void DrawSubpageTabs(
            MissionRenderContext context)
        {
            Rectangle content =
                context.ContentBounds;

            const int gap = 8;
            const int tabWidth = 180;
            const int tabHeight = 36;

            int totalWidth =
                tabWidth * 3 +
                gap * 2;

            int x =
                content.Right -
                totalWidth;

            /*
             * Keep PROP internal navigation below the MissionPageLayout
             * header/CH 04 line. The previous top+8 placement collided with
             * the page header and forced text ellipsis.
             */
            int y =
                content.Top + 44;

            _overviewTab =
                new Rectangle(
                    x,
                    y,
                    tabWidth,
                    tabHeight);

            _engineTab =
                new Rectangle(
                    _overviewTab.Right + gap,
                    y,
                    tabWidth,
                    tabHeight);

            _feedTab =
                new Rectangle(
                    _engineTab.Right + gap,
                    y,
                    tabWidth,
                    tabHeight);

            DrawTab(
                context,
                _overviewTab,
                "1/3 OVERVIEW",
                _subpage == 1);

            DrawTab(
                context,
                _engineTab,
                "2/3 ENGINES",
                _subpage == 2);

            DrawTab(
                context,
                _feedTab,
                "3/3 FEED",
                _subpage == 3);
        }

        private static void DrawTab(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            bool active)
        {
            Color color =
                active
                    ? context.PhosphorColor
                    : context.DimPhosphorColor;

            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        active
                            ? 190
                            : 105,
                        color),
                    active
                        ? 1.6f
                        : 1.0f))
            {
                context.Graphics.DrawRectangle(
                    border,
                    bounds);
            }

            TextRenderer.DrawText(
                context.Graphics,
                text,
                context.SmallFont,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static Rectangle GetSubpageContent(
            MissionRenderContext context)
        {
            return
                new Rectangle(
                    context.ContentBounds.Left + 18,
                    context.ContentBounds.Top + 92,
                    context.ContentBounds.Width - 36,
                    context.ContentBounds.Height - 106);
        }

        private static void DrawPageTitle(
            MissionRenderContext context,
            Rectangle content,
            string title)
        {
            TextRenderer.DrawText(
                context.Graphics,
                title,
                context.SmallFont,
                new Rectangle(
                    content.Left,
                    content.Top,
                    content.Width,
                    34),
                context.PhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        110,
                        context.DimPhosphorColor)))
            {
                context.Graphics.DrawLine(
                    pen,
                    content.Left,
                    content.Top + 36,
                    content.Right,
                    content.Top + 36);
            }
        }

        private static void DrawEngineHealthSummary(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionStatusModel status)
        {
            string healthTitle =
                status != null &&
                status.ThrustDataDisagreement &&
                status.ThrustDiscrepancyKnown
                    ? "CHANNEL HEALTH / THRUST DATA DISAGREE / DELTA " +
                      FormatSignedThrust(
                          status.ThrustDiscrepancy)
                    : "CHANNEL HEALTH";

            DrawPanelFrame(
                context,
                bounds,
                healthTitle);

            string[] labels =
            {
                "TOTAL",
                "NORMAL",
                "ADVISORY",
                "FAULT",
                "UNKNOWN"
            };

            string[] values =
            {
                status.EngineChannels.Count.ToString("00"),
                status.ChannelNormalCount.ToString("00"),
                status.ChannelAdvisoryCount.ToString("00"),
                status.ChannelFaultCount.ToString("00"),
                status.ChannelUnknownCount.ToString("00")
            };

            DrawSummaryCells(
                context,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 34,
                    bounds.Width - 20,
                    68),
                labels,
                values);
        }

        private static void DrawFeedSummary(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionFeedModel feed)
        {
            DrawPanelFrame(
                context,
                bounds,
                "FEED STATUS / TOPOLOGY SNAPSHOT");

            string[] labels =
            {
                "CURRENT FED",
                "DEG / LOST",
                "NEXT RETAIN",
                "NEXT LOST",
                "NEXT FED",
                "PUMP A",
                "PUMP B"
            };

            string[] values =
            {
                feed.CurrentFeedAvailableEngineCount +
                "/" +
                feed.EngineCount,

                feed.CurrentFeedDegradedEngineCount
                    .ToString() +
                " / " +
                feed.CurrentFeedLimitedEngineCount
                    .ToString(),

                feed.NextStageRetainedEngineCount
                    .ToString(),

                feed.NextStageLostEngineCount
                    .ToString(),

                feed.NextStageRetainedFeedAvailableCount +
                "/" +
                feed.NextStageRetainedEngineCount,

                ShortPump(
                    feed.PumpAState),

                ShortPump(
                    feed.PumpBState)
            };

            DrawSummaryCells(
                context,
                new Rectangle(
                    bounds.Left + 10,
                    bounds.Top + 34,
                    bounds.Width - 20,
                    68),
                labels,
                values);
        }

        private static void DrawSummaryCells(
            MissionRenderContext context,
            Rectangle bounds,
            string[] labels,
            string[] values)
        {
            int count =
                Math.Min(
                    labels.Length,
                    values.Length);

            if (count <= 0)
            {
                return;
            }

            int cellWidth =
                Math.Max(
                    1,
                    bounds.Width /
                    count);

            for (int index = 0;
                 index < count;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        bounds.Left +
                        index * cellWidth,
                        bounds.Top,
                        index ==
                            count - 1
                                ? bounds.Right -
                                  (bounds.Left +
                                   index * cellWidth)
                                : cellWidth,
                        bounds.Height);

                if (index > 0)
                {
                    using (Pen pen =
                        new Pen(
                            Color.FromArgb(
                                65,
                                context.DimPhosphorColor)))
                    {
                        context.Graphics.DrawLine(
                            pen,
                            cell.Left,
                            cell.Top + 3,
                            cell.Left,
                            cell.Bottom - 3);
                    }
                }

                const int textRowHeight = 34;

                TextRenderer.DrawText(
                    context.Graphics,
                    labels[index],
                    context.SmallFont,
                    new Rectangle(
                        cell.Left + 3,
                        cell.Top,
                        cell.Width - 6,
                        textRowHeight),
                    context.DimPhosphorColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

                TextRenderer.DrawText(
                    context.Graphics,
                    values[index],
                    context.SmallFont,
                    new Rectangle(
                        cell.Left + 3,
                        cell.Top + textRowHeight,
                        cell.Width - 6,
                        textRowHeight),
                    context.PhosphorColor,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);
            }
        }

        private static void DrawEngineTable(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionStatusModel status,
            int bank)
        {
            DrawPanelFrame(
                context,
                bounds,
                "ENGINE CHANNEL BANK");

            int headerY =
                bounds.Top + 30;

            int headerHeight =
                30;

            Rectangle inner =
                new Rectangle(
                    bounds.Left + 8,
                    headerY,
                    bounds.Width - 16,
                    Math.Max(
                        1,
                        bounds.Bottom -
                        headerY - 8));

            int[] widths =
                CalculateColumns(
                    inner.Width,
                    new int[]
                    {
                        8, 14, 18, 7, 17, 13, 15, 8
                    });

            string[] headers =
            {
                "ENG",
                "PART ID",
                "ENGINE",
                "STG",
                "STATE",
                "FEED",
                "THRUST CUR/MAX",
                "NEXT"
            };

            DrawTableHeader(
                context,
                inner.Left,
                inner.Top,
                headerHeight,
                widths,
                headers);

            int start =
                bank *
                EngineRowsPerBank;

            int count =
                Math.Min(
                    EngineRowsPerBank,
                    Math.Max(
                        0,
                        status.EngineChannels.Count -
                        start));

            int rowTop =
                inner.Top +
                headerHeight;

            int rowHeight =
                Math.Max(
                    34,
                    Math.Min(
                        56,
                        (inner.Bottom -
                         rowTop) /
                        Math.Max(
                            1,
                            EngineRowsPerBank)));

            for (int row = 0;
                 row < count;
                 row++)
            {
                PropulsionEngineChannelModel channel =
                    status.EngineChannels[
                        start + row];

                DrawEngineRow(
                    context,
                    rowTop +
                    row * rowHeight,
                    rowHeight,
                    inner.Left,
                    widths,
                    start + row + 1,
                    channel,
                    row % 2 == 1);
            }

            if (count == 0)
            {
                DrawCentered(
                    context,
                    inner,
                    "NO ENGINE CHANNELS");
            }
        }

        private static void DrawEngineRow(
            MissionRenderContext context,
            int y,
            int height,
            int left,
            int[] widths,
            int ordinal,
            PropulsionEngineChannelModel channel,
            bool alternate)
        {
            Color color =
                SeverityColor(
                    context,
                    channel != null
                        ? channel.Severity
                        : PropulsionSeverity.Unknown);

            if (alternate)
            {
                using (SolidBrush fill =
                    new SolidBrush(
                        Color.FromArgb(
                            14,
                            context.DimPhosphorColor)))
                {
                    context.Graphics.FillRectangle(
                        fill,
                        new Rectangle(
                            left,
                            y,
                            Sum(widths),
                            height));
                }
            }

            string feed =
                channel != null &&
                channel.FeedStateKnown
                    ? ShortFeed(
                        channel.CurrentFeedStatus)
                    : "UNKNOWN";

            string thrust =
                channel != null &&
                channel.CurrentThrustKnown
                    ? channel.CurrentThrust
                        .ToString("0.0") +
                      " / " +
                      channel.MaximumThrust
                        .ToString("0.0") +
                      " kN"
                    : "---";

            string next =
                channel != null
                    ? channel.SurvivesNextStage
                        ? "RETAIN"
                        : "SEP"
                    : "---";

            string[] values =
            {
                ordinal.ToString("00"),
                channel != null
                    ? channel.PartId.ToString()
                    : "---",
                channel != null
                    ? channel.PartTitle
                    : "---",
                channel != null
                    ? channel.ActivationStage.ToString("00")
                    : "--",
                channel != null
                    ? BreakCondition(
                        channel.Condition.ToString())
                    : "UNKNOWN",
                feed,
                thrust,
                next
            };

            DrawTableRow(
                context,
                left,
                y,
                height,
                widths,
                values,
                color);
        }

        private static void DrawFeedTable(
            MissionRenderContext context,
            Rectangle bounds,
            PropulsionFeedModel feed,
            int bank)
        {
            DrawPanelFrame(
                context,
                bounds,
                "ENGINE FEED CHANNELS");

            int headerY =
                bounds.Top + 30;

            int headerHeight =
                30;

            Rectangle inner =
                new Rectangle(
                    bounds.Left + 8,
                    headerY,
                    bounds.Width - 16,
                    Math.Max(
                        1,
                        bounds.Bottom -
                        headerY - 8));

            int[] widths =
                CalculateColumns(
                    inner.Width,
                    new int[]
                    {
                        8, 15, 20, 15, 11, 13, 18
                    });

            string[] headers =
            {
                "ENG",
                "PART ID",
                "ENGINE",
                "CURRENT FEED",
                "READY",
                "NEXT FEED",
                "REQUIREMENTS"
            };

            DrawTableHeader(
                context,
                inner.Left,
                inner.Top,
                headerHeight,
                widths,
                headers);

            int start =
                bank *
                FeedRowsPerBank;

            int count =
                Math.Min(
                    FeedRowsPerBank,
                    Math.Max(
                        0,
                        feed.Engines.Count -
                        start));

            int rowTop =
                inner.Top +
                headerHeight;

            int rowHeight =
                Math.Max(
                    38,
                    Math.Min(
                        60,
                        (inner.Bottom -
                         rowTop) /
                        Math.Max(
                            1,
                            FeedRowsPerBank)));

            for (int row = 0;
                 row < count;
                 row++)
            {
                PropulsionEngineFeedModel engine =
                    feed.Engines[
                        start + row];

                string[] values =
                {
                    (start + row + 1)
                        .ToString("00"),

                    engine.PartId
                        .ToString(),

                    engine.PartTitle,

                    ShortFeed(
                        engine.CurrentFeedStatus),

                    engine.ReadyForThrust
                        ? "YES"
                        : "NO",

                    ShortFeed(
                        engine.NextStageFeedStatus),

                    BuildRequirementSummary(
                        engine)
                };

                Color color =
                    engine.CurrentFeedStatus ==
                        PropulsionFeedStatus.Available
                        ? context.PhosphorColor
                        : Color.FromArgb(
                            255,
                            220,
                            185,
                            92);

                DrawTableRow(
                    context,
                    inner.Left,
                    rowTop +
                    row * rowHeight,
                    rowHeight,
                    widths,
                    values,
                    color);
            }

            if (count == 0)
            {
                DrawCentered(
                    context,
                    inner,
                    "NO ENGINE FEED CHANNELS");
            }
        }

        private void DrawBankNavigation(
            MissionRenderContext context,
            Rectangle content,
            int bank,
            int bankCount,
            string label)
        {
            int buttonWidth =
                118;

            int y =
                content.Bottom -
                42;

            _previousBank =
                new Rectangle(
                    content.Left,
                    y,
                    buttonWidth,
                    32);

            _nextBank =
                new Rectangle(
                    content.Right -
                    buttonWidth,
                    y,
                    buttonWidth,
                    32);

            bool previousEnabled =
                bank > 0;

            bool nextEnabled =
                bank <
                bankCount - 1;

            DrawNavigationButton(
                context,
                _previousBank,
                "< PREV",
                previousEnabled);

            DrawNavigationButton(
                context,
                _nextBank,
                "NEXT >",
                nextEnabled);

            string status =
                label +
                "  BANK " +
                (bank + 1) +
                " / " +
                bankCount;

            TextRenderer.DrawText(
                context.Graphics,
                status,
                context.SmallFont,
                new Rectangle(
                    _previousBank.Right + 12,
                    y,
                    Math.Max(
                        1,
                        _nextBank.Left -
                        _previousBank.Right -
                        24),
                    32),
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawNavigationButton(
            MissionRenderContext context,
            Rectangle bounds,
            string text,
            bool enabled)
        {
            Color color =
                enabled
                    ? context.PhosphorColor
                    : context.DimPhosphorColor;

            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        enabled
                            ? 155
                            : 65,
                        color)))
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds);
            }

            TextRenderer.DrawText(
                context.Graphics,
                text,
                context.SmallFont,
                bounds,
                color,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawTableHeader(
            MissionRenderContext context,
            int left,
            int top,
            int height,
            int[] widths,
            string[] headers)
        {
            int x =
                left;

            for (int index = 0;
                 index < widths.Length;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        x,
                        top,
                        widths[index],
                        height);

                using (Pen pen =
                    new Pen(
                        Color.FromArgb(
                            80,
                            context.DimPhosphorColor)))
                {
                    context.Graphics.DrawRectangle(
                        pen,
                        cell);
                }

                TextRenderer.DrawText(
                    context.Graphics,
                    headers[index],
                    context.SmallFont,
                    Rectangle.Inflate(
                        cell,
                        -4,
                        -2),
                    context.DimPhosphorColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis);

                x +=
                    widths[index];
            }
        }

        private static void DrawTableRow(
            MissionRenderContext context,
            int left,
            int top,
            int height,
            int[] widths,
            string[] values,
            Color valueColor)
        {
            int x =
                left;

            for (int index = 0;
                 index < widths.Length &&
                 index < values.Length;
                 index++)
            {
                Rectangle cell =
                    new Rectangle(
                        x,
                        top,
                        widths[index],
                        height);

                using (Pen pen =
                    new Pen(
                        Color.FromArgb(
                            45,
                            context.DimPhosphorColor)))
                {
                    context.Graphics.DrawRectangle(
                        pen,
                        cell);
                }

                TextRenderer.DrawText(
                    context.Graphics,
                    values[index] ?? string.Empty,
                    context.SmallFont,
                    Rectangle.Inflate(
                        cell,
                        -5,
                        -3),
                    valueColor,
                    TextFormatFlags.Left |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.EndEllipsis |
                    TextFormatFlags.SingleLine);

                x +=
                    widths[index];
            }
        }

        private static int[] CalculateColumns(
            int totalWidth,
            int[] weights)
        {
            int weightTotal =
                0;

            for (int index = 0;
                 index < weights.Length;
                 index++)
            {
                weightTotal +=
                    Math.Max(
                        1,
                        weights[index]);
            }

            int[] result =
                new int[
                    weights.Length];

            int used =
                0;

            for (int index = 0;
                 index < weights.Length;
                 index++)
            {
                if (index ==
                    weights.Length - 1)
                {
                    result[index] =
                        Math.Max(
                            1,
                            totalWidth -
                            used);
                }
                else
                {
                    result[index] =
                        Math.Max(
                            1,
                            totalWidth *
                            Math.Max(
                                1,
                                weights[index]) /
                            weightTotal);

                    used +=
                        result[index];
                }
            }

            return result;
        }

        private static string BuildRequirementSummary(
            PropulsionEngineFeedModel engine)
        {
            if (engine == null ||
                engine.Requirements.Count == 0)
            {
                return "NONE";
            }

            StringBuilder builder =
                new StringBuilder();

            for (int index = 0;
                 index < engine.Requirements.Count &&
                 index < 3;
                 index++)
            {
                if (index > 0)
                {
                    builder.Append("  ");
                }

                PropulsionRequirementFeedModel requirement =
                    engine.Requirements[index];

                builder.Append(
                    ShortResource(
                        requirement.ResourceName));

                builder.Append(':');

                builder.Append(
                    ShortFeed(
                        requirement.CurrentStatus));
            }

            if (engine.Requirements.Count > 3)
            {
                builder.Append(" +");
                builder.Append(
                    engine.Requirements.Count - 3);
            }

            return
                builder.ToString();
        }

        private static string ShortResource(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return "RES";
            }

            if (string.Equals(
                    value,
                    "LiquidFuel",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "LF";
            }

            if (string.Equals(
                    value,
                    "Oxidizer",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "OX";
            }

            if (string.Equals(
                    value,
                    "SolidFuel",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "SF";
            }

            if (string.Equals(
                    value,
                    "MonoPropellant",
                    StringComparison.OrdinalIgnoreCase))
            {
                return "MONO";
            }

            return
                value.Length <= 6
                    ? value.ToUpperInvariant()
                    : value.Substring(
                        0,
                        6).ToUpperInvariant();
        }

        private static string ShortFeed(
            PropulsionFeedStatus status)
        {
            switch (status)
            {
                case PropulsionFeedStatus.Available:
                    return "AVAILABLE";

                case PropulsionFeedStatus.PressureLow:
                    return "PRESS LOW";

                case PropulsionFeedStatus.Depleted:
                    return "DEPLETED";

                case PropulsionFeedStatus.FlowDisabled:
                    return "FLOW OFF";

                case PropulsionFeedStatus.SourceStateUnknown:
                    return "UNKNOWN";

                case PropulsionFeedStatus.NoReachableSource:
                    return "NO SOURCE";

                default:
                    return "UNKNOWN";
            }
        }

        private static string ShortPump(
            PropulsionFeedPumpState state)
        {
            switch (state)
            {
                case PropulsionFeedPumpState.Nominal:
                    return "NOMINAL";

                case PropulsionFeedPumpState.Degraded:
                    return "DEGRADED";

                case PropulsionFeedPumpState.Failed:
                    return "FAILED";

                case PropulsionFeedPumpState.Unpowered:
                    return "UNPOWERED";

                default:
                    return "UNKNOWN";
            }
        }

        private static string BreakCondition(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return "---";
            }

            StringBuilder result =
                new StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char c =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(c) &&
                    char.IsLower(
                        value[index - 1]))
                {
                    result.Append(' ');
                }

                result.Append(c);
            }

            return
                result.ToString()
                    .ToUpperInvariant();
        }

        private static string FormatSignedThrust(
            double value)
        {
            string sign =
                value > 0.0
                    ? "+"
                    : string.Empty;

            return
                sign +
                value.ToString("0.0") +
                " kN";
        }

        private static Color SeverityColor(
            MissionRenderContext context,
            PropulsionSeverity severity)
        {
            switch (severity)
            {
                case PropulsionSeverity.Critical:
                    return
                        Color.FromArgb(
                            255,
                            255,
                            82,
                            72);

                case PropulsionSeverity.Warning:
                    return
                        Color.FromArgb(
                            255,
                            255,
                            196,
                            72);

                case PropulsionSeverity.Advisory:
                    return
                        Color.FromArgb(
                            255,
                            220,
                            185,
                            92);

                case PropulsionSeverity.Normal:
                    return
                        context.PhosphorColor;

                default:
                    return
                        context.DimPhosphorColor;
            }
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (Pen pen =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor)))
            {
                context.Graphics.DrawRectangle(
                    pen,
                    bounds);
            }

            TextRenderer.DrawText(
                context.Graphics,
                title,
                context.SmallFont,
                new Rectangle(
                    bounds.Left + 9,
                    bounds.Top + 4,
                    bounds.Width - 18,
                    24),
                context.DimPhosphorColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawCentered(
            MissionRenderContext context,
            Rectangle bounds,
            string text)
        {
            TextRenderer.DrawText(
                context.Graphics,
                text,
                context.SmallFont,
                bounds,
                context.DimPhosphorColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static int Sum(
            int[] values)
        {
            int total =
                0;

            for (int index = 0;
                 index < values.Length;
                 index++)
            {
                total +=
                    values[index];
            }

            return total;
        }

        private static PropulsionModel
            GetCompatibleEngineeringModel(
                PropulsionRenderGraph graph)
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore.TryGetLatest(
                    out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Propulsion == null)
            {
                return null;
            }

            PropulsionModel propulsion =
                result.Snapshot.Propulsion;

            if (graph == null ||
                propulsion.Topology == null)
            {
                return propulsion;
            }

            if (propulsion.Topology.TopologyRevision !=
                graph.TopologyRevision)
            {
                return null;
            }

            return propulsion;
        }

        private void MarkAllCardsDirty(
            CardDirtyState dirtyState)
        {
            _engineClusterCard.MarkDirty(
                dirtyState);

            _performanceCard.MarkDirty(
                dirtyState);

            _propellantFlowCard.MarkDirty(
                dirtyState);

            _footerCard.MarkDirty(
                dirtyState);
        }
    }
}
