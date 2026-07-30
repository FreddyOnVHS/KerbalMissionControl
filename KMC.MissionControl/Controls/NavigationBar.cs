using KMC.MissionControl.Pages;
using KMC.MissionControl.Themes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    public sealed class NavigationBar : Panel
    {
        private sealed class NavigationItem
        {
            public string Title { get; set; }
            public IMissionPage Page { get; set; }
            public bool Enabled { get; set; }
            public Rectangle Bounds { get; set; }
        }

        private readonly List<NavigationItem> _items;
        private readonly Font _buttonFont;

        private NavigationItem _activeItem;
        private NavigationItem _hoverItem;

        private const int ButtonWidth = 70;
        private const int ButtonHeight = 28;
        private const int ButtonSpacing = 6;
        private const int ButtonTop = 14;
        private const int CornerRadius = 4;

        public event Action<IMissionPage, string> PageChanged;

        public NavigationBar()
        {
            _items = new List<NavigationItem>();

            _buttonFont = new Font(
                "Consolas",
                10f,
                FontStyle.Bold);

            Height = 44;
            BackColor = ApolloTheme.WindowBackground;

            DoubleBuffered = true;
            ResizeRedraw = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
        }

        public void AddPage(
            string title,
            IMissionPage page,
            bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            if (page == null)
            {
                return;
            }

            int itemIndex = _items.Count;

            NavigationItem item =
                new NavigationItem
                {
                    Title = title.ToUpperInvariant(),
                    Page = page,
                    Enabled = enabled,

                    Bounds = new Rectangle(
                        itemIndex *
                        (ButtonWidth + ButtonSpacing),
                        ButtonTop,
                        ButtonWidth,
                        ButtonHeight)
                };

            _items.Add(item);

            if (_activeItem == null &&
                item.Enabled)
            {
                _activeItem = item;
            }

            Invalidate();
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            e.Graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            DrawBackground(e.Graphics);

            foreach (NavigationItem item in _items)
            {
                DrawNavigationItem(
                    e.Graphics,
                    item);
            }

            DrawLamps(e.Graphics);
        }

        private void DrawBackground(
            Graphics graphics)
        {
            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        39,
                        45,
                        42)))
            {
                graphics.FillRectangle(
                    backgroundBrush,
                    ClientRectangle);
            }
        }

        private void DrawNavigationItem(
            Graphics graphics,
            NavigationItem item)
        {
            bool isActive =
                item == _activeItem;

            bool isHovered =
                item == _hoverItem;

            Color topColor;
            Color bottomColor;
            Color textColor;
            Color borderColor;

            if (!item.Enabled)
            {
                topColor =
                    Color.FromArgb(
                        50,
                        54,
                        51);

                bottomColor =
                    Color.FromArgb(
                        42,
                        46,
                        43);

                textColor =
                    Color.FromArgb(
                        90,
                        98,
                        92);

                borderColor =
                    Color.FromArgb(
                        78,
                        84,
                        79);
            }
            else if (isHovered)
            {
                topColor =
                    Color.FromArgb(
                        83,
                        90,
                        85);

                bottomColor =
                    Color.FromArgb(
                        59,
                        66,
                        61);

                textColor =
                    Color.FromArgb(
                        230,
                        245,
                        230);

                borderColor =
                    Color.FromArgb(
                        150,
                        160,
                        150);
            }
            else
            {
                topColor =
                    Color.FromArgb(
                        66,
                        72,
                        68);

                bottomColor =
                    Color.FromArgb(
                        48,
                        54,
                        50);

                textColor =
                    Color.FromArgb(
                        205,
                        225,
                        205);

                borderColor =
                    Color.FromArgb(
                        120,
                        128,
                        120);
            }

            if (isActive)
            {
                textColor =
                    Color.FromArgb(
                        235,
                        255,
                        235);

                borderColor =
                    Color.FromArgb(
                        160,
                        180,
                        165);
            }

            using (GraphicsPath path =
                CreateRoundedRectangle(
                    item.Bounds,
                    CornerRadius))
            {
                using (LinearGradientBrush backgroundBrush =
                    new LinearGradientBrush(
                        item.Bounds,
                        topColor,
                        bottomColor,
                        LinearGradientMode.Vertical))
                {
                    graphics.FillPath(
                        backgroundBrush,
                        path);
                }

                using (Pen borderPen =
                    new Pen(
                        borderColor,
                        1f))
                {
                    graphics.DrawPath(
                        borderPen,
                        path);
                }
            }

            DrawBevel(
                graphics,
                item.Bounds,
                item.Enabled);

            if (isActive)
            {
                DrawActiveUnderline(
                    graphics,
                    item.Bounds);
            }

            TextRenderer.DrawText(
                graphics,
                item.Title,
                _buttonFont,
                item.Bounds,
                textColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding);
        }

        private static void DrawBevel(
            Graphics graphics,
            Rectangle bounds,
            bool enabled)
        {
            Color highlightColor =
                enabled
                    ? Color.FromArgb(
                        95,
                        130,
                        135,
                        130)
                    : Color.FromArgb(
                        50,
                        110,
                        115,
                        110);

            Color shadowColor =
                Color.FromArgb(
                    100,
                    18,
                    22,
                    19);

            using (Pen highlightPen =
                new Pen(
                    highlightColor,
                    1f))
            {
                graphics.DrawLine(
                    highlightPen,
                    bounds.Left + 4,
                    bounds.Top + 1,
                    bounds.Right - 5,
                    bounds.Top + 1);
            }

            using (Pen shadowPen =
                new Pen(
                    shadowColor,
                    1f))
            {
                graphics.DrawLine(
                    shadowPen,
                    bounds.Left + 4,
                    bounds.Bottom - 2,
                    bounds.Right - 5,
                    bounds.Bottom - 2);
            }
        }

        private static void DrawActiveUnderline(
            Graphics graphics,
            Rectangle bounds)
        {
            Rectangle glowRectangle =
                new Rectangle(
                    bounds.Left + 7,
                    bounds.Bottom - 5,
                    bounds.Width - 14,
                    4);

            Rectangle lineRectangle =
                new Rectangle(
                    bounds.Left + 9,
                    bounds.Bottom - 4,
                    bounds.Width - 18,
                    2);

            using (SolidBrush glowBrush =
                new SolidBrush(
                    Color.FromArgb(
                        75,
                        50,
                        255,
                        95)))
            {
                graphics.FillRectangle(
                    glowBrush,
                    glowRectangle);
            }

            using (SolidBrush lineBrush =
                new SolidBrush(
                    Color.FromArgb(
                        90,
                        255,
                        125)))
            {
                graphics.FillRectangle(
                    lineBrush,
                    lineRectangle);
            }
        }

        private void DrawLamps(
    Graphics graphics)
        {
            foreach (NavigationItem item in _items)
            {
                bool isActive =
                    item == _activeItem;

                DrawLamp(
                    graphics,
                    item,
                    isActive);
            }
        }

        private static void DrawLamp(
            Graphics graphics,
            NavigationItem item,
            bool isActive)
        {
            const int lampSize = 10;

            int lampX =
                item.Bounds.Left
                + (item.Bounds.Width - lampSize) / 2;

            int lampY = 1;

            Rectangle bezelBounds =
                new Rectangle(
                    lampX - 2,
                    lampY - 2,
                    lampSize + 4,
                    lampSize + 4);

            Rectangle lampBounds =
                new Rectangle(
                    lampX,
                    lampY,
                    lampSize,
                    lampSize);

            using (LinearGradientBrush bezelBrush =
                new LinearGradientBrush(
                    bezelBounds,
                    Color.FromArgb(65, 75, 67),
                    Color.FromArgb(8, 14, 10),
                    LinearGradientMode.Vertical))
            {
                graphics.FillEllipse(
                    bezelBrush,
                    bezelBounds);
            }

            if (isActive)
            {
                Rectangle outerGlow =
                    new Rectangle(
                        lampX - 5,
                        lampY - 5,
                        lampSize + 10,
                        lampSize + 10);

                using (SolidBrush glowBrush =
                    new SolidBrush(
                        Color.FromArgb(
                            65,
                            65,
                            255,
                            105)))
                {
                    graphics.FillEllipse(
                        glowBrush,
                        outerGlow);
                }

                using (LinearGradientBrush lampBrush =
                    new LinearGradientBrush(
                        lampBounds,
                        Color.FromArgb(
                            150,
                            255,
                            170),
                        Color.FromArgb(
                            20,
                            185,
                            55),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillEllipse(
                        lampBrush,
                        lampBounds);
                }

                Rectangle reflectionBounds =
                    new Rectangle(
                        lampX + 2,
                        lampY + 1,
                        3,
                        3);

                using (SolidBrush reflectionBrush =
                    new SolidBrush(
                        Color.FromArgb(
                            225,
                            240,
                            255,
                            240)))
                {
                    graphics.FillEllipse(
                        reflectionBrush,
                        reflectionBounds);
                }
            }
            else
            {
                using (LinearGradientBrush offLampBrush =
                    new LinearGradientBrush(
                        lampBounds,
                        Color.FromArgb(
                            38,
                            42,
                            39),
                        Color.FromArgb(
                            8,
                            10,
                            9),
                        LinearGradientMode.Vertical))
                {
                    graphics.FillEllipse(
                        offLampBrush,
                        lampBounds);
                }

                Rectangle reflectionBounds =
                    new Rectangle(
                        lampX + 2,
                        lampY + 1,
                        3,
                        2);

                using (SolidBrush reflectionBrush =
                    new SolidBrush(
                        Color.FromArgb(
                            45,
                            130,
                            140,
                            130)))
                {
                    graphics.FillEllipse(
                        reflectionBrush,
                        reflectionBounds);
                }
            }

            using (Pen bezelOutline =
                new Pen(
                    Color.FromArgb(
                        8,
                        12,
                        9),
                    1f))
            {
                graphics.DrawEllipse(
                    bezelOutline,
                    bezelBounds);
            }
        }

        protected override void OnMouseMove(
            MouseEventArgs e)
        {
            base.OnMouseMove(e);

            NavigationItem hoveredItem =
                FindItemAt(e.Location);

            if (_hoverItem != hoveredItem)
            {
                _hoverItem = hoveredItem;
                Invalidate();
            }

            Cursor =
                hoveredItem != null &&
                hoveredItem.Enabled
                    ? Cursors.Hand
                    : Cursors.Default;
        }

        protected override void OnMouseLeave(
            EventArgs e)
        {
            base.OnMouseLeave(e);

            _hoverItem = null;
            Cursor = Cursors.Default;

            Invalidate();
        }

        protected override void OnMouseDown(
            MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            NavigationItem clickedItem =
                FindItemAt(e.Location);

            if (clickedItem == null ||
                !clickedItem.Enabled)
            {
                return;
            }

            if (_activeItem == clickedItem)
            {
                return;
            }

            _activeItem = clickedItem;

            Invalidate();

            PageChanged?.Invoke(
                clickedItem.Page,
                clickedItem.Title);
        }

        private NavigationItem FindItemAt(
            Point location)
        {
            foreach (NavigationItem item in _items)
            {
                if (item.Bounds.Contains(location))
                {
                    return item;
                }
            }

            return null;
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

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                _buttonFont.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}