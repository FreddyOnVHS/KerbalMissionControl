using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Themes;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    public sealed class PerformanceOverlay : Control
    {
        private PerformanceSnapshot _snapshot;

        private readonly Font _titleFont;
        private readonly Font _labelFont;
        private readonly Font _valueFont;

        public PerformanceOverlay()
        {
            _snapshot =
                new PerformanceSnapshot();

            _titleFont =
                new Font(
                    "Consolas",
                    12.0f,
                    FontStyle.Bold);

            _labelFont =
                new Font(
                    "Consolas",
                    9.0f,
                    FontStyle.Regular);

            _valueFont =
                new Font(
                    "Consolas",
                    9.0f,
                    FontStyle.Bold);

            Width = 390;
            Height = 330;

            Anchor =
                AnchorStyles.Top |
                AnchorStyles.Right;

            /*
             * Use a fully opaque background.
             *
             * Standard WinForms controls reject colors with a partial alpha
             * channel unless transparent-background support is enabled.
             * Transparency would also force additional parent repainting,
             * which is undesirable for a performance diagnostics control.
             */
            BackColor =
                Color.FromArgb(
                    3,
                    12,
                    17);

            ForeColor =
                ApolloTheme.CrtGreen;

            Visible =
                false;

            DoubleBuffered =
                true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer,
                true);
        }

        public void UpdateSnapshot(
            PerformanceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _snapshot =
                snapshot;

            if (Visible)
            {
                Invalidate();
            }
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(
                e);

            e.Graphics.Clear(
                BackColor);

            Rectangle bounds =
                ClientRectangle;

            using (Pen framePen =
                new Pen(
                    Color.FromArgb(
                        190,
                        ApolloTheme.CrtGreen),
                    1.5f))
            {
                e.Graphics.DrawRectangle(
                    framePen,
                    0,
                    0,
                    Math.Max(
                        0,
                        bounds.Width - 1),
                    Math.Max(
                        0,
                        bounds.Height - 1));
            }

            int y = 12;

            using (SolidBrush titleBrush =
                new SolidBrush(
                    ApolloTheme.CrtGreen))
            {
                e.Graphics.DrawString(
                    "KMC PERFORMANCE MONITOR",
                    _titleFont,
                    titleBrush,
                    12,
                    y);
            }

            y += 30;

            DrawRow(e.Graphics, ref y, "LINK",
                _snapshot.LinkOnline ? "ONLINE" : "OFFLINE");

            DrawRow(e.Graphics, ref y, "DISPLAY TARGET",
                _snapshot.SelectedDisplayFps + " FPS");

            DrawRow(e.Graphics, ref y, "PACKETS RX",
                _snapshot.PacketsReceived.ToString("N0"));

            DrawRow(e.Graphics, ref y, "PACKETS SHOWN",
                _snapshot.PacketsDisplayed.ToString("N0"));

            DrawRow(e.Graphics, ref y, "SUPERSEDED",
                _snapshot.PacketsSuperseded.ToString("N0"));

            y += 6;

            DrawRow(e.Graphics, ref y, "RENDER COUNT",
                _snapshot.RenderCount.ToString("N0"));

            DrawRow(e.Graphics, ref y, "RENDER LAST / AVG",
                _snapshot.LastRenderMilliseconds.ToString("0.00")
                + " / "
                + _snapshot.AverageRenderMilliseconds.ToString("0.00")
                + " ms");

            DrawRow(e.Graphics, ref y, "PAINT COUNT",
                _snapshot.PaintCount.ToString("N0"));

            DrawRow(e.Graphics, ref y, "PAINT LAST / AVG",
                _snapshot.LastPaintMilliseconds.ToString("0.00")
                + " / "
                + _snapshot.AveragePaintMilliseconds.ToString("0.00")
                + " ms");

            y += 6;

            DrawRow(e.Graphics, ref y, "BITMAP",
                _snapshot.BitmapSize.Width
                + " x "
                + _snapshot.BitmapSize.Height);

            DrawRow(e.Graphics, ref y, "BITMAP MEMORY",
                FormatBytes(_snapshot.BitmapBytes));

            DrawRow(e.Graphics, ref y, "BITMAP ALLOCS",
                _snapshot.BitmapAllocationCount.ToString("N0"));

            DrawRow(e.Graphics, ref y, "MANAGED MEMORY",
                FormatBytes(_snapshot.ManagedMemoryBytes));

            DrawRow(e.Graphics, ref y, "GC GEN 0 / 1 / 2",
                _snapshot.GenerationZeroCollections
                + " / "
                + _snapshot.GenerationOneCollections
                + " / "
                + _snapshot.GenerationTwoCollections);

            DrawRow(e.Graphics, ref y, "RENDER STATE",
                _snapshot.RenderingSuspended
                    ? "SUSPENDED"
                    : "ACTIVE");
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _labelFont.Dispose();
                _valueFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        private void DrawRow(
            Graphics graphics,
            ref int y,
            string label,
            string value)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    Color.FromArgb(
                        170,
                        ApolloTheme.CrtGreen)))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    Color.FromArgb(
                        225,
                        205,
                        255,
                        215)))
            {
                graphics.DrawString(
                    label,
                    _labelFont,
                    labelBrush,
                    12,
                    y);

                graphics.DrawString(
                    value,
                    _valueFont,
                    valueBrush,
                    178,
                    y);
            }

            y += 20;
        }

        private static string FormatBytes(
            long bytes)
        {
            double value =
                Math.Max(
                    0L,
                    bytes);

            string[] suffixes =
            {
                "B",
                "KB",
                "MB",
                "GB"
            };

            int suffixIndex = 0;

            while (value >= 1024.0 &&
                   suffixIndex <
                   suffixes.Length - 1)
            {
                value /= 1024.0;
                suffixIndex++;
            }

            return
                value.ToString(
                    suffixIndex == 0
                        ? "0"
                        : "0.0")
                + " "
                + suffixes[suffixIndex];
        }
    }
}
