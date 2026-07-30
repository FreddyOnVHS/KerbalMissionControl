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
    /// Mission pages render to a virtual canvas with a fixed height of
    /// 720 units. The canvas has a minimum width of 1280 units and expands
    /// horizontally when the physical CRT becomes wider than 16:9.
    /// </summary>
    public sealed class MissionDisplay : Control
    {
        public const int MinimumVirtualWidth = 1280;
        public const int VirtualHeight = 720;
        public const int VirtualWidth = MinimumVirtualWidth;

        private const int BezelInset = 18;
        private const int GlassContentInsetX = 22;
        private const int GlassContentInsetY = 18;
        private const int MaximumVirtualWidth = 4096;

        private IMissionPage _missionPage;
        private MissionTelemetry _telemetry;
        private string _screenTitle;
        private CrtPhosphorMode _phosphorMode;
        private bool _showScanLines;
        private bool _showScalingDiagnostics;
        private int _currentVirtualWidth;

        private struct CanvasLayout
        {
            public int VirtualWidth;
            public RectangleF DestinationRectangle;
            public float Scale;
        }

        public MissionDisplay()
        {
            _missionPage = new OrbitPage();
            _telemetry = new MissionTelemetry();
            _screenTitle = "DATA DISPLAY";
            _phosphorMode = CrtPhosphorMode.Blue;
            _showScanLines = true;
            _showScalingDiagnostics = false;
            _currentVirtualWidth = MinimumVirtualWidth;

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
            get { return _screenTitle; }
            set
            {
                _screenTitle = string.IsNullOrWhiteSpace(value)
                    ? "DATA DISPLAY"
                    : value.Trim().ToUpperInvariant();
                Invalidate();
            }
        }

        public CrtPhosphorMode PhosphorMode
        {
            get { return _phosphorMode; }
            set
            {
                _phosphorMode = value;
                Invalidate();
            }
        }

        public bool ShowScanLines
        {
            get { return _showScanLines; }
            set
            {
                _showScanLines = value;
                Invalidate();
            }
        }

        public bool ShowScalingDiagnostics
        {
            get { return _showScalingDiagnostics; }
            set
            {
                _showScalingDiagnostics = value;
                Invalidate();
            }
        }

        public Size VirtualCanvasSize
        {
            get { return new Size(_currentVirtualWidth, VirtualHeight); }
        }

        public void SetPage(IMissionPage page)
        {
            if (page == null) return;
            _missionPage = page;
            Invalidate();
        }

        public void UpdateTelemetry(MissionTelemetry telemetry)
        {
            if (telemetry == null) return;
            _telemetry = telemetry;
            Invalidate();
        }

        public RectangleF GetCanvasDestinationRectangle()
        {
            RectangleF viewport = RectangleF.Inflate(
                GetGlassRectangle(),
                -GlassContentInsetX,
                -GlassContentInsetY);

            return CalculateCanvasLayout(viewport).DestinationRectangle;
        }

        public bool TryClientToVirtual(Point clientPoint, out PointF virtualPoint)
        {
            RectangleF viewport = RectangleF.Inflate(
                GetGlassRectangle(),
                -GlassContentInsetX,
                -GlassContentInsetY);

            CanvasLayout layout = CalculateCanvasLayout(viewport);
            RectangleF destination = layout.DestinationRectangle;

            if (destination.Width <= 0.0f ||
                destination.Height <= 0.0f ||
                !destination.Contains(clientPoint))
            {
                virtualPoint = PointF.Empty;
                return false;
            }

            float normalizedX = (clientPoint.X - destination.Left) / destination.Width;
            float normalizedY = (clientPoint.Y - destination.Top) / destination.Height;

            virtualPoint = new PointF(
                normalizedX * layout.VirtualWidth,
                normalizedY * VirtualHeight);

            return true;
        }

        public PointF VirtualToClient(PointF virtualPoint)
        {
            RectangleF viewport = RectangleF.Inflate(
                GetGlassRectangle(),
                -GlassContentInsetX,
                -GlassContentInsetY);

            CanvasLayout layout = CalculateCanvasLayout(viewport);
            RectangleF destination = layout.DestinationRectangle;

            float x = destination.Left +
                virtualPoint.X / layout.VirtualWidth * destination.Width;

            float y = destination.Top +
                virtualPoint.Y / VirtualHeight * destination.Height;

            return new PointF(x, y);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color parentColor = Parent != null
                ? Parent.BackColor
                : ApolloTheme.ConsoleFace;

            e.Graphics.Clear(parentColor);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (Width < 8 || Height < 8) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle bezelRectangle = GetBezelRectangle();
            Rectangle glassRectangle = GetGlassRectangle();

            DrawBezel(e.Graphics, bezelRectangle);
            DrawGlass(e.Graphics, glassRectangle);
            DrawVirtualCanvas(e.Graphics, glassRectangle);

            if (_showScanLines)
            {
                DrawScanLines(e.Graphics, glassRectangle);
            }

            DrawGlassReflection(e.Graphics, glassRectangle);
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
                Math.Max(1, Width - BezelInset * 2 - 1),
                Math.Max(1, Height - BezelInset * 2 - 1));
        }

        private void DrawVirtualCanvas(Graphics targetGraphics, Rectangle glassRectangle)
        {
            RectangleF viewport = RectangleF.Inflate(
                glassRectangle,
                -GlassContentInsetX,
                -GlassContentInsetY);

            if (viewport.Width <= 1.0f || viewport.Height <= 1.0f) return;

            CanvasLayout layout = CalculateCanvasLayout(viewport);

            if (layout.VirtualWidth <= 0 ||
                layout.DestinationRectangle.Width <= 0.0f ||
                layout.DestinationRectangle.Height <= 0.0f)
            {
                return;
            }

            _currentVirtualWidth = layout.VirtualWidth;

            using (Bitmap virtualCanvas = new Bitmap(
                layout.VirtualWidth,
                VirtualHeight,
                PixelFormat.Format32bppPArgb))
            {
                virtualCanvas.SetResolution(96.0f, 96.0f);

                using (Graphics canvasGraphics = Graphics.FromImage(virtualCanvas))
                {
                    ConfigureCanvasGraphics(canvasGraphics);
                    DrawCanvasBackground(canvasGraphics, layout.VirtualWidth);
                    DrawMissionPage(canvasGraphics, layout.VirtualWidth);

                    if (_showScalingDiagnostics)
                    {
                        DrawDiagnostics(canvasGraphics, layout);
                    }
                }

                GraphicsState state = targetGraphics.Save();

                try
                {
                    targetGraphics.SetClip(glassRectangle);
                    targetGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    targetGraphics.CompositingQuality = CompositingQuality.HighQuality;
                    targetGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    targetGraphics.DrawImage(
                        virtualCanvas,
                        layout.DestinationRectangle,
                        new RectangleF(0.0f, 0.0f, layout.VirtualWidth, VirtualHeight),
                        GraphicsUnit.Pixel);
                }
                finally
                {
                    targetGraphics.Restore(state);
                }
            }
        }

        private static void ConfigureCanvasGraphics(Graphics graphics)
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
        }

        private static void DrawCanvasBackground(Graphics graphics, int virtualWidth)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Rectangle(0, 0, virtualWidth, VirtualHeight),
                Color.FromArgb(255, 6, 20, 27),
                Color.FromArgb(255, 2, 10, 14),
                LinearGradientMode.Vertical))
            {
                graphics.FillRectangle(brush, 0, 0, virtualWidth, VirtualHeight);
            }
        }

        private void DrawMissionPage(Graphics graphics, int virtualWidth)
        {
            Color phosphorColor = GetPhosphorColor();
            Color dimColor = Color.FromArgb(155, phosphorColor);

            Rectangle contentRectangle = new Rectangle(
                42,
                34,
                MinimumVirtualWidth - 84,
                VirtualHeight - 68);

            using (Font largeFont = ApolloTheme.CreateConsoleFont(27.0f, FontStyle.Regular))
            using (Font smallFont = ApolloTheme.CreateConsoleFont(21.0f, FontStyle.Bold))
            {
                MissionRenderContext context = new MissionRenderContext(
                    graphics,
                    contentRectangle,
                    largeFont,
                    smallFont,
                    phosphorColor,
                    dimColor,
                    new Size(virtualWidth, VirtualHeight));

                if (_missionPage != null && _telemetry != null)
                {
                    _missionPage.Draw(context, _telemetry);
                }
            }
        }

        private void DrawDiagnostics(Graphics graphics, CanvasLayout layout)
        {
            string diagnosticText =
                "VIRTUAL " + layout.VirtualWidth + " X " + VirtualHeight +
                "  |  SCALE " + layout.Scale.ToString("0.000");

            using (Font font = ApolloTheme.CreateConsoleFont(16.0f, FontStyle.Regular))
            using (SolidBrush brush = new SolidBrush(
                Color.FromArgb(180, GetPhosphorColor())))
            {
                graphics.DrawString(
                    diagnosticText,
                    font,
                    brush,
                    42.0f,
                    VirtualHeight - 28.0f);
            }
        }

        private static CanvasLayout CalculateCanvasLayout(RectangleF viewport)
        {
            CanvasLayout result = new CanvasLayout
            {
                VirtualWidth = MinimumVirtualWidth,
                DestinationRectangle = RectangleF.Empty,
                Scale = 0.0f
            };

            if (viewport.Width <= 0.0f || viewport.Height <= 0.0f)
            {
                return result;
            }

            float minimumAspect = (float)MinimumVirtualWidth / VirtualHeight;
            float viewportAspect = viewport.Width / viewport.Height;

            if (viewportAspect <= minimumAspect)
            {
                float scale = Math.Min(
                    viewport.Width / MinimumVirtualWidth,
                    viewport.Height / VirtualHeight);

                float destinationWidth = MinimumVirtualWidth * scale;
                float destinationHeight = VirtualHeight * scale;

                result.Scale = scale;
                result.DestinationRectangle = new RectangleF(
                    viewport.Left + (viewport.Width - destinationWidth) / 2.0f,
                    viewport.Top + (viewport.Height - destinationHeight) / 2.0f,
                    destinationWidth,
                    destinationHeight);

                return result;
            }

            float wideScale = viewport.Height / VirtualHeight;

            if (wideScale <= 0.0f)
            {
                return result;
            }

            int expandedVirtualWidth = (int)Math.Ceiling(
                viewport.Width / wideScale);

            expandedVirtualWidth = Math.Max(
                MinimumVirtualWidth,
                expandedVirtualWidth);

            expandedVirtualWidth = Math.Min(
                MaximumVirtualWidth,
                expandedVirtualWidth);

            float renderedWidth = expandedVirtualWidth * wideScale;

            result.VirtualWidth = expandedVirtualWidth;
            result.Scale = wideScale;
            result.DestinationRectangle = new RectangleF(
                viewport.Left + (viewport.Width - renderedWidth) / 2.0f,
                viewport.Top,
                renderedWidth,
                viewport.Height);

            return result;
        }

        private static void DrawBezel(Graphics graphics, Rectangle rectangle)
        {
            if (rectangle.Width <= 1 || rectangle.Height <= 1) return;

            using (GraphicsPath path = CreateRoundedRectangle(rectangle, 28))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rectangle,
                Color.FromArgb(78, 84, 79),
                Color.FromArgb(25, 29, 28),
                LinearGradientMode.Vertical))
            using (Pen borderPen = new Pen(
                Color.FromArgb(115, 121, 112),
                2.0f))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(borderPen, path);
            }

            Rectangle innerBezel = Rectangle.Inflate(rectangle, -8, -8);

            if (innerBezel.Width <= 1 || innerBezel.Height <= 1) return;

            using (GraphicsPath path = CreateRoundedRectangle(innerBezel, 22))
            using (Pen shadowPen = new Pen(Color.FromArgb(8, 10, 10), 4.0f))
            {
                graphics.DrawPath(shadowPen, path);
            }
        }

        private static void DrawGlass(Graphics graphics, Rectangle rectangle)
        {
            if (rectangle.Width <= 1 || rectangle.Height <= 1) return;

            using (GraphicsPath path = CreateRoundedRectangle(rectangle, 20))
            using (LinearGradientBrush brush = new LinearGradientBrush(
                rectangle,
                Color.FromArgb(15, 42, 57),
                ApolloTheme.CrtBackground,
                LinearGradientMode.Vertical))
            using (Pen rimPen = new Pen(Color.FromArgb(48, 73, 82), 2.0f))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(rimPen, path);
            }
        }

        private static void DrawScanLines(Graphics graphics, Rectangle glassRectangle)
        {
            using (Pen scanLinePen = new Pen(
                Color.FromArgb(14, 0, 0, 0),
                1.0f))
            {
                for (int y = glassRectangle.Top + 3;
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
            Rectangle reflectionRectangle = new Rectangle(
                glassRectangle.Left + 18,
                glassRectangle.Top + 10,
                Math.Max(1, glassRectangle.Width - 36),
                Math.Max(12, glassRectangle.Height / 5));

            using (GraphicsPath reflectionPath =
                CreateRoundedRectangle(reflectionRectangle, 12))
            using (LinearGradientBrush reflectionBrush =
                new LinearGradientBrush(
                    reflectionRectangle,
                    Color.FromArgb(28, 190, 225, 240),
                    Color.FromArgb(0, 190, 225, 240),
                    LinearGradientMode.Vertical))
            {
                graphics.FillPath(reflectionBrush, reflectionPath);
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
            GraphicsPath path = new GraphicsPath();

            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                return path;
            }

            int maximumRadius = Math.Min(
                rectangle.Width,
                rectangle.Height) / 2;

            int safeRadius = Math.Max(
                1,
                Math.Min(radius, maximumRadius));

            int diameter = safeRadius * 2;

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