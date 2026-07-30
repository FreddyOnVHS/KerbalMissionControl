using KMC.MissionControl.Models;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    public sealed class MissionSummary : Control
    {
        private readonly Font _titleFont;
        private readonly Font _labelFont;
        private readonly Font _valueFont;
        private readonly Font _channelFont;

        private MissionTelemetry _telemetry;

        public MissionSummary()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            BackColor =
                Color.FromArgb(
                    35,
                    40,
                    38);

            _titleFont = new Font(
                "Consolas",
                11f,
                FontStyle.Bold);

            _labelFont = new Font(
                "Consolas",
                10f,
                FontStyle.Regular);

            _valueFont = new Font(
                "Consolas",
                10f,
                FontStyle.Bold);

            _channelFont = new Font(
                "Consolas",
                9f,
                FontStyle.Regular);

            _telemetry =
                new MissionTelemetry();

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public void UpdateTelemetry(
            MissionTelemetry telemetry)
        {
            _telemetry =
                telemetry ??
                new MissionTelemetry();

            Invalidate();
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics =
                e.Graphics;

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            DrawOuterFrame(graphics);
            DrawBezel(graphics);
            DrawGlass(graphics);
            DrawHeader(graphics);
            DrawRows(graphics);
            DrawScanLines(graphics);
            DrawGlassReflection(graphics);
        }

        private void DrawOuterFrame(
            Graphics graphics)
        {
            Rectangle outerBounds =
                new Rectangle(
                    0,
                    0,
                    Width - 1,
                    Height - 1);

            using (LinearGradientBrush frameBrush =
                new LinearGradientBrush(
                    outerBounds,
                    Color.FromArgb(
                        108,
                        112,
                        103),
                    Color.FromArgb(
                        40,
                        44,
                        41),
                    LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(
                    frameBrush,
                    outerBounds);
            }

            using (Pen outerBorder =
                new Pen(
                    Color.FromArgb(
                        15,
                        18,
                        16),
                    2f))
            {
                graphics.DrawRectangle(
                    outerBorder,
                    outerBounds);
            }

            using (Pen highlightPen =
                new Pen(
                    Color.FromArgb(
                        155,
                        160,
                        150),
                    1f))
            {
                graphics.DrawLine(
                    highlightPen,
                    3,
                    3,
                    Width - 4,
                    3);

                graphics.DrawLine(
                    highlightPen,
                    3,
                    3,
                    3,
                    Height - 4);
            }

            DrawFastener(
                graphics,
                8,
                8);

            DrawFastener(
                graphics,
                Width - 16,
                8);

            DrawFastener(
                graphics,
                8,
                Height - 16);

            DrawFastener(
                graphics,
                Width - 16,
                Height - 16);
        }

        private void DrawBezel(
            Graphics graphics)
        {
            Rectangle bezelBounds =
                new Rectangle(
                    18,
                    14,
                    Width - 36,
                    Height - 28);

            using (GraphicsPath bezelPath =
                CreateRoundedRectangle(
                    bezelBounds,
                    14))
            {
                using (LinearGradientBrush bezelBrush =
                    new LinearGradientBrush(
                        bezelBounds,
                        Color.FromArgb(
                            28,
                            33,
                            31),
                        Color.FromArgb(
                            8,
                            11,
                            10),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(
                        bezelBrush,
                        bezelPath);
                }

                using (Pen bezelBorder =
                    new Pen(
                        Color.FromArgb(
                            7,
                            10,
                            8),
                        2f))
                {
                    graphics.DrawPath(
                        bezelBorder,
                        bezelPath);
                }
            }
        }

        private void DrawGlass(
            Graphics graphics)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            using (GraphicsPath glassPath =
                CreateRoundedRectangle(
                    glassBounds,
                    10))
            {
                using (LinearGradientBrush glassBrush =
                    new LinearGradientBrush(
                        glassBounds,
                        Color.FromArgb(
                            9,
                            35,
                            42),
                        Color.FromArgb(
                            3,
                            19,
                            24),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(
                        glassBrush,
                        glassPath);
                }

                using (Pen glassBorder =
                    new Pen(
                        Color.FromArgb(
                            42,
                            86,
                            91),
                        1.5f))
                {
                    graphics.DrawPath(
                        glassBorder,
                        glassPath);
                }
            }
        }

        private void DrawHeader(
            Graphics graphics)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            Rectangle titleBounds =
                new Rectangle(
                    glassBounds.Left + 20,
                    glassBounds.Top + 13,
                    260,
                    22);

            Rectangle channelBounds =
                new Rectangle(
                    glassBounds.Right - 90,
                    glassBounds.Top + 13,
                    70,
                    22);

            Color phosphor =
                Color.FromArgb(
                    185,
                    240,
                    245);

            TextRenderer.DrawText(
                graphics,
                "MISSION SUMMARY",
                _titleFont,
                titleBounds,
                phosphor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                "CH 03",
                _channelFont,
                channelBounds,
                phosphor,
                TextFormatFlags.Right |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            int lineY =
                titleBounds.Bottom + 2;

            using (Pen linePen =
                new Pen(
                    Color.FromArgb(
                        95,
                        155,
                        165),
                    1f))
            {
                graphics.DrawLine(
                    linePen,
                    glassBounds.Left + 20,
                    lineY,
                    glassBounds.Right - 20,
                    lineY);
            }
        }

        private void DrawRows(
            Graphics graphics)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            int rowTop =
                glassBounds.Top + 50;

            int rowHeight = 25;

            DrawRow(
                graphics,
                "VESSEL",
                FormatText(
                    _telemetry.VesselName),
                rowTop + rowHeight * 0);

            DrawRow(
                graphics,
                "BODY",
                FormatText(
                    _telemetry.BodyName),
                rowTop + rowHeight * 1);

            DrawRow(
                graphics,
                "MET",
                FormatMissionTime(
                    _telemetry.MissionTime),
                rowTop + rowHeight * 2);

            DrawRow(
                graphics,
                "ALTITUDE",
                FormatDistance(
                    _telemetry.Altitude),
                rowTop + rowHeight * 3);

            DrawRow(
                graphics,
                "SURF SPEED",
                FormatSpeed(
                    _telemetry.SurfaceSpeed),
                rowTop + rowHeight * 4);

            DrawRow(
                graphics,
                "VERT SPEED",
                FormatSignedSpeed(
                    _telemetry.VerticalSpeed),
                rowTop + rowHeight * 5);

            DrawRow(
                graphics,
                "ORBIT SPEED",
                FormatSpeed(
                    _telemetry.OrbitalSpeed),
                rowTop + rowHeight * 6);
        }

        private void DrawRow(
            Graphics graphics,
            string label,
            string value,
            int top)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            Rectangle labelBounds =
                new Rectangle(
                    glassBounds.Left + 22,
                    top,
                    150,
                    22);

            Rectangle valueBounds =
                new Rectangle(
                    glassBounds.Left + 205,
                    top,
                    glassBounds.Width - 230,
                    22);

            Color labelColor =
                Color.FromArgb(
                    145,
                    205,
                    220);

            Color valueColor =
                Color.FromArgb(
                    205,
                    250,
                    255);

            TextRenderer.DrawText(
                graphics,
                label,
                _labelFont,
                labelBounds,
                labelColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            DrawLeaderDots(
                graphics,
                labelBounds.Right,
                valueBounds.Left,
                top + 11);

            TextRenderer.DrawText(
                graphics,
                value,
                _valueFont,
                valueBounds,
                valueColor,
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawLeaderDots(
            Graphics graphics,
            int startX,
            int endX,
            int y)
        {
            using (SolidBrush dotBrush =
                new SolidBrush(
                    Color.FromArgb(
                        55,
                        100,
                        110)))
            {
                for (int x = startX + 5;
                     x < endX - 7;
                     x += 7)
                {
                    graphics.FillRectangle(
                        dotBrush,
                        x,
                        y,
                        2,
                        1);
                }
            }
        }

        private void DrawScanLines(
            Graphics graphics)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            using (Pen scanLinePen =
                new Pen(
                    Color.FromArgb(
                        18,
                        0,
                        0,
                        0),
                    1f))
            {
                for (int y = glassBounds.Top + 2;
                     y < glassBounds.Bottom - 2;
                     y += 3)
                {
                    graphics.DrawLine(
                        scanLinePen,
                        glassBounds.Left + 3,
                        y,
                        glassBounds.Right - 3,
                        y);
                }
            }
        }

        private void DrawGlassReflection(
            Graphics graphics)
        {
            Rectangle glassBounds =
                GetGlassBounds();

            Rectangle reflectionBounds =
                new Rectangle(
                    glassBounds.Left + 8,
                    glassBounds.Top + 6,
                    glassBounds.Width - 16,
                    glassBounds.Height / 3);

            using (GraphicsPath reflectionPath =
                CreateRoundedRectangle(
                    reflectionBounds,
                    8))
            {
                using (LinearGradientBrush reflectionBrush =
                    new LinearGradientBrush(
                        reflectionBounds,
                        Color.FromArgb(
                            24,
                            185,
                            225,
                            235),
                        Color.FromArgb(
                            0,
                            185,
                            225,
                            235),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(
                        reflectionBrush,
                        reflectionPath);
                }
            }
        }

        private static void DrawFastener(
            Graphics graphics,
            int x,
            int y)
        {
            Rectangle bounds =
                new Rectangle(
                    x,
                    y,
                    8,
                    8);

            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(
                        160,
                        165,
                        155),
                    Color.FromArgb(
                        45,
                        48,
                        44),
                    LinearGradientMode.Vertical))
            {
                graphics.FillEllipse(
                    brush,
                    bounds);
            }

            using (Pen outlinePen =
                new Pen(
                    Color.FromArgb(
                        16,
                        18,
                        16)))
            {
                graphics.DrawEllipse(
                    outlinePen,
                    bounds);
            }

            using (Pen slotPen =
                new Pen(
                    Color.FromArgb(
                        30,
                        32,
                        29)))
            {
                graphics.DrawLine(
                    slotPen,
                    x + 2,
                    y + 6,
                    x + 6,
                    y + 2);
            }
        }

        private Rectangle GetGlassBounds()
        {
            return new Rectangle(
                28,
                24,
                Math.Max(
                    1,
                    Width - 56),
                Math.Max(
                    1,
                    Height - 48));
        }

        private static GraphicsPath CreateRoundedRectangle(
            Rectangle bounds,
            int radius)
        {
            GraphicsPath path =
                new GraphicsPath();

            int diameter =
                radius * 2;

            Rectangle arc =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    diameter,
                    diameter);

            path.AddArc(
                arc,
                180,
                90);

            arc.X =
                bounds.Right - diameter;

            path.AddArc(
                arc,
                270,
                90);

            arc.Y =
                bounds.Bottom - diameter;

            path.AddArc(
                arc,
                0,
                90);

            arc.X =
                bounds.Left;

            path.AddArc(
                arc,
                90,
                90);

            path.CloseFigure();

            return path;
        }

        private static string FormatText(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "---"
                : value.ToUpperInvariant();
        }

        private static string FormatDistance(
            double meters)
        {
            if (Math.Abs(meters) >= 1000000.0)
            {
                return
                    (meters / 1000000.0)
                    .ToString("N2")
                    + " Mm";
            }

            if (Math.Abs(meters) >= 1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString("N2")
                    + " km";
            }

            return
                meters.ToString("N1")
                + " m";
        }

        private static string FormatSpeed(
            double metersPerSecond)
        {
            return
                metersPerSecond.ToString("N1")
                + " m/s";
        }

        private static string FormatSignedSpeed(
            double metersPerSecond)
        {
            return
                metersPerSecond
                .ToString(
                    "+0.0;-0.0;0.0")
                + " m/s";
        }

        private static string FormatMissionTime(
            double seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            TimeSpan time =
                TimeSpan.FromSeconds(
                    seconds);

            return string.Format(
                "{0:000}:{1:00}:{2:00}",
                (int)time.TotalHours,
                time.Minutes,
                time.Seconds);
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _labelFont.Dispose();
                _valueFont.Dispose();
                _channelFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}