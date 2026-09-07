using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;
using KMC.MissionControl.Controls;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Power;
using System.Windows.Forms;

namespace KMC.MissionControl.Pages
{
    /// <summary>
    /// Build 14.11.2 EECOM POWER redesign foundation.
    ///
    /// POWER 1/2 is now a performance-first top-to-bottom one-line schematic.
    /// The legacy POWER renderer is intentionally not called. Page 2/2 is
    /// visually reserved for the later detail/analysis page.
    /// </summary>
    public sealed class PowerPage :
        IMissionPage,
        IMissionPageCanvasProvider,
        IMessageFilter
    {
        // POWER RESERVED NAV RAIL
        private const int NavRailWidth = 220;
        private const int NavRailGap = 18;

        private const int WmLeftButtonDown =
            0x0201;

        private int _subpage = 1;

        private Rectangle _oneLineTab;
        private Rectangle _breakerTab;
        private Rectangle _detailTab;

        private int _sourceInventoryPage;
        private int _sourceInventoryPageCount = 1;
        private Rectangle _sourcePreviousButton;
        private Rectangle _sourceNextButton;

        public PowerPage()
        {
            /*
             * Build 14.13.4:
             * MainForm constructs the POWER page during navigation setup even
             * when another page is selected. Starting the lease sender here
             * therefore does not make real EC loading dependent on POWER being
             * visible.
             */
            ElectricalLoadLeaseSender.EnsureStarted();

            /*
             * Build 14.14.1:
             * Match PROP's proven internal-page navigation mechanism.
             */
            Application.AddMessageFilter(
                this);
        }

        public string Name
        {
            get { return "POWER"; }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                /*
                 * Keep a bounded high-resolution logical canvas. 2400 x 900 has
                 * nearly the same pixel count as 1920 x 1080, but matches
                 * KMC's wide CRT much better so POWER fills the display
                 * without returning to the oversized responsive bitmap.
                 */
                return new Size(
                    3000,
                    1250);
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return MissionPageContentProfile.DenseEngineering;
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
                    "POWER DATA",
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

            int previous =
                _subpage;

            if (_oneLineTab.Contains(
                    point))
            {
                _subpage = 1;
            }
            else if (_breakerTab.Contains(
                         point))
            {
                _subpage = 2;
            }
            else if (_detailTab.Contains(
                         point))
            {
                _subpage = 3;
            }
            else if (_subpage == 3 &&
                     _sourcePreviousButton.Contains(
                         point) &&
                     _sourceInventoryPage > 0)
            {
                _sourceInventoryPage--;
                display.RequestRender();
                return false;
            }
            else if (_subpage == 3 &&
                     _sourceNextButton.Contains(
                         point) &&
                     _sourceInventoryPage <
                         _sourceInventoryPageCount - 1)
            {
                _sourceInventoryPage++;
                display.RequestRender();
                return false;
            }
            else
            {
                return false;
            }

            if (previous !=
                _subpage)
            {
                display.RequestRender();
            }

            return false;
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            AnalysisPipelineResult engineering;

            EngineeringSnapshotStore.TryGetLatest(
                out engineering);

            Rectangle pageBounds =
                new Rectangle(
                    context.ContentBounds.Left,
                    context.ContentBounds.Top,
                    Math.Max(
                        1,
                        context.ContentBounds.Width -
                        NavRailWidth -
                        NavRailGap),
                    context.ContentBounds.Height);

            MissionRenderContext pageContext =
                new MissionRenderContext(
                    context.Graphics,
                    pageBounds,
                    context.LargeFont,
                    context.SmallFont,
                    context.PhosphorColor,
                    context.DimPhosphorColor,
                    context.VirtualCanvasSize);

            if (_subpage == 2)
            {
                PowerBreakerPanelRenderer.Draw(
                    pageContext,
                    engineering);

                DrawDistributionEventHeader(
                    pageContext,
                    engineering);
            }
            else if (_subpage == 3)
            {
                /*
                 * Build 14.14.2C:
                 * Render POWER 2/2 once using the consolidated two-column
                 * layout. The previous base-renderer + overlay stack is no
                 * longer called, eliminating hidden duplicate drawing work.
                 */
                int effectivePage;

                PowerDetailConsolidatedRenderer.Draw(
                    pageContext,
                    engineering,
                    _sourceInventoryPage,
                    out _sourcePreviousButton,
                    out _sourceNextButton,
                    out _sourceInventoryPageCount,
                    out effectivePage);

                _sourceInventoryPage =
                    effectivePage;

                /*
                 * Build 14.14.3:
                 * Compact latest A/B/ESS transition evidence in unused header
                 * space. This does not alter the consolidated panel geometry.
                 */
                DrawDistributionEventHeader(
                    pageContext,
                    engineering);
            }
            else
            {
                PowerSchematicRenderer.Draw(
                    pageContext,
                    telemetry,
                    engineering);
            }

            DrawSubpageTabs(
                context);
        }

        private static void DrawDistributionEventHeader(
            MissionRenderContext context,
            AnalysisPipelineResult engineering)
        {
            if (context == null)
            {
                return;
            }

            Rectangle content =
                context.ContentBounds;

            Rectangle box =
                new Rectangle(
                    content.Left + 720,
                    content.Top + 24,
                    Math.Max(
                        0,
                        content.Width - 1130),
                    32);

            if (box.Width <= 0)
            {
                return;
            }

            ElectricalDistributionEventHistoryModel history =
                engineering != null &&
                engineering.Snapshot != null &&
                engineering.Snapshot.Power != null
                    ? engineering.Snapshot.Power.DistributionEvents
                    : null;

            ElectricalDistributionEventRecord latest =
                history != null
                    ? history.Latest
                    : null;

            string text;
            Color color;

            if (latest == null)
            {
                text =
                    "DIST EVT 00 / BASELINE";

                color =
                    context.DimPhosphorColor;
            }
            else
            {
                text =
                    "DIST EVT " +
                    history.Count.ToString("00") +
                    "  " +
                    latest.TimestampUtc.ToString("HH:mm:ss") +
                    "Z  " +
                    (latest.Code ?? "---");

                if (!string.IsNullOrWhiteSpace(
                        latest.Message))
                {
                    text +=
                        "  [" +
                        latest.Message +
                        "]";
                }

                color =
                    DistributionEventColor(
                        latest.Severity,
                        context);
            }

            TextRenderer.DrawText(
                context.Graphics,
                text.ToUpperInvariant(),
                context.SmallFont,
                box,
                color,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private static Color DistributionEventColor(
            ElectricalEventSeverity severity,
            MissionRenderContext context)
        {
            switch (severity)
            {
                case ElectricalEventSeverity.Info:
                    return Color.FromArgb(112, 202, 154);

                case ElectricalEventSeverity.Advisory:
                    return Color.FromArgb(232, 188, 84);

                case ElectricalEventSeverity.Warning:
                    return Color.FromArgb(236, 142, 66);

                case ElectricalEventSeverity.Critical:
                    return Color.FromArgb(236, 92, 76);

                default:
                    return context.DimPhosphorColor;
            }
        }

        private void DrawSubpageTabs(
            MissionRenderContext context)
        {
            Rectangle content =
                context.ContentBounds;

            // VERTICAL POWER SUBPAGE NAV
            // POWER RESERVED NAV RAIL
            const int gap = 8;
            const int tabWidth = 180;
            const int tabHeight = 36;

            int x =
                content.Right -
                NavRailWidth +
                (NavRailWidth - tabWidth) / 2;

            int y =
                content.Top + 54;

            _oneLineTab =
                new Rectangle(
                    x,
                    y,
                    tabWidth,
                    tabHeight);

            _breakerTab =
                new Rectangle(
                    x,
                    _oneLineTab.Bottom + gap,
                    tabWidth,
                    tabHeight);

            _detailTab =
                new Rectangle(
                    x,
                    _breakerTab.Bottom + gap,
                    tabWidth,
                    tabHeight);

            DrawTab(
                context,
                _oneLineTab,
                "1/3 ONE-LINE",
                _subpage == 1);

            DrawTab(
                context,
                _breakerTab,
                "2/3 BREAKERS",
                _subpage == 2);

            DrawTab(
                context,
                _detailTab,
                "3/3 DETAIL",
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
    }
}
