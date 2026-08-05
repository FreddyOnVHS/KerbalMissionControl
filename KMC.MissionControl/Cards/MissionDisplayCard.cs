using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Retained mission display card.
    ///
    /// Dirty cards rebuild one reusable local bitmap. Clean cards only present
    /// the cached bitmap onto the existing page canvas.
    /// </summary>
    public abstract class MissionDisplayCard<TModel> :
        IMissionDisplayCard<TModel>
    {
        private const int HorizontalContentInset = 18;
        private const int TopContentInset = 48;
        private const int BottomContentInset = 10;

        private Rectangle _bounds;
        private bool _visible;

        private Bitmap _cachedBitmap;
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

            Id = id.Trim();
            Title =
                string.IsNullOrWhiteSpace(title)
                    ? Id
                    : title.Trim();

            _visible = true;
            DirtyState = CardDirtyState.All;
        }

        public string Id { get; private set; }

        public string Title { get; protected set; }

        public Rectangle Bounds
        {
            get { return _bounds; }
            set
            {
                if (_bounds == value)
                {
                    return;
                }

                _bounds = value;

                MarkDirty(
                    CardDirtyState.Layout |
                    CardDirtyState.Static);
            }
        }

        public bool Visible
        {
            get { return _visible; }
            set
            {
                if (_visible == value)
                {
                    return;
                }

                _visible = value;
                MarkDirty(CardDirtyState.Static);
            }
        }

        public CardDirtyState DirtyState
        {
            get;
            private set;
        }

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

        public long BitmapAllocationCount
        {
            get { return _bitmapAllocationCount; }
        }

        public long CachedBitmapBytes
        {
            get
            {
                return _cachedBitmap == null
                    ? 0L
                    : (long)_cachedBitmap.Width *
                      _cachedBitmap.Height *
                      4L;
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
            DirtyState |= dirtyState;
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

            EnsureBitmap();

            bool rebuild =
                DirtyState != CardDirtyState.None;

            if (rebuild)
            {
                RebuildBitmap(
                    context,
                    model);

                DirtyState =
                    CardDirtyState.None;
            }
            else
            {
                _cacheHitCount++;
            }

            context.Graphics.DrawImageUnscaled(
                _cachedBitmap,
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

        private void EnsureBitmap()
        {
            Size required =
                new Size(
                    Math.Max(1, Bounds.Width),
                    Math.Max(1, Bounds.Height));

            if (_cachedBitmap != null &&
                _cachedBitmapSize == required)
            {
                return;
            }

            if (_cachedBitmap != null)
            {
                _cachedBitmap.Dispose();
            }

            _cachedBitmap =
                new Bitmap(
                    required.Width,
                    required.Height,
                    PixelFormat.Format32bppPArgb);

            _cachedBitmapSize =
                required;

            _bitmapAllocationCount++;

            MarkDirty(
                CardDirtyState.Layout |
                CardDirtyState.Static);
        }

        private void RebuildBitmap(
            MissionRenderContext parentContext,
            TModel model)
        {
            Stopwatch stopwatch =
                Stopwatch.StartNew();

            using (Graphics graphics =
                Graphics.FromImage(
                    _cachedBitmap))
            {
                graphics.Clear(
                    Color.Transparent);

                graphics.SmoothingMode =
                    SmoothingMode.AntiAlias;

                graphics.PixelOffsetMode =
                    PixelOffsetMode.HighQuality;

                graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;

                graphics.TextRenderingHint =
                    parentContext.Graphics
                        .TextRenderingHint;

                Rectangle localBounds =
                    new Rectangle(
                        0,
                        0,
                        _cachedBitmap.Width,
                        _cachedBitmap.Height);

                MissionRenderContext localContext =
                    new MissionRenderContext(
                        graphics,
                        localBounds,
                        parentContext.LargeFont,
                        parentContext.SmallFont,
                        parentContext.PhosphorColor,
                        parentContext.DimPhosphorColor,
                        parentContext.VirtualCanvasSize);

                if (DrawStandardFrame)
                {
                    DrawFrame(
                        localContext,
                        localBounds);
                }

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
                    Math.Max(0, bounds.Width - 1),
                    Math.Max(0, bounds.Height - 1));

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

        private static double UpdateRunningAverage(
            double currentAverage,
            double sample,
            long sampleCount)
        {
            if (sampleCount <= 1)
            {
                return sample;
            }

            return currentAverage +
                (sample - currentAverage) /
                sampleCount;
        }
    }
}
