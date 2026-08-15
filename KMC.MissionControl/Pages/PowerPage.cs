using System;
using System.Drawing;
using KMC.Engine.Analysis;
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
        private const int WmLeftButtonDown =
            0x0201;

        private int _subpage = 1;

        private Rectangle _oneLineTab;
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
            else if (_detailTab.Contains(
                         point))
            {
                _subpage = 2;
            }
            else if (_subpage == 2 &&
                     _sourcePreviousButton.Contains(
                         point) &&
                     _sourceInventoryPage > 0)
            {
                _sourceInventoryPage--;
                display.RequestRender();
                return false;
            }
            else if (_subpage == 2 &&
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

            if (_subpage == 2)
            {
                /*
                 * Build 14.14.2C:
                 * Render POWER 2/2 once using the consolidated two-column
                 * layout. The previous base-renderer + overlay stack is no
                 * longer called, eliminating hidden duplicate drawing work.
                 */
                int effectivePage;

                PowerDetailConsolidatedRenderer.Draw(
                    context,
                    engineering,
                    _sourceInventoryPage,
                    out _sourcePreviousButton,
                    out _sourceNextButton,
                    out _sourceInventoryPageCount,
                    out effectivePage);

                _sourceInventoryPage =
                    effectivePage;
            }
            else
            {
                PowerSchematicRenderer.Draw(
                    context,
                    telemetry,
                    engineering);
            }

            DrawSubpageTabs(
                context);
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
                tabWidth * 2 +
                gap;

            int x =
                content.Right -
                totalWidth;

            int y =
                content.Top + 44;

            _oneLineTab =
                new Rectangle(
                    x,
                    y,
                    tabWidth,
                    tabHeight);

            _detailTab =
                new Rectangle(
                    _oneLineTab.Right + gap,
                    y,
                    tabWidth,
                    tabHeight);

            DrawTab(
                context,
                _oneLineTab,
                "1/2 ONE-LINE",
                _subpage == 1);

            DrawTab(
                context,
                _detailTab,
                "2/2 DETAIL",
                _subpage == 2);
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
