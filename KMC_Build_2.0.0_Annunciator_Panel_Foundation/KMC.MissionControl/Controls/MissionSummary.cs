using KMC.MissionControl.Models;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    public sealed class MissionSummary : Control
    {
        private enum LampColor { Blue, Green, Amber, Red }

        private sealed class LampDefinition
        {
            public LampDefinition(string label, LampColor color)
            {
                Label = label;
                Color = color;
            }

            public string Label { get; private set; }
            public LampColor Color { get; private set; }
        }

        private readonly Font _titleFont = new Font("Consolas", 9.5f, FontStyle.Bold);
        private readonly Font _lampFont = new Font("Consolas", 8.0f, FontStyle.Bold);
        private readonly Font _smallFont = new Font("Consolas", 7.5f, FontStyle.Bold);
        private readonly Timer _lampTestTimer;
        private readonly LampDefinition[] _lamps;

        private Rectangle _ackBounds;
        private Rectangle _lampTestBounds;
        private bool _lampTestActive;
        private MissionTelemetry _telemetry = new MissionTelemetry();

        public MissionSummary()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.FromArgb(35, 40, 38);
            Cursor = Cursors.Hand;
            TabStop = true;

            _lamps = CreateLampDefinitions();
            _lampTestTimer = new Timer { Interval = 3000 };
            _lampTestTimer.Tick += OnLampTestTimerTick;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
        }

        public void UpdateTelemetry(MissionTelemetry telemetry)
        {
            _telemetry = telemetry ?? new MissionTelemetry();
            Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _lampTestTimer.Stop();
                _lampTestTimer.Dispose();
                _titleFont.Dispose();
                _lampFont.Dispose();
                _smallFont.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (_lampTestBounds.Contains(e.Location))
            {
                StartLampTest();
            }
            else if (_ackBounds.Contains(e.Location))
            {
                Invalidate(_ackBounds);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.T)
            {
                StartLampTest();
                e.Handled = true;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            DrawPanelFrame(graphics);

            Rectangle inner = new Rectangle(
                18,
                9,
                Math.Max(1, Width - 36),
                Math.Max(1, Height - 18));

            DrawHeader(graphics, inner);

            Rectangle grid = new Rectangle(
                inner.Left + 5,
                inner.Top + 25,
                inner.Width - 10,
                Math.Max(1, inner.Height - 30));

            DrawLampGrid(graphics, grid);
        }

        private void DrawPanelFrame(Graphics graphics)
        {
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));

            using (LinearGradientBrush frame = new LinearGradientBrush(
                bounds,
                Color.FromArgb(102, 106, 99),
                Color.FromArgb(35, 39, 36),
                LinearGradientMode.Vertical))
            using (Pen dark = new Pen(Color.FromArgb(14, 17, 15), 2.0f))
            using (Pen highlight = new Pen(Color.FromArgb(145, 150, 140), 1.0f))
            {
                graphics.FillRectangle(frame, bounds);
                graphics.DrawRectangle(dark, bounds);
                graphics.DrawLine(highlight, 3, 3, Width - 4, 3);
                graphics.DrawLine(highlight, 3, 3, 3, Height - 4);
            }

            DrawFastener(graphics, 7, 7);
            DrawFastener(graphics, Width - 15, 7);
            DrawFastener(graphics, 7, Height - 15);
            DrawFastener(graphics, Width - 15, Height - 15);
        }

        private void DrawHeader(Graphics graphics, Rectangle inner)
        {
            int buttonWidth = Math.Max(76, Math.Min(108, inner.Width / 12));

            _lampTestBounds = new Rectangle(inner.Right - buttonWidth, inner.Top, buttonWidth, 20);
            _ackBounds = new Rectangle(_lampTestBounds.Left - buttonWidth - 7, inner.Top, buttonWidth, 20);

            Rectangle title = new Rectangle(inner.Left + 4, inner.Top, 300, 20);
            Rectangle status = new Rectangle(
                title.Right + 8,
                inner.Top,
                Math.Max(1, _ackBounds.Left - title.Right - 14),
                20);

            TextRenderer.DrawText(
                graphics,
                "EVENT / CAUTION INDICATOR",
                _titleFont,
                title,
                Color.FromArgb(205, 240, 245),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                _lampTestActive ? "LAMP TEST ACTIVE" : "24 INDICATORS  •  FOUNDATION MODE",
                _smallFont,
                status,
                _lampTestActive ? Color.FromArgb(255, 215, 70) : Color.FromArgb(135, 175, 185),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            DrawControlButton(graphics, _ackBounds, "ACK", false);
            DrawControlButton(graphics, _lampTestBounds, "LAMP TEST", _lampTestActive);
        }

        private void DrawLampGrid(Graphics graphics, Rectangle bounds)
        {
            const int columns = 12;
            const int gap = 4;
            int cellWidth = Math.Max(24, (bounds.Width - gap * (columns - 1)) / columns);
            int cellHeight = Math.Max(20, (bounds.Height - gap) / 2);

            for (int index = 0; index < _lamps.Length; index++)
            {
                int row = index / columns;
                int column = index % columns;

                Rectangle lamp = new Rectangle(
                    bounds.Left + column * (cellWidth + gap),
                    bounds.Top + row * (cellHeight + gap),
                    cellWidth,
                    cellHeight);

                DrawLamp(graphics, lamp, _lamps[index], _lampTestActive);
            }
        }

        private void DrawLamp(
            Graphics graphics,
            Rectangle bounds,
            LampDefinition lamp,
            bool illuminated)
        {
            Rectangle lens = Rectangle.Inflate(bounds, -3, -3);
            Color active = GetLampColor(lamp.Color);
            Color top = illuminated ? Lighten(active, 0.24) : Color.FromArgb(57, 61, 58);
            Color bottom = illuminated ? Darken(active, 0.18) : Color.FromArgb(24, 27, 25);

            if (illuminated)
            {
                Rectangle glow = Rectangle.Inflate(bounds, 3, 2);
                using (GraphicsPath path = CreateRoundedRectangle(glow, 4))
                using (PathGradientBrush glowBrush = new PathGradientBrush(path))
                {
                    glowBrush.CenterColor = Color.FromArgb(90, active);
                    glowBrush.SurroundColors = new[] { Color.FromArgb(0, active) };
                    graphics.FillPath(glowBrush, path);
                }
            }

            using (LinearGradientBrush housing = new LinearGradientBrush(
                bounds,
                Color.FromArgb(115, 120, 112),
                Color.FromArgb(32, 35, 32),
                LinearGradientMode.Vertical))
            using (LinearGradientBrush lensBrush = new LinearGradientBrush(
                lens,
                top,
                bottom,
                LinearGradientMode.Vertical))
            using (Pen housingBorder = new Pen(Color.FromArgb(18, 20, 18)))
            using (Pen lensBorder = new Pen(
                illuminated ? Lighten(active, 0.35) : Color.FromArgb(92, 97, 91)))
            {
                graphics.FillRectangle(housing, bounds);
                graphics.DrawRectangle(housingBorder, bounds);
                graphics.FillRectangle(lensBrush, lens);
                graphics.DrawRectangle(lensBorder, lens);
            }

            TextRenderer.DrawText(
                graphics,
                lamp.Label,
                _lampFont,
                new Rectangle(lens.Left + 2, lens.Top + 1, Math.Max(1, lens.Width - 4), Math.Max(1, lens.Height - 2)),
                illuminated ? GetReadableTextColor(lamp.Color) : Color.FromArgb(118, 124, 119),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        private void DrawControlButton(Graphics graphics, Rectangle bounds, string text, bool active)
        {
            Color accent = active ? Color.FromArgb(255, 210, 55) : Color.FromArgb(125, 160, 165);

            using (LinearGradientBrush brush = new LinearGradientBrush(
                bounds,
                active ? Color.FromArgb(125, 105, 24) : Color.FromArgb(58, 63, 59),
                Color.FromArgb(22, 25, 23),
                LinearGradientMode.Vertical))
            using (Pen pen = new Pen(accent))
            {
                graphics.FillRectangle(brush, bounds);
                graphics.DrawRectangle(pen, bounds);
            }

            TextRenderer.DrawText(
                graphics,
                text,
                _smallFont,
                bounds,
                accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void StartLampTest()
        {
            _lampTestActive = true;
            _lampTestTimer.Stop();
            _lampTestTimer.Start();
            Invalidate();
        }

        private void OnLampTestTimerTick(object sender, EventArgs e)
        {
            _lampTestTimer.Stop();
            _lampTestActive = false;
            Invalidate();
        }

        private static LampDefinition[] CreateLampDefinitions()
        {
            return new[]
            {
                new LampDefinition("MASTER\nCAUTION", LampColor.Amber),
                new LampDefinition("MASTER\nWARNING", LampColor.Red),
                new LampDefinition("ENGINE\nFAULT", LampColor.Red),
                new LampDefinition("LOW\nPOWER", LampColor.Amber),
                new LampDefinition("LINK\nLOST", LampColor.Red),
                new LampDefinition("ABORT\nREQ", LampColor.Red),
                new LampDefinition("ASCENT", LampColor.Blue),
                new LampDefinition("ORBIT", LampColor.Blue),
                new LampDefinition("DESCENT", LampColor.Blue),
                new LampDefinition("LANDED", LampColor.Green),
                new LampDefinition("DOCKED", LampColor.Green),
                new LampDefinition("LINK\nOK", LampColor.Green),
                new LampDefinition("ENG\nIGN", LampColor.Blue),
                new LampDefinition("MAIN\nENG", LampColor.Green),
                new LampDefinition("SRB\nBURN", LampColor.Amber),
                new LampDefinition("SRB\nSEP", LampColor.Green),
                new LampDefinition("STAGE\nSEP", LampColor.Green),
                new LampDefinition("FLAMEOUT", LampColor.Red),
                new LampDefinition("LOW LF", LampColor.Amber),
                new LampDefinition("LOW OX", LampColor.Amber),
                new LampDefinition("LOW\nMONO", LampColor.Amber),
                new LampDefinition("HEAT\nHIGH", LampColor.Red),
                new LampDefinition("G FORCE", LampColor.Amber),
                new LampDefinition("SAS ON", LampColor.Blue)
            };
        }

        private static Color GetLampColor(LampColor color)
        {
            switch (color)
            {
                case LampColor.Blue: return Color.FromArgb(48, 90, 255);
                case LampColor.Green: return Color.FromArgb(30, 245, 75);
                case LampColor.Amber: return Color.FromArgb(255, 205, 35);
                case LampColor.Red: return Color.FromArgb(235, 38, 28);
                default: return Color.White;
            }
        }

        private static Color GetReadableTextColor(LampColor color)
        {
            return color == LampColor.Blue || color == LampColor.Red
                ? Color.White
                : Color.FromArgb(12, 16, 13);
        }

        private static Color Lighten(Color color, double amount)
        {
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromArgb(
                color.A,
                color.R + (int)((255 - color.R) * amount),
                color.G + (int)((255 - color.G) * amount),
                color.B + (int)((255 - color.B) * amount));
        }

        private static Color Darken(Color color, double amount)
        {
            amount = Math.Max(0.0, Math.Min(1.0, amount));
            return Color.FromArgb(
                color.A,
                (int)(color.R * (1.0 - amount)),
                (int)(color.G * (1.0 - amount)),
                (int)(color.B * (1.0 - amount)));
        }

        private static void DrawFastener(Graphics graphics, int x, int y)
        {
            Rectangle bounds = new Rectangle(x, y, 8, 8);

            using (LinearGradientBrush brush = new LinearGradientBrush(
                bounds,
                Color.FromArgb(170, 174, 164),
                Color.FromArgb(45, 48, 44),
                LinearGradientMode.Vertical))
            using (Pen outline = new Pen(Color.FromArgb(18, 20, 18)))
            using (Pen slot = new Pen(Color.FromArgb(35, 37, 34)))
            {
                graphics.FillEllipse(brush, bounds);
                graphics.DrawEllipse(outline, bounds);
                graphics.DrawLine(slot, x + 2, y + 6, x + 6, y + 2);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(2, radius * 2);
            Rectangle arc = new Rectangle(bounds.Left, bounds.Top, diameter, diameter);

            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();

            return path;
        }
    }
}
