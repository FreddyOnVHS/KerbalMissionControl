using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Retained mission display card with separate static and dynamic layers.
    ///
    /// The static layer contains the standard card frame, background, title,
    /// and title divider. It is rebuilt only for layout or static changes.
    ///
    /// The dynamic layer contains the card-specific content. It is rebuilt
    /// only when layout, static styling, or visible telemetry changes.
    /// </summary>
    public abstract class MissionDisplayCard<TModel> :
        IMissionDisplayCard<TModel>
    {
        private const int HorizontalContentInset = 18;
        private const int TopContentInset = 48;
        private const int BottomContentInset = 10;

        private Rectangle _bounds;
        private bool _visible;

        private Bitmap _staticBitmap;
        private Bitmap _dynamicBitmap;
        private Size _cachedBitmapSize;

        private long _drawCount;
        private long _presentationCount;
        private long _cacheHitCount;
        private long _bitmapAllocationCount;

        private double _lastDrawMilliseconds;
        private double _averageDrawMilliseconds;

        protected MissionDisplayCard(
            string id,
            string title)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "A card id is required.",
                    nameof(id));
            }

            Id =
                id.Trim();

            Title =
                string.IsNullOrWhiteSpace(title)
                    ? Id
                    : title.Trim();

            _visible =
                true;

            DirtyState =
                CardDirtyState.All;
        }

        public string Id
        {
            get;
            private set;
        }

        public string Title
        {
            get;
            protected set;
        }

        public Rectangle Bounds
        {
            get
            {
                return _bounds;
            }

            set
            {
                if (_bounds == value)
                {
                    return;
                }

                _bounds =
                    value;

                MarkDirty(
                    CardDirtyState.Layout |
                    CardDirtyState.Static);
            }
        }

        public bool Visible
        {
            get
            {
                return _visible;
            }

            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible =
                    value;

                MarkDirty(
                    CardDirtyState.Static);
            }
        }

        public CardDirtyState DirtyState
        {
            get;
            private set;
        }

        /// <summary>
        /// Number of dynamic-layer rebuilds.
        /// </summary>
        public long DrawCount
        {
            get { return _drawCount; }
        }

        public long PresentationCount
        {
            get { return _presentationCount; }
        }

        public long CacheHitCount
        {
            get { return _cacheHitCount; }
        }

        /// <summary>
        /// Total static and dynamic bitmap allocations.
        /// </summary>
        public long BitmapAllocationCount
        {
            get { return _bitmapAllocationCount; }
        }

        /// <summary>
        /// Combined estimated memory for both retained card layers.
        /// </summary>
        public long CachedBitmapBytes
        {
            get
            {
                long bytes =
                    0L;

                if (_staticBitmap != null)
                {
                    bytes +=
                        (long)_staticBitmap.Width *
                        _staticBitmap.Height *
                        4L;
                }

                if (_dynamicBitmap != null)
                {
                    bytes +=
                        (long)_dynamicBitmap.Width *
                        _dynamicBitmap.Height *
                        4L;
                }

                return bytes;
            }
        }

        public double LastDrawMilliseconds
        {
            get { return _lastDrawMilliseconds; }
        }

        public double AverageDrawMilliseconds
        {
            get { return _averageDrawMilliseconds; }
        }

        protected virtual bool DrawStandardFrame
        {
            get { return true; }
        }

        public void MarkDirty(
            CardDirtyState dirtyState)
        {
            DirtyState |=
                dirtyState;
        }

        public void Draw(
            MissionRenderContext context,
            TModel model)
        {
            if (!Visible ||
                context == null ||
                Bounds.Width <= 0 ||
                Bounds.Height <= 0)
            {
                return;
            }

            CardDirtyState stateBeforeDraw =
                DirtyState;

            EnsureBitmaps();

            bool staticLayerDirty =
                DrawStandardFrame &&
                (DirtyState &
                 (CardDirtyState.Layout |
                  CardDirtyState.Static)) !=
                CardDirtyState.None;

            bool dynamicLayerDirty =
                (DirtyState &
                 (CardDirtyState.Layout |
                  CardDirtyState.Static |
                  CardDirtyState.Telemetry)) !=
                CardDirtyState.None;

            bool rebuiltAnyLayer =
                false;

            if (staticLayerDirty)
            {
                RebuildStaticLayer(
                    context);

                rebuiltAnyLayer =
                    true;
            }

            if (dynamicLayerDirty)
            {
                RebuildDynamicLayer(
                    context,
                    model);

                rebuiltAnyLayer =
                    true;
            }

            if (!rebuiltAnyLayer)
            {
                _cacheHitCount++;
            }

            if (_staticBitmap != null)
            {
                context.Graphics.DrawImageUnscaled(
                    _staticBitmap,
                    Bounds.Location);
            }

            context.Graphics.DrawImageUnscaled(
                _dynamicBitmap,
                Bounds.Location);

            _presentationCount++;

            CardDiagnosticsRegistry.Record(
                Id,
                Bounds,
                stateBeforeDraw,
                _drawCount,
                _presentationCount,
                _cacheHitCount,
                _bitmapAllocationCount,
                CachedBitmapBytes,
                _lastDrawMilliseconds,
                _averageDrawMilliseconds);

            DirtyState =
                CardDirtyState.None;
        }

        protected abstract void DrawContent(
            MissionRenderContext context,
            Rectangle contentBounds,
            TModel model);

        protected virtual Rectangle CalculateContentBounds(
            Rectangle localBounds)
        {
            if (!DrawStandardFrame)
            {
                return localBounds;
            }

            return new Rectangle(
                localBounds.Left +
                HorizontalContentInset,
                localBounds.Top +
                TopContentInset,
                Math.Max(
                    1,
                    localBounds.Width -
                    HorizontalContentInset * 2),
                Math.Max(
                    1,
                    localBounds.Height -
                    TopContentInset -
                    BottomContentInset));
        }

        private void EnsureBitmaps()
        {
            Size required =
                new Size(
                    Math.Max(
                        1,
                        Bounds.Width),
                    Math.Max(
                        1,
                        Bounds.Height));

            bool sizeMatches =
                _cachedBitmapSize ==
                required;

            bool staticLayerReady =
                !DrawStandardFrame ||
                _staticBitmap != null;

            if (sizeMatches &&
                staticLayerReady &&
                _dynamicBitmap != null)
            {
                return;
            }

            DisposeBitmaps();

            if (DrawStandardFrame)
            {
                _staticBitmap =
                    CreateBitmap(
                        required);

                _bitmapAllocationCount++;
            }

            _dynamicBitmap =
                CreateBitmap(
                    required);

            _bitmapAllocationCount++;

            _cachedBitmapSize =
                required;

            MarkDirty(
                CardDirtyState.Layout |
                CardDirtyState.Static |
                CardDirtyState.Telemetry);
        }

        private void RebuildStaticLayer(
            MissionRenderContext parentContext)
        {
            if (_staticBitmap == null)
            {
                return;
            }

            using (Graphics graphics =
                Graphics.FromImage(
                    _staticBitmap))
            {
                ConfigureGraphics(
                    graphics,
                    parentContext);

                graphics.Clear(
                    Color.Transparent);

                Rectangle localBounds =
                    GetLocalBounds(
                        _staticBitmap);

                MissionRenderContext localContext =
                    CreateLocalContext(
                        graphics,
                        localBounds,
                        parentContext);

                DrawFrame(
                    localContext,
                    localBounds);
            }
        }

        private void RebuildDynamicLayer(
            MissionRenderContext parentContext,
            TModel model)
        {
            Stopwatch stopwatch =
                Stopwatch.StartNew();

            using (Graphics graphics =
                Graphics.FromImage(
                    _dynamicBitmap))
            {
                ConfigureGraphics(
                    graphics,
                    parentContext);

                graphics.Clear(
                    Color.Transparent);

                Rectangle localBounds =
                    GetLocalBounds(
                        _dynamicBitmap);

                MissionRenderContext localContext =
                    CreateLocalContext(
                        graphics,
                        localBounds,
                        parentContext);

                Rectangle contentBounds =
                    CalculateContentBounds(
                        localBounds);

                GraphicsState graphicsState =
                    graphics.Save();

                try
                {
                    graphics.SetClip(
                        contentBounds);

                    DrawContent(
                        localContext,
                        contentBounds,
                        model);
                }
                finally
                {
                    graphics.Restore(
                        graphicsState);
                }
            }

            stopwatch.Stop();

            _drawCount++;

            _lastDrawMilliseconds =
                stopwatch.Elapsed.TotalMilliseconds;

            _averageDrawMilliseconds =
                UpdateRunningAverage(
                    _averageDrawMilliseconds,
                    _lastDrawMilliseconds,
                    _drawCount);
        }

        private static Bitmap CreateBitmap(
            Size size)
        {
            return new Bitmap(
                size.Width,
                size.Height,
                PixelFormat.Format32bppPArgb);
        }

        private static Rectangle GetLocalBounds(
            Bitmap bitmap)
        {
            return new Rectangle(
                0,
                0,
                bitmap.Width,
                bitmap.Height);
        }

        private static void ConfigureGraphics(
            Graphics graphics,
            MissionRenderContext parentContext)
        {
            graphics.SmoothingMode =
                SmoothingMode.AntiAlias;

            graphics.PixelOffsetMode =
                PixelOffsetMode.HighQuality;

            graphics.InterpolationMode =
                InterpolationMode.HighQualityBicubic;

            graphics.CompositingMode =
                CompositingMode.SourceOver;

            graphics.CompositingQuality =
                CompositingQuality.HighQuality;

            graphics.TextRenderingHint =
                parentContext.Graphics
                    .TextRenderingHint;
        }

        private static MissionRenderContext CreateLocalContext(
            Graphics graphics,
            Rectangle localBounds,
            MissionRenderContext parentContext)
        {
            return new MissionRenderContext(
                graphics,
                localBounds,
                parentContext.LargeFont,
                parentContext.SmallFont,
                parentContext.PhosphorColor,
                parentContext.DimPhosphorColor,
                parentContext.VirtualCanvasSize);
        }

        private void DrawFrame(
            MissionRenderContext context,
            Rectangle bounds)
        {
            using (SolidBrush fill =
                new SolidBrush(
                    Color.FromArgb(
                        70,
                        2,
                        14,
                        20)))
            using (Pen border =
                new Pen(
                    Color.FromArgb(
                        130,
                        context.DimPhosphorColor),
                    1.4f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.FillRectangle(
                    fill,
                    bounds);

                context.Graphics.DrawRectangle(
                    border,
                    bounds.Left,
                    bounds.Top,
                    Math.Max(
                        0,
                        bounds.Width - 1),
                    Math.Max(
                        0,
                        bounds.Height - 1));

                context.Graphics.DrawString(
                    Title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 14,
                    bounds.Top + 12);

                context.Graphics.DrawLine(
                    border,
                    bounds.Left + 14,
                    bounds.Top + 39,
                    bounds.Right - 14,
                    bounds.Top + 39);
            }
        }

        private void DisposeBitmaps()
        {
            if (_staticBitmap != null)
            {
                _staticBitmap.Dispose();

                _staticBitmap =
                    null;
            }

            if (_dynamicBitmap != null)
            {
                _dynamicBitmap.Dispose();

                _dynamicBitmap =
                    null;
            }

            _cachedBitmapSize =
                Size.Empty;
        }

        private static double UpdateRunningAverage(
            double currentAverage,
            double sample,
            long sampleCount)
        {
            if (sampleCount <= 1)
            {
                return sample;
            }

            return
                currentAverage +
                (sample -
                 currentAverage) /
                sampleCount;
        }
    }
}
