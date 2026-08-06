using KMC.MissionControl.Models;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    /// <summary>
    /// Persistent lower-console annunciator panel.
    /// Build 2.0.1 provides the panel foundation and lamp test only.
    /// Live event evaluation will be connected in later milestones.
    /// </summary>
    public sealed class MissionSummary : Control
    {
        private const int RequiredAnnunciatorHeight = 180;

        private enum LampColor
        {
            Blue,
            Green,
            Amber,
            Red
        }

        private sealed class LampDefinition
        {
            public LampDefinition(
                string label,
                LampColor color)
            {
                Label = label;
                Color = color;
            }

            public string Label { get; private set; }
            public LampColor Color { get; private set; }
        }

        private readonly Font _titleFont;
        private readonly Font _lampFont;
        private readonly Font _smallFont;
        private readonly Timer _lampTestTimer;
        private readonly LampDefinition[] _lamps;

        private Rectangle _ackBounds;
        private Rectangle _lampTestBounds;
        private bool _lampTestActive;

        public MissionSummary()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;

            BackColor =
                Color.FromArgb(
                    35,
                    40,
                    38);

            _titleFont =
                new Font(
                    "Consolas",
                    9.5f,
                    FontStyle.Bold);

            _lampFont =
                new Font(
                    "Consolas",
                    7.5f,
                    FontStyle.Bold);

            _smallFont =
                new Font(
                    "Consolas",
                    7.5f,
                    FontStyle.Bold);

            _lamps =
                CreateLampDefinitions();

            _lampTestTimer =
                new Timer
                {
                    Interval = 3000
                };

            _lampTestTimer.Tick +=
                OnLampTestTimerTick;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            TabStop = true;
            Cursor = Cursors.Hand;

            MinimumSize =
                new Size(
                    320,
                    RequiredAnnunciatorHeight);
        }

        protected override void OnParentChanged(
            EventArgs e)
        {
            base.OnParentChanged(
                e);

            TableLayoutPanel layout =
                Parent as TableLayoutPanel;

            if (layout != null)
            {
                layout.SizeChanged -=
                    OnHostLayoutSizeChanged;

                layout.SizeChanged +=
                    OnHostLayoutSizeChanged;
            }

            EnsureHostRowHeight();
        }

        protected override void OnVisibleChanged(
            EventArgs e)
        {
            base.OnVisibleChanged(
                e);

            if (Visible)
            {
                EnsureHostRowHeight();
            }
        }

        private void OnHostLayoutSizeChanged(
            object sender,
            EventArgs e)
        {
            EnsureHostRowHeight();
        }

        private void EnsureHostRowHeight()
        {
            if (!Visible)
            {
                return;
            }

            TableLayoutPanel layout =
                Parent as TableLayoutPanel;

            if (layout == null)
            {
                return;
            }

            TableLayoutPanelCellPosition position =
                layout.GetPositionFromControl(
                    this);

            if (position.Row < 0 ||
                position.Row >=
                    layout.RowStyles.Count)
            {
                return;
            }

            RowStyle row =
                layout.RowStyles[position.Row];

            if (row.SizeType !=
                    SizeType.Absolute ||
                row.Height <
                    RequiredAnnunciatorHeight)
            {
                row.SizeType =
                    SizeType.Absolute;

                row.Height =
                    RequiredAnnunciatorHeight;

                layout.PerformLayout();
            }
        }

        public void UpdateTelemetry(
            MissionTelemetry telemetry)
        {
            // Live event logic is intentionally deferred.
            Invalidate();
        }

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _lampTestTimer.Stop();
                _lampTestTimer.Dispose();

                _titleFont.Dispose();
                _lampFont.Dispose();
                _smallFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            Focus();

            if (_lampTestBounds.Contains(
                    e.Location))
            {
                StartLampTest();
                return;
            }

            if (_ackBounds.Contains(
                    e.Location))
            {
                Invalidate(
                    _ackBounds);
            }
        }

        protected override void OnKeyDown(
            KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.T)
            {
                StartLampTest();
                e.Handled = true;
            }
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

            DrawPanelFrame(
                graphics);

            Rectangle inner =
                new Rectangle(
                    18,
                    9,
                    Math.Max(
                        1,
                        Width - 36),
                    Math.Max(
                        1,
                        Height - 18));

            DrawHeader(
                graphics,
                inner);

            Rectangle grid =
                new Rectangle(
                    inner.Left + 5,
                    inner.Top + 25,
                    Math.Max(
                        1,
                        inner.Width - 10),
                    Math.Max(
                        1,
                        inner.Height - 29));

            DrawLampGrid(
                graphics,
                grid);
        }

        private void DrawPanelFrame(
            Graphics graphics)
        {
            Rectangle bounds =
                new Rectangle(
                    0,
                    0,
                    Math.Max(
                        1,
                        Width - 1),
                    Math.Max(
                        1,
                        Height - 1));

            using (LinearGradientBrush frame =
                new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(
                        102,
                        106,
                        99),
                    Color.FromArgb(
                        35,
                        39,
                        36),
                    LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(
                    frame,
                    bounds);
            }

            using (Pen dark =
                new Pen(
                    Color.FromArgb(
                        14,
                        17,
                        15),
                    2.0f))
            using (Pen highlight =
                new Pen(
                    Color.FromArgb(
                        145,
                        150,
                        140),
                    1.0f))
            {
                graphics.DrawRectangle(
                    dark,
                    bounds);

                graphics.DrawLine(
                    highlight,
                    3,
                    3,
                    Width - 4,
                    3);

                graphics.DrawLine(
                    highlight,
                    3,
                    3,
                    3,
                    Height - 4);
            }

            DrawFastener(
                graphics,
                7,
                7);

            DrawFastener(
                graphics,
                Width - 15,
                7);

            DrawFastener(
                graphics,
                7,
                Height - 15);

            DrawFastener(
                graphics,
                Width - 15,
                Height - 15);
        }

        private void DrawHeader(
            Graphics graphics,
            Rectangle inner)
        {
            Rectangle title =
                new Rectangle(
                    inner.Left + 4,
                    inner.Top,
                    310,
                    21);

            int buttonWidth =
                Math.Max(
                    72,
                    Math.Min(
                        104,
                        inner.Width / 12));

            _lampTestBounds =
                new Rectangle(
                    inner.Right - buttonWidth,
                    inner.Top,
                    buttonWidth,
                    20);

            _ackBounds =
                new Rectangle(
                    _lampTestBounds.Left -
                    buttonWidth -
                    7,
                    inner.Top,
                    buttonWidth,
                    20);

            Rectangle status =
                new Rectangle(
                    title.Right + 8,
                    inner.Top,
                    Math.Max(
                        1,
                        _ackBounds.Left -
                        title.Right -
                        14),
                    20);

            TextRenderer.DrawText(
                graphics,
                "EVENT / CAUTION INDICATOR",
                _titleFont,
                title,
                Color.FromArgb(
                    205,
                    240,
                    245),
                TextFormatFlags.Left |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                _lampTestActive
                    ? "LAMP TEST ACTIVE"
                    : "FOUNDATION MODE",
                _smallFont,
                status,
                _lampTestActive
                    ? Color.FromArgb(
                        255,
                        215,
                        70)
                    : Color.FromArgb(
                        135,
                        175,
                        185),
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            DrawControlButton(
                graphics,
                _ackBounds,
                "ACK",
                false);

            DrawControlButton(
                graphics,
                _lampTestBounds,
                "LAMP TEST",
                _lampTestActive);
        }

        private void DrawLampGrid(
            Graphics graphics,
            Rectangle bounds)
        {
            const int columns = 12;
            const int rows = 2;
            const int gap = 3;

            int cellWidth =
                Math.Max(
                    20,
                    (bounds.Width -
                     gap *
                     (columns - 1)) /
                    columns);

            int cellHeight =
                Math.Max(
                    18,
                    (bounds.Height -
                     gap *
                     (rows - 1)) /
                    rows);

            for (int index = 0;
                 index < _lamps.Length;
                 index++)
            {
                int row =
                    index /
                    columns;

                int column =
                    index %
                    columns;

                Rectangle lamp =
                    new Rectangle(
                        bounds.Left +
                        column *
                        (cellWidth + gap),
                        bounds.Top +
                        row *
                        (cellHeight + gap),
                        cellWidth,
                        cellHeight);

                DrawLamp(
                    graphics,
                    lamp,
                    _lamps[index],
                    _lampTestActive);
            }
        }

        private void DrawLamp(
            Graphics graphics,
            Rectangle bounds,
            LampDefinition lamp,
            bool illuminated)
        {
            Rectangle lens =
                Rectangle.Inflate(
                    bounds,
                    -3,
                    -3);

            Color active =
                GetLampColor(
                    lamp.Color);

            Color faceTop =
                illuminated
                    ? Lighten(
                        active,
                        0.24)
                    : Color.FromArgb(
                        57,
                        61,
                        58);

            Color faceBottom =
                illuminated
                    ? Darken(
                        active,
                        0.18)
                    : Color.FromArgb(
                        24,
                        27,
                        25);

            using (LinearGradientBrush housing =
                new LinearGradientBrush(
                    bounds,
                    Color.FromArgb(
                        115,
                        120,
                        112),
                    Color.FromArgb(
                        32,
                        35,
                        32),
                    LinearGradientMode.Vertical))
            using (Pen outerBorder =
                new Pen(
                    Color.FromArgb(
                        18,
                        20,
                        18),
                    1.0f))
            {
                graphics.FillRectangle(
                    housing,
                    bounds);

                graphics.DrawRectangle(
                    outerBorder,
                    bounds);
            }

            using (LinearGradientBrush lensBrush =
                new LinearGradientBrush(
                    lens,
                    faceTop,
                    faceBottom,
                    LinearGradientMode.Vertical))
            using (Pen lensBorder =
                new Pen(
                    illuminated
                        ? Lighten(
                            active,
                            0.35)
                        : Color.FromArgb(
                            92,
                            97,
                            91),
                    1.0f))
            {
                graphics.FillRectangle(
                    lensBrush,
                    lens);

                graphics.DrawRectangle(
                    lensBorder,
                    lens);
            }

            Color textColor =
                illuminated
                    ? GetReadableTextColor(
                        lamp.Color)
                    : Color.FromArgb(
                        118,
                        124,
                        119);

            TextRenderer.DrawText(
                graphics,
                lamp.Label,
                _lampFont,
                new Rectangle(
                    lens.Left + 2,
                    lens.Top + 1,
                    Math.Max(
                        1,
                        lens.Width - 4),
                    Math.Max(
                        1,
                        lens.Height - 2)),
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.WordBreak |
                TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);
        }

        private void DrawControlButton(
            Graphics graphics,
            Rectangle bounds,
            string text,
            bool active)
        {
            Color accent =
                active
                    ? Color.FromArgb(
                        255,
                        210,
                        55)
                    : Color.FromArgb(
                        125,
                        160,
                        165);

            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    bounds,
                    active
                        ? Color.FromArgb(
                            125,
                            105,
                            24)
                        : Color.FromArgb(
                            58,
                            63,
                            59),
                    Color.FromArgb(
                        22,
                        25,
                        23),
                    LinearGradientMode.Vertical))
            using (Pen pen =
                new Pen(
                    accent,
                    1.0f))
            {
                graphics.FillRectangle(
                    brush,
                    bounds);

                graphics.DrawRectangle(
                    pen,
                    bounds);
            }

            TextRenderer.DrawText(
                graphics,
                text,
                _smallFont,
                bounds,
                accent,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private void StartLampTest()
        {
            _lampTestActive = true;

            _lampTestTimer.Stop();
            _lampTestTimer.Start();

            Invalidate();
        }

        private void OnLampTestTimerTick(
            object sender,
            EventArgs e)
        {
            _lampTestTimer.Stop();

            _lampTestActive = false;

            Invalidate();
        }

        private static LampDefinition[]
            CreateLampDefinitions()
        {
            return new[]
            {
                new LampDefinition(
                    "MASTER\nCAUTION",
                    LampColor.Amber),
                new LampDefinition(
                    "MASTER\nWARNING",
                    LampColor.Red),
                new LampDefinition(
                    "ENGINE\nFAULT",
                    LampColor.Red),
                new LampDefinition(
                    "LOW\nPOWER",
                    LampColor.Amber),
                new LampDefinition(
                    "LINK\nLOST",
                    LampColor.Red),
                new LampDefinition(
                    "ABORT\nREQ",
                    LampColor.Red),
                new LampDefinition(
                    "ASCENT",
                    LampColor.Blue),
                new LampDefinition(
                    "ORBIT",
                    LampColor.Blue),
                new LampDefinition(
                    "DESCENT",
                    LampColor.Blue),
                new LampDefinition(
                    "LANDED",
                    LampColor.Green),
                new LampDefinition(
                    "DOCKED",
                    LampColor.Green),
                new LampDefinition(
                    "LINK\nOK",
                    LampColor.Green),

                new LampDefinition(
                    "ENG\nIGN",
                    LampColor.Blue),
                new LampDefinition(
                    "MAIN\nENG",
                    LampColor.Green),
                new LampDefinition(
                    "SRB\nBURN",
                    LampColor.Amber),
                new LampDefinition(
                    "SRB\nSEP",
                    LampColor.Green),
                new LampDefinition(
                    "STAGE\nSEP",
                    LampColor.Green),
                new LampDefinition(
                    "FLAMEOUT",
                    LampColor.Red),
                new LampDefinition(
                    "LOW LF",
                    LampColor.Amber),
                new LampDefinition(
                    "LOW OX",
                    LampColor.Amber),
                new LampDefinition(
                    "LOW\nMONO",
                    LampColor.Amber),
                new LampDefinition(
                    "HEAT\nHIGH",
                    LampColor.Red),
                new LampDefinition(
                    "G FORCE",
                    LampColor.Amber),
                new LampDefinition(
                    "SAS ON",
                    LampColor.Blue)
            };
        }

        private static Color GetLampColor(
            LampColor color)
        {
            switch (color)
            {
                case LampColor.Blue:
                    return Color.FromArgb(
                        48,
                        90,
                        255);

                case LampColor.Green:
                    return Color.FromArgb(
                        30,
                        245,
                        75);

                case LampColor.Amber:
                    return Color.FromArgb(
                        255,
                        205,
                        35);

                case LampColor.Red:
                    return Color.FromArgb(
                        235,
                        38,
                        28);

                default:
                    return Color.White;
            }
        }

        private static Color GetReadableTextColor(
            LampColor color)
        {
            switch (color)
            {
                case LampColor.Blue:
                case LampColor.Red:
                    return Color.White;

                default:
                    return Color.FromArgb(
                        12,
                        16,
                        13);
            }
        }

        private static Color Lighten(
            Color color,
            double amount)
        {
            amount =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        amount));

            return Color.FromArgb(
                color.A,
                color.R +
                (int)
                ((255 - color.R) *
                 amount),
                color.G +
                (int)
                ((255 - color.G) *
                 amount),
                color.B +
                (int)
                ((255 - color.B) *
                 amount));
        }

        private static Color Darken(
            Color color,
            double amount)
        {
            amount =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        amount));

            return Color.FromArgb(
                color.A,
                (int)
                (color.R *
                 (1.0 - amount)),
                (int)
                (color.G *
                 (1.0 - amount)),
                (int)
                (color.B *
                 (1.0 - amount)));
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
                        170,
                        174,
                        164),
                    Color.FromArgb(
                        45,
                        48,
                        44),
                    LinearGradientMode.Vertical))
            using (Pen outline =
                new Pen(
                    Color.FromArgb(
                        18,
                        20,
                        18)))
            using (Pen slot =
                new Pen(
                    Color.FromArgb(
                        35,
                        37,
                        34)))
            {
                graphics.FillEllipse(
                    brush,
                    bounds);

                graphics.DrawEllipse(
                    outline,
                    bounds);

                graphics.DrawLine(
                    slot,
                    x + 2,
                    y + 6,
                    x + 6,
                    y + 2);
            }
        }
    }
}
