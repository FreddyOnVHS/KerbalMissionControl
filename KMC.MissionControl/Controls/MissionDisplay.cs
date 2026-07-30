using KMC.MissionControl.Themes;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Rendering;



namespace KMC.MissionControl.Controls
{
    public enum CrtPhosphorMode
    {
        Blue,
        Green,
        Amber
    }

    /// <summary>
    /// Apollo-inspired CRT display that renders a mission page
    /// with configurable phosphor color and CRT visual effects.
    /// </summary>

    public sealed class MissionDisplay : Control
    {
        private IMissionPage _missionPage;
        private MissionTelemetry _telemetry;
        private string _screenTitle;
        private CrtPhosphorMode _phosphorMode;
        private bool _showScanLines;

        public MissionDisplay()
        {

            _missionPage = new OrbitPage();
            _telemetry = new MissionTelemetry();  
            _screenTitle = "DATA DISPLAY";
            _phosphorMode = CrtPhosphorMode.Blue;
            _showScanLines = true;

            Width = 360;
            Height = 260;

            BackColor = ApolloTheme.ConsoleFace;
            ForeColor = ApolloTheme.CrtBlue;

            DoubleBuffered = true;
            ResizeRedraw = true;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);

        }

        public void SetPage(IMissionPage page)
        {
            if (page == null)
            {
                return;
            }

            _missionPage = page;
            Invalidate();
        }

        public void UpdateTelemetry(MissionTelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            _telemetry = telemetry;
            Invalidate();
        }

        public string ScreenTitle
        {
            get
            {
                return _screenTitle;
            }

            set
            {
                _screenTitle =
                    string.IsNullOrWhiteSpace(value)
                        ? "DATA DISPLAY"
                        : value.ToUpperInvariant();

                Invalidate();
            }
        }

        public CrtPhosphorMode PhosphorMode
        {
            get
            {
                return _phosphorMode;
            }

            set
            {
                _phosphorMode = value;
                Invalidate();
            }
        }

        public bool ShowScanLines
        {
            get
            {
                return _showScanLines;
            }

            set
            {
                _showScanLines = value;
                Invalidate();
            }
        }



        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color parentColor =
                Parent != null
                    ? Parent.BackColor
                    : ApolloTheme.ConsoleFace;

            e.Graphics.Clear(parentColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            e.Graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            Rectangle bezelRectangle =
                new Rectangle(
                    1,
                    1,
                    Width - 3,
                    Height - 3);

            Rectangle glassRectangle =
                new Rectangle(
                    18,
                    18,
                    Width - 37,
                    Height - 37);

            DrawBezel(
                e.Graphics,
                bezelRectangle);

            DrawGlass(
                e.Graphics,
                glassRectangle);

            DrawScreenContent(
                e.Graphics,
                glassRectangle);

            if (_showScanLines)
            {
                DrawScanLines(
                    e.Graphics,
                    glassRectangle);
            }

            DrawGlassReflection(
                e.Graphics,
                glassRectangle);
        }

        private static void DrawBezel(
            Graphics graphics,
            Rectangle rectangle)
        {
            using (GraphicsPath path =
                CreateRoundedRectangle(
                    rectangle,
                    28))
            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    rectangle,
                    Color.FromArgb(78, 84, 79),
                    Color.FromArgb(25, 29, 28),
                    LinearGradientMode.Vertical))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(115, 121, 112),
                    2f))
            {
                graphics.FillPath(
                    brush,
                    path);

                graphics.DrawPath(
                    borderPen,
                    path);
                Rectangle vignetteRectangle =
                Rectangle.Inflate(
                    rectangle,
                     -5,
                     -5);

                using (GraphicsPath vignettePath =
                    CreateRoundedRectangle(
                        vignetteRectangle,
                        16))
                using (PathGradientBrush vignetteBrush =
                    new PathGradientBrush(
                        vignettePath))
                {
                    vignetteBrush.CenterColor =
                        Color.FromArgb(
                            0,
                            0,
                            0,
                            0);

                    vignetteBrush.SurroundColors =
                        new[]
                        {
            Color.FromArgb(
                135,
                0,
                0,
                0)
                        };

                    graphics.FillPath(
                        vignetteBrush,
                        vignettePath);
                }
            }

            Rectangle innerBezel =
                Rectangle.Inflate(
                    rectangle,
                    -8,
                    -8);

            using (GraphicsPath path =
                CreateRoundedRectangle(
                    innerBezel,
                    22))
            using (Pen shadowPen =
                new Pen(
                    Color.FromArgb(8, 10, 10),
                    4f))
            {
                graphics.DrawPath(
                    shadowPen,
                    path);
            }
        }

        private static void DrawGlass(
            Graphics graphics,
            Rectangle rectangle)
        {
            using (GraphicsPath path =
                CreateRoundedRectangle(
                    rectangle,
                    20))
            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    rectangle,
                    Color.FromArgb(15, 42, 57),
                    ApolloTheme.CrtBackground,
                    LinearGradientMode.Vertical))
            using (Pen rimPen =
                new Pen(
                    Color.FromArgb(48, 73, 82),
                    2f))
            {
                graphics.FillPath(
                    brush,
                    path);

                graphics.DrawPath(
                    rimPen,
                    path);
            }
        }

        private void DrawScreenContent(
             Graphics graphics,
             Rectangle glassRectangle)
        {
            Rectangle contentRectangle =
                Rectangle.Inflate(
                    glassRectangle,
                    -18,
                    -14);

            Color phosphorColor =
                GetPhosphorColor();

            Color dimColor =
                Color.FromArgb(
                    155,
                    phosphorColor);

            using (Font titleFont =
                ApolloTheme.CreateConsoleFont(
                    10f,
                    FontStyle.Bold))
            using (Font dataFont =
                ApolloTheme.CreateConsoleFont(
                    13f,
                    FontStyle.Regular))
            {
                MissionRenderContext context =
                    new MissionRenderContext(
                        graphics,
                        contentRectangle,
                        dataFont,
                        titleFont,
                        phosphorColor,
                        dimColor);

                if (_missionPage != null &&
                    _telemetry != null)
                {
                    _missionPage.Draw(
                        context,
                        _telemetry);
                }
            }
        }




        private static void DrawScanLines(
            Graphics graphics,
            Rectangle glassRectangle)
        {
            using (Pen scanLinePen =
                new Pen(
                    Color.FromArgb(
                        14,
                        0,
                        0,
                        0),
                    1f))
            {
                for (int y =
                    glassRectangle.Top + 3;
                    y < glassRectangle.Bottom - 2;
                    y += 4)
                {
                    graphics.DrawLine(
                        scanLinePen,
                        glassRectangle.Left + 4,
                        y,
                        glassRectangle.Right - 4,
                        y);
                }
            }
        }

        private static void DrawGlassReflection(
            Graphics graphics,
            Rectangle glassRectangle)
        {
            Rectangle reflectionRectangle =
                new Rectangle(
                    glassRectangle.Left + 18,
                    glassRectangle.Top + 10,
                    glassRectangle.Width - 36,
                    Math.Max(
                        12,
                        glassRectangle.Height / 5));

            using (GraphicsPath reflectionPath =
                CreateRoundedRectangle(
                    reflectionRectangle,
                    12))
            using (LinearGradientBrush reflectionBrush =
                new LinearGradientBrush(
                    reflectionRectangle,
                    Color.FromArgb(
                        28,
                        190,
                        225,
                        240),
                    Color.FromArgb(
                        0,
                        190,
                        225,
                        240),
                    LinearGradientMode.Vertical))
            {
                graphics.FillPath(
                    reflectionBrush,
                    reflectionPath);
            }
        }

        private Color GetPhosphorColor()
        {
            switch (_phosphorMode)
            {
                case CrtPhosphorMode.Green:
                    return ApolloTheme.CrtGreen;

                case CrtPhosphorMode.Amber:
                    return ApolloTheme.LampAmber;

                default:
                    return ApolloTheme.CrtBlue;
            }
        }

        private static GraphicsPath CreateRoundedRectangle(
            Rectangle rectangle,
            int radius)
        {
            GraphicsPath path =
                new GraphicsPath();

            int diameter =
                radius * 2;

            path.AddArc(
                rectangle.Left,
                rectangle.Top,
                diameter,
                diameter,
                180,
                90);

            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Top,
                diameter,
                diameter,
                270,
                90);

            path.AddArc(
                rectangle.Right - diameter,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                0,
                90);

            path.AddArc(
                rectangle.Left,
                rectangle.Bottom - diameter,
                diameter,
                diameter,
                90,
                90);

            path.CloseFigure();

            return path;
        }
    }
}