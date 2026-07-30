using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Themes;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace KMC.MissionControl.Controls
{
    public enum CrtPhosphorMode
    {
        Blue,
        Green,
        Amber
    }

    /// <summary>
    /// Apollo-inspired CRT display.
    ///
    /// Mission pages are rendered to a fixed 1280 x 720 virtual canvas.
    /// The finished canvas is then scaled into the CRT glass while
    /// preserving its aspect ratio.
    /// </summary>
    public sealed class MissionDisplay : Control
    {
        public const int VirtualWidth = 1280;
        public const int VirtualHeight = 720;

        private const int BezelInset = 18;
        private const int GlassContentInsetX = 22;
        private const int GlassContentInsetY = 18;

        private IMissionPage _missionPage;
        private MissionTelemetry _telemetry;
        private string _screenTitle;
        private CrtPhosphorMode _phosphorMode;
        private bool _showScanLines;
        private bool _showScalingDiagnostics;

        public MissionDisplay()
        {
            _missionPage = new OrbitPage();
            _telemetry = new MissionTelemetry();
            _screenTitle = "DATA DISPLAY";
            _phosphorMode = CrtPhosphorMode.Blue;
            _showScanLines = true;
            _showScalingDiagnostics = false;

            Width = 640;
            Height = 400;

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
                        : value.Trim().ToUpperInvariant();

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

        /// <summary>
        /// Draws the virtual-canvas dimensions and scale value.
        /// This is intended only for temporary testing.
        /// </summary>
        public bool ShowScalingDiagnostics
        {
            get
            {
                return _showScalingDiagnostics;
            }

            set
            {
                _showScalingDiagnostics = value;
                Invalidate();
            }
        }

        public Size VirtualCanvasSize
        {
            get
            {
                return new Size(
                    VirtualWidth,
                    VirtualHeight);
            }
        }

        public void SetPage(
            IMissionPage page)
        {
            if (page == null)
            {
                return;
            }

            _missionPage = page;
            Invalidate();
        }

        public void UpdateTelemetry(
            MissionTelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            _telemetry = telemetry;
            Invalidate();
        }

        /// <summary>
        /// Returns the physical client rectangle currently occupied
        /// by the scaled 16:9 virtual canvas.
        /// </summary>
        public RectangleF GetCanvasDestinationRectangle()
        {
            Rectangle glassRectangle =
                GetGlassRectangle();

            RectangleF viewport =
                RectangleF.Inflate(
                    glassRectangle,
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            return CalculateLetterboxRectangle(
                viewport,
                VirtualWidth,
                VirtualHeight);
        }

        /// <summary>
        /// Converts a physical mouse/control point into virtual
        /// 1280 x 720 coordinates.
        ///
        /// Returns false when the point falls within a letterbox area.
        /// </summary>
        public bool TryClientToVirtual(
            Point clientPoint,
            out PointF virtualPoint)
        {
            RectangleF destination =
                GetCanvasDestinationRectangle();

            if (destination.Width <= 0.0f ||
                destination.Height <= 0.0f ||
                !destination.Contains(clientPoint))
            {
                virtualPoint = PointF.Empty;
                return false;
            }

            float normalizedX =
                (clientPoint.X - destination.Left) /
                destination.Width;

            float normalizedY =
                (clientPoint.Y - destination.Top) /
                destination.Height;

            virtualPoint =
                new PointF(
                    normalizedX * VirtualWidth,
                    normalizedY * VirtualHeight);

            return true;
        }

        /// <summary>
        /// Converts a point on the virtual canvas into physical
        /// coordinates within this control.
        /// </summary>
        public PointF VirtualToClient(
            PointF virtualPoint)
        {
            RectangleF destination =
                GetCanvasDestinationRectangle();

            float x =
                destination.Left +
                virtualPoint.X /
                VirtualWidth *
                destination.Width;

            float y =
                destination.Top +
                virtualPoint.Y /
                VirtualHeight *
                destination.Height;

            return new PointF(x, y);
        }

        protected override void OnPaintBackground(
            PaintEventArgs e)
        {
            Color parentColor =
                Parent != null
                    ? Parent.BackColor
                    : ApolloTheme.ConsoleFace;

            e.Graphics.Clear(parentColor);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width < 8 ||
                Height < 8)
            {
                return;
            }

            e.Graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            e.Graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            Rectangle bezelRectangle =
                GetBezelRectangle();

            Rectangle glassRectangle =
                GetGlassRectangle();

            DrawBezel(
                e.Graphics,
                bezelRectangle);

            DrawGlass(
                e.Graphics,
                glassRectangle);

            DrawVirtualCanvas(
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

        private Rectangle GetBezelRectangle()
        {
            return new Rectangle(
                1,
                1,
                Math.Max(1, Width - 3),
                Math.Max(1, Height - 3));
        }

        private Rectangle GetGlassRectangle()
        {
            return new Rectangle(
                BezelInset,
                BezelInset,
                Math.Max(
                    1,
                    Width - BezelInset * 2 - 1),
                Math.Max(
                    1,
                    Height - BezelInset * 2 - 1));
        }

        private void DrawVirtualCanvas(
            Graphics targetGraphics,
            Rectangle glassRectangle)
        {
            RectangleF viewport =
                RectangleF.Inflate(
                    glassRectangle,
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            if (viewport.Width <= 1.0f ||
                viewport.Height <= 1.0f)
            {
                return;
            }

            RectangleF destinationRectangle =
                CalculateLetterboxRectangle(
                    viewport,
                    VirtualWidth,
                    VirtualHeight);

            using (Bitmap virtualCanvas =
                new Bitmap(
                    VirtualWidth,
                    VirtualHeight,
                    PixelFormat.Format32bppPArgb))
            {
                virtualCanvas.SetResolution(
                    96.0f,
                    96.0f);

                using (Graphics canvasGraphics =
                    Graphics.FromImage(
                        virtualCanvas))
                {
                    ConfigureCanvasGraphics(
                        canvasGraphics);

                    DrawCanvasBackground(
                        canvasGraphics);

                    DrawMissionPage(
                        canvasGraphics);

                    if (_showScalingDiagnostics)
                    {
                        DrawDiagnostics(
                            canvasGraphics,
                            destinationRectangle);
                    }
                }

                GraphicsState state =
                    targetGraphics.Save();

                try
                {
                    targetGraphics.SetClip(
                        glassRectangle);

                    targetGraphics.InterpolationMode =
                        InterpolationMode.HighQualityBicubic;

                    targetGraphics.CompositingQuality =
                        CompositingQuality.HighQuality;

                    targetGraphics.PixelOffsetMode =
                        PixelOffsetMode.HighQuality;

                    targetGraphics.DrawImage(
                        virtualCanvas,
                        destinationRectangle,
                        new RectangleF(
                            0.0f,
                            0.0f,
                            VirtualWidth,
                            VirtualHeight),
                        GraphicsUnit.Pixel);
                }
                finally
                {
                    targetGraphics.Restore(state);
                }
            }
        }

        private static void ConfigureCanvasGraphics(
            Graphics graphics)
        {
            graphics.Clear(
                Color.Transparent);

            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            graphics.CompositingMode =
                CompositingMode.SourceOver;

            graphics.CompositingQuality =
                CompositingQuality.HighQuality;

            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            graphics.TextRenderingHint =
                System.Drawing.Text
                    .TextRenderingHint
                    .SingleBitPerPixelGridFit;
        }

        private static void DrawCanvasBackground(
            Graphics graphics)
        {
            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    new Rectangle(
                        0,
                        0,
                        VirtualWidth,
                        VirtualHeight),
                    Color.FromArgb(
                        255,
                        6,
                        20,
                        27),
                    Color.FromArgb(
                        255,
                        2,
                        10,
                        14),
                    LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(
                    brush,
                    0,
                    0,
                    VirtualWidth,
                    VirtualHeight);
            }
        }

        private void DrawMissionPage(
            Graphics graphics)
        {
            Color phosphorColor =
                GetPhosphorColor();

            Color dimColor =
                Color.FromArgb(
                    155,
                    phosphorColor);

            Rectangle contentRectangle =
                new Rectangle(
                    42,
                    34,
                    VirtualWidth - 84,
                    VirtualHeight - 68);

            using (Font largeFont =
                ApolloTheme.CreateConsoleFont(
                    27.0f,
                    FontStyle.Regular))
            using (Font smallFont =
                ApolloTheme.CreateConsoleFont(
                    21.0f,
                    FontStyle.Bold))
            {
                MissionRenderContext context =
                    new MissionRenderContext(
                        graphics,
                        contentRectangle,
                        largeFont,
                        smallFont,
                        phosphorColor,
                        dimColor,
                        new Size(
                            VirtualWidth,
                            VirtualHeight));

                if (_missionPage != null &&
                    _telemetry != null)
                {
                    _missionPage.Draw(
                        context,
                        _telemetry);
                }
            }
        }

        private void DrawDiagnostics(
            Graphics graphics,
            RectangleF destinationRectangle)
        {
            string diagnosticText =
                "VIRTUAL 1280 X 720  |  DISPLAY "
                + destinationRectangle.Width.ToString("0")
                + " X "
                + destinationRectangle.Height.ToString("0");

            using (Font font =
                ApolloTheme.CreateConsoleFont(
                    16.0f,
                    FontStyle.Regular))
            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        GetPhosphorColor())))
            {
                graphics.DrawString(
                    diagnosticText,
                    font,
                    brush,
                    42.0f,
                    VirtualHeight - 28.0f);
            }
        }

        private static RectangleF
            CalculateLetterboxRectangle(
                RectangleF viewport,
                float sourceWidth,
                float sourceHeight)
        {
            if (viewport.Width <= 0.0f ||
                viewport.Height <= 0.0f ||
                sourceWidth <= 0.0f ||
                sourceHeight <= 0.0f)
            {
                return RectangleF.Empty;
            }

            float horizontalScale =
                viewport.Width /
                sourceWidth;

            float verticalScale =
                viewport.Height /
                sourceHeight;

            float scale =
                Math.Min(
                    horizontalScale,
                    verticalScale);

            float width =
                sourceWidth * scale;

            float height =
                sourceHeight * scale;

            float left =
                viewport.Left +
                (viewport.Width - width) /
                2.0f;

            float top =
                viewport.Top +
                (viewport.Height - height) /
                2.0f;

            return new RectangleF(
                left,
                top,
                width,
                height);
        }

        private static void DrawBezel(
            Graphics graphics,
            Rectangle rectangle)
        {
            if (rectangle.Width <= 1 ||
                rectangle.Height <= 1)
            {
                return;
            }

            using (GraphicsPath path =
                CreateRoundedRectangle(
                    rectangle,
                    28))
            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    rectangle,
                    Color.FromArgb(
                        78,
                        84,
                        79),
                    Color.FromArgb(
                        25,
                        29,
                        28),
                    LinearGradientMode.Vertical))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        115,
                        121,
                        112),
                    2.0f))
            {
                graphics.FillPath(
                    brush,
                    path);

                graphics.DrawPath(
                    borderPen,
                    path);
            }

            Rectangle innerBezel =
                Rectangle.Inflate(
                    rectangle,
                    -8,
                    -8);

            if (innerBezel.Width <= 1 ||
                innerBezel.Height <= 1)
            {
                return;
            }

            using (GraphicsPath path =
                CreateRoundedRectangle(
                    innerBezel,
                    22))
            using (Pen shadowPen =
                new Pen(
                    Color.FromArgb(
                        8,
                        10,
                        10),
                    4.0f))
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
            if (rectangle.Width <= 1 ||
                rectangle.Height <= 1)
            {
                return;
            }

            using (GraphicsPath path =
                CreateRoundedRectangle(
                    rectangle,
                    20))
            using (LinearGradientBrush brush =
                new LinearGradientBrush(
                    rectangle,
                    Color.FromArgb(
                        15,
                        42,
                        57),
                    ApolloTheme.CrtBackground,
                    LinearGradientMode.Vertical))
            using (Pen rimPen =
                new Pen(
                    Color.FromArgb(
                        48,
                        73,
                        82),
                    2.0f))
            {
                graphics.FillPath(
                    brush,
                    path);

                graphics.DrawPath(
                    rimPen,
                    path);
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
                    1.0f))
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
                    Math.Max(
                        1,
                        glassRectangle.Width - 36),
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

        private static GraphicsPath
            CreateRoundedRectangle(
                Rectangle rectangle,
                int radius)
        {
            GraphicsPath path =
                new GraphicsPath();

            if (rectangle.Width <= 0 ||
                rectangle.Height <= 0)
            {
                return path;
            }

            int maximumRadius =
                Math.Min(
                    rectangle.Width,
                    rectangle.Height) /
                2;

            int safeRadius =
                Math.Max(
                    1,
                    Math.Min(
                        radius,
                        maximumRadius));

            int diameter =
                safeRadius * 2;

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