using KMC.MissionControl.Models;
using KMC.MissionControl.Pages;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Themes;
using System;
using System.Diagnostics;
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
    /// Apollo-inspired CRT display with a persistent virtual canvas.
    ///
    /// Mission-page rendering is intentionally separated from presentation.
    /// Expensive page drawing occurs only when telemetry, page content, theme,
    /// diagnostics, or virtual-canvas dimensions change. Normal WinForms paint
    /// messages simply present the most recently completed bitmap.
    /// </summary>
    public sealed class MissionDisplay : Control
    {
        public const int MinimumVirtualWidth = 1280;
        public const int MinimumVirtualHeight = 720;

        public const int VirtualWidth = MinimumVirtualWidth;
        public const int VirtualHeight = MinimumVirtualHeight;

        private const int BezelInset = 18;
        private const int GlassContentInsetX = 22;
        private const int GlassContentInsetY = 18;
        private const int MaximumVirtualWidth = 4096;
        private const int MaximumVirtualHeight = 2304;
        private const float MaximumCanvasScale = 0.625f;

        private IMissionPage _missionPage;
        private MissionTelemetry _telemetry;
        private string _screenTitle;
        private CrtPhosphorMode _phosphorMode;
        private bool _showScanLines;
        private bool _showScalingDiagnostics;

        private int _currentVirtualWidth;
        private int _currentVirtualHeight;

        private Bitmap _cachedCanvas;
        private Size _cachedCanvasSize;
        private bool _canvasDirty;
        private bool _renderingSuspended;

        private readonly Font _largePageFont;
        private readonly Font _smallPageFont;
        private readonly Font _diagnosticFont;

        private long _renderCount;
        private double _lastRenderMilliseconds;

        private struct CanvasLayout
        {
            public int VirtualWidth;
            public int VirtualHeight;
            public RectangleF DestinationRectangle;
            public float Scale;
        }

        public MissionDisplay()
        {
            _missionPage =
                new OrbitPage();

            _telemetry =
                new MissionTelemetry();

            _screenTitle =
                "DATA DISPLAY";

            _phosphorMode =
                CrtPhosphorMode.Blue;

            _showScanLines =
                true;

            _showScalingDiagnostics =
                false;

            _currentVirtualWidth =
                MinimumVirtualWidth;

            _currentVirtualHeight =
                MinimumVirtualHeight;

            _cachedCanvasSize =
                Size.Empty;

            _canvasDirty =
                true;

            _largePageFont =
                ApolloTheme.CreateConsoleFont(
                    27.0f,
                    FontStyle.Regular);

            _smallPageFont =
                ApolloTheme.CreateConsoleFont(
                    21.0f,
                    FontStyle.Bold);

            _diagnosticFont =
                ApolloTheme.CreateConsoleFont(
                    16.0f,
                    FontStyle.Regular);

            Width = 640;
            Height = 400;

            BackColor =
                ApolloTheme.ConsoleFace;

            ForeColor =
                ApolloTheme.CrtBlue;

            DoubleBuffered =
                true;

            ResizeRedraw =
                true;

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
                string normalized =
                    string.IsNullOrWhiteSpace(value)
                        ? "DATA DISPLAY"
                        : value
                            .Trim()
                            .ToUpperInvariant();

                if (string.Equals(
                    _screenTitle,
                    normalized,
                    StringComparison.Ordinal))
                {
                    return;
                }

                _screenTitle =
                    normalized;

                MarkCanvasDirty();
            }
        }

        public CrtPhosphorMode PhosphorMode
        {
            get { return _phosphorMode; }
            set
            {
                if (_phosphorMode == value)
                {
                    return;
                }

                _phosphorMode =
                    value;

                MarkCanvasDirty();
            }
        }

        public bool ShowScanLines
        {
            get { return _showScanLines; }
            set
            {
                if (_showScanLines == value)
                {
                    return;
                }

                _showScanLines =
                    value;

                /*
                 * Scanlines are a presentation layer and do not require the
                 * mission-page bitmap to be rebuilt.
                 */
                Invalidate();
            }
        }

        public bool ShowScalingDiagnostics
        {
            get { return _showScalingDiagnostics; }
            set
            {
                if (_showScalingDiagnostics == value)
                {
                    return;
                }

                _showScalingDiagnostics =
                    value;

                MarkCanvasDirty();
            }
        }

        public Size VirtualCanvasSize
        {
            get
            {
                return new Size(
                    _currentVirtualWidth,
                    _currentVirtualHeight);
            }
        }

        public long RenderCount
        {
            get { return _renderCount; }
        }

        public double LastRenderMilliseconds
        {
            get { return _lastRenderMilliseconds; }
        }

        public void SetPage(
            IMissionPage page)
        {
            if (page == null ||
                ReferenceEquals(
                    _missionPage,
                    page))
            {
                return;
            }

            _missionPage =
                page;

            MarkCanvasDirty();
        }

        public void UpdateTelemetry(
            MissionTelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            _telemetry =
                telemetry;

            MarkCanvasDirty();
        }

        /// <summary>
        /// Prevents expensive mission-page rendering while allowing paint
        /// messages to present the last completed cached frame.
        /// </summary>
        public void SuspendRendering()
        {
            _renderingSuspended =
                true;
        }

        public void ResumeRendering(
            bool renderImmediately)
        {
            _renderingSuspended =
                false;

            _canvasDirty =
                true;

            if (renderImmediately)
            {
                RenderCurrentCanvas();
            }

            Invalidate();
        }

        public void RequestRender()
        {
            MarkCanvasDirty();
        }

        public RectangleF GetCanvasDestinationRectangle()
        {
            RectangleF viewport =
                RectangleF.Inflate(
                    GetGlassRectangle(),
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            return CalculateCanvasLayout(
                viewport)
                .DestinationRectangle;
        }

        public bool TryClientToVirtual(
            Point clientPoint,
            out PointF virtualPoint)
        {
            RectangleF viewport =
                RectangleF.Inflate(
                    GetGlassRectangle(),
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            CanvasLayout layout =
                CalculateCanvasLayout(
                    viewport);

            RectangleF destination =
                layout.DestinationRectangle;

            if (destination.Width <= 0.0f ||
                destination.Height <= 0.0f ||
                !destination.Contains(
                    clientPoint))
            {
                virtualPoint =
                    PointF.Empty;

                return false;
            }

            float normalizedX =
                (clientPoint.X -
                 destination.Left) /
                destination.Width;

            float normalizedY =
                (clientPoint.Y -
                 destination.Top) /
                destination.Height;

            virtualPoint =
                new PointF(
                    normalizedX *
                    layout.VirtualWidth,

                    normalizedY *
                    layout.VirtualHeight);

            return true;
        }

        public PointF VirtualToClient(
            PointF virtualPoint)
        {
            RectangleF viewport =
                RectangleF.Inflate(
                    GetGlassRectangle(),
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            CanvasLayout layout =
                CalculateCanvasLayout(
                    viewport);

            RectangleF destination =
                layout.DestinationRectangle;

            float x =
                destination.Left +
                virtualPoint.X /
                layout.VirtualWidth *
                destination.Width;

            float y =
                destination.Top +
                virtualPoint.Y /
                layout.VirtualHeight *
                destination.Height;

            return new PointF(
                x,
                y);
        }

        protected override void OnSizeChanged(
            EventArgs e)
        {
            base.OnSizeChanged(
                e);

            /*
             * A size change may require a different virtual-canvas size.
             * During interactive resize, keep presenting the existing bitmap
             * and defer the rebuild until rendering resumes.
             */
            _canvasDirty =
                true;

            Invalidate();
        }

        protected override void OnPaintBackground(
            PaintEventArgs e)
        {
            Color parentColor =
                Parent != null
                    ? Parent.BackColor
                    : ApolloTheme.ConsoleFace;

            e.Graphics.Clear(
                parentColor);
        }

        protected override void OnPaint(
            PaintEventArgs e)
        {
            base.OnPaint(
                e);

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

        protected override void Dispose(
            bool disposing)
        {
            if (disposing)
            {
                DisposeCachedCanvas();

                _largePageFont.Dispose();
                _smallPageFont.Dispose();
                _diagnosticFont.Dispose();
            }

            base.Dispose(
                disposing);
        }

        private void MarkCanvasDirty()
        {
            _canvasDirty =
                true;

            /*
             * While rendering is suspended, do not create extra paint work.
             * The last completed bitmap remains available for window moves.
             */
            if (!_renderingSuspended)
            {
                Invalidate();
            }
        }

        private Rectangle GetBezelRectangle()
        {
            return new Rectangle(
                1,
                1,
                Math.Max(
                    1,
                    Width - 3),
                Math.Max(
                    1,
                    Height - 3));
        }

        private Rectangle GetGlassRectangle()
        {
            return new Rectangle(
                BezelInset,
                BezelInset,
                Math.Max(
                    1,
                    Width -
                    BezelInset * 2 -
                    1),
                Math.Max(
                    1,
                    Height -
                    BezelInset * 2 -
                    1));
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

            DrawViewportBackground(
                targetGraphics,
                viewport);

            CanvasLayout layout =
                CalculateCanvasLayout(
                    viewport);

            if (layout.VirtualWidth <= 0 ||
                layout.VirtualHeight <= 0 ||
                layout.DestinationRectangle.Width <= 0.0f ||
                layout.DestinationRectangle.Height <= 0.0f)
            {
                return;
            }

            _currentVirtualWidth =
                layout.VirtualWidth;

            _currentVirtualHeight =
                layout.VirtualHeight;

            bool cacheSizeChanged =
                _cachedCanvas == null ||
                _cachedCanvasSize.Width !=
                    layout.VirtualWidth ||
                _cachedCanvasSize.Height !=
                    layout.VirtualHeight;

            if (!_renderingSuspended &&
                (_canvasDirty ||
                 cacheSizeChanged))
            {
                RenderCachedCanvas(
                    layout);
            }

            if (_cachedCanvas == null)
            {
                return;
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

                RectangleF sourceRectangle =
                    new RectangleF(
                        0.0f,
                        0.0f,
                        _cachedCanvas.Width,
                        _cachedCanvas.Height);

                targetGraphics.DrawImage(
                    _cachedCanvas,
                    layout.DestinationRectangle,
                    sourceRectangle,
                    GraphicsUnit.Pixel);
            }
            finally
            {
                targetGraphics.Restore(
                    state);
            }
        }

        private void RenderCurrentCanvas()
        {
            if (_renderingSuspended ||
                Width < 8 ||
                Height < 8)
            {
                return;
            }

            RectangleF viewport =
                RectangleF.Inflate(
                    GetGlassRectangle(),
                    -GlassContentInsetX,
                    -GlassContentInsetY);

            CanvasLayout layout =
                CalculateCanvasLayout(
                    viewport);

            if (layout.VirtualWidth <= 0 ||
                layout.VirtualHeight <= 0)
            {
                return;
            }

            _currentVirtualWidth =
                layout.VirtualWidth;

            _currentVirtualHeight =
                layout.VirtualHeight;

            RenderCachedCanvas(
                layout);
        }

        private void RenderCachedCanvas(
            CanvasLayout layout)
        {
            Stopwatch stopwatch =
                Stopwatch.StartNew();

            EnsureCachedCanvas(
                layout.VirtualWidth,
                layout.VirtualHeight);

            using (Graphics canvasGraphics =
                Graphics.FromImage(
                    _cachedCanvas))
            {
                ConfigureCanvasGraphics(
                    canvasGraphics);

                DrawMissionPage(
                    canvasGraphics,
                    layout.VirtualWidth,
                    layout.VirtualHeight);

                if (_showScalingDiagnostics)
                {
                    DrawDiagnostics(
                        canvasGraphics,
                        layout);
                }
            }

            _canvasDirty =
                false;

            _renderCount++;

            stopwatch.Stop();

            _lastRenderMilliseconds =
                stopwatch.Elapsed.TotalMilliseconds;
        }

        private void EnsureCachedCanvas(
            int width,
            int height)
        {
            if (_cachedCanvas != null &&
                _cachedCanvasSize.Width == width &&
                _cachedCanvasSize.Height == height)
            {
                return;
            }

            DisposeCachedCanvas();

            _cachedCanvas =
                new Bitmap(
                    width,
                    height,
                    PixelFormat.Format32bppPArgb);

            _cachedCanvas.SetResolution(
                96.0f,
                96.0f);

            _cachedCanvasSize =
                new Size(
                    width,
                    height);
        }

        private void DisposeCachedCanvas()
        {
            if (_cachedCanvas != null)
            {
                _cachedCanvas.Dispose();
                _cachedCanvas = null;
            }

            _cachedCanvasSize =
                Size.Empty;
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

        private static void DrawViewportBackground(
            Graphics graphics,
            RectangleF viewport)
        {
            if (viewport.Width <= 0.0f ||
                viewport.Height <= 0.0f)
            {
                return;
            }

            GraphicsState state =
                graphics.Save();

            try
            {
                graphics.SetClip(
                    viewport);

                using (LinearGradientBrush brush =
                    new LinearGradientBrush(
                        viewport,
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
                        viewport);
                }
            }
            finally
            {
                graphics.Restore(
                    state);
            }
        }

        private void DrawMissionPage(
            Graphics graphics,
            int virtualWidth,
            int virtualHeight)
        {
            Color phosphorColor =
                GetPhosphorColor();

            Color dimColor =
                Color.FromArgb(
                    155,
                    phosphorColor);

            const float PageExpansionFactor =
                1.30f;

            int baselineContentWidth =
                MinimumVirtualWidth -
                84;

            int baselineContentHeight =
                MinimumVirtualHeight -
                68;

            int requestedContentWidth =
                (int)Math.Round(
                    baselineContentWidth *
                    PageExpansionFactor);

            int requestedContentHeight =
                (int)Math.Round(
                    baselineContentHeight *
                    PageExpansionFactor);

            int horizontalMargin =
                28;

            int verticalMargin =
                24;

            int availableWidth =
                Math.Max(
                    1,
                    virtualWidth -
                    horizontalMargin * 2);

            int availableHeight =
                Math.Max(
                    1,
                    virtualHeight -
                    verticalMargin * 2);

            int contentWidth =
                Math.Min(
                    requestedContentWidth,
                    availableWidth);

            int contentHeight =
                Math.Min(
                    requestedContentHeight,
                    availableHeight);

            int contentLeft =
                Math.Max(
                    horizontalMargin,
                    (virtualWidth -
                     contentWidth) /
                    2);

            int contentTop =
                Math.Max(
                    verticalMargin,
                    (virtualHeight -
                     contentHeight) /
                    2);

            Rectangle contentRectangle =
                new Rectangle(
                    contentLeft,
                    contentTop,
                    contentWidth,
                    contentHeight);

            MissionRenderContext context =
                new MissionRenderContext(
                    graphics,
                    contentRectangle,
                    _largePageFont,
                    _smallPageFont,
                    phosphorColor,
                    dimColor,
                    new Size(
                        virtualWidth,
                        virtualHeight));

            if (_missionPage != null &&
                _telemetry != null)
            {
                _missionPage.Draw(
                    context,
                    _telemetry);
            }
        }

        private void DrawDiagnostics(
            Graphics graphics,
            CanvasLayout layout)
        {
            string diagnosticText =
                "VIRTUAL " +
                layout.VirtualWidth +
                " X " +
                layout.VirtualHeight +
                "  |  SCALE " +
                layout.Scale.ToString(
                    "0.000") +
                "  |  RENDER " +
                _lastRenderMilliseconds.ToString(
                    "0.0") +
                " MS";

            using (SolidBrush brush =
                new SolidBrush(
                    Color.FromArgb(
                        180,
                        GetPhosphorColor())))
            {
                graphics.DrawString(
                    diagnosticText,
                    _diagnosticFont,
                    brush,
                    42.0f,
                    layout.VirtualHeight -
                    28.0f);
            }
        }

        private static CanvasLayout CalculateCanvasLayout(
            RectangleF viewport)
        {
            CanvasLayout result =
                new CanvasLayout
                {
                    VirtualWidth =
                        MinimumVirtualWidth,

                    VirtualHeight =
                        MinimumVirtualHeight,

                    DestinationRectangle =
                        RectangleF.Empty,

                    Scale =
                        0.0f
                };

            if (viewport.Width <= 0.0f ||
                viewport.Height <= 0.0f)
            {
                return result;
            }

            float widthScale =
                viewport.Width /
                MinimumVirtualWidth;

            float heightScale =
                viewport.Height /
                MinimumVirtualHeight;

            float scale =
                Math.Min(
                    MaximumCanvasScale,
                    Math.Min(
                        widthScale,
                        heightScale));

            if (scale <= 0.0f)
            {
                return result;
            }

            int virtualWidth =
                (int)Math.Ceiling(
                    viewport.Width /
                    scale);

            int virtualHeight =
                (int)Math.Ceiling(
                    viewport.Height /
                    scale);

            virtualWidth =
                Math.Max(
                    MinimumVirtualWidth,
                    Math.Min(
                        MaximumVirtualWidth,
                        virtualWidth));

            virtualHeight =
                Math.Max(
                    MinimumVirtualHeight,
                    Math.Min(
                        MaximumVirtualHeight,
                        virtualHeight));

            float destinationWidth =
                virtualWidth *
                scale;

            float destinationHeight =
                virtualHeight *
                scale;

            result.VirtualWidth =
                virtualWidth;

            result.VirtualHeight =
                virtualHeight;

            result.Scale =
                scale;

            result.DestinationRectangle =
                new RectangleF(
                    viewport.Left +
                    (viewport.Width -
                     destinationWidth) /
                    2.0f,

                    viewport.Top +
                    (viewport.Height -
                     destinationHeight) /
                    2.0f,

                    destinationWidth,
                    destinationHeight);

            return result;
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
                     y <
                         glassRectangle.Bottom - 2;
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

        private static GraphicsPath CreateRoundedRectangle(
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
                rectangle.Right -
                diameter,
                rectangle.Top,
                diameter,
                diameter,
                270,
                90);

            path.AddArc(
                rectangle.Right -
                diameter,
                rectangle.Bottom -
                diameter,
                diameter,
                diameter,
                0,
                90);

            path.AddArc(
                rectangle.Left,
                rectangle.Bottom -
                diameter,
                diameter,
                diameter,
                90,
                90);

            path.CloseFigure();

            return path;
        }
    }
}
