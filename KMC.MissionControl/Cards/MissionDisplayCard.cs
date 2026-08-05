using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Cards
{
    /// <summary>
    /// Base implementation for retained mission display cards.
    ///
    /// Cards still draw into the shared page bitmap in Build 0.9.0.2. The
    /// dirty-state and timing lifecycle introduced here is the foundation for
    /// independent card bitmap caching in a later milestone.
    /// </summary>
    public abstract class MissionDisplayCard<TModel> :
        IMissionDisplayCard<TModel>
    {
        private const int HorizontalContentInset = 18;
        private const int TopContentInset = 48;
        private const int BottomContentInset = 10;

        private Rectangle _bounds;
        private bool _visible;

        private long _drawCount;
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

        public long DrawCount
        {
            get { return _drawCount; }
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

        protected virtual Rectangle CalculateContentBounds()
        {
            if (!DrawStandardFrame)
            {
                return Bounds;
            }

            return new Rectangle(
                Bounds.Left +
                HorizontalContentInset,
                Bounds.Top +
                TopContentInset,
                Math.Max(
                    1,
                    Bounds.Width -
                    HorizontalContentInset * 2),
                Math.Max(
                    1,
                    Bounds.Height -
                    TopContentInset -
                    BottomContentInset));
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

            Stopwatch stopwatch =
                Stopwatch.StartNew();

            try
            {
                if (DrawStandardFrame)
                {
                    DrawFrame(
                        context);
                }

                Rectangle contentBounds =
                    CalculateContentBounds();

                GraphicsState graphicsState =
                    context.Graphics.Save();

                try
                {
                    context.Graphics.SetClip(
                        contentBounds);

                    DrawContent(
                        context,
                        contentBounds,
                        model);
                }
                finally
                {
                    context.Graphics.Restore(
                        graphicsState);
                }
            }
            finally
            {
                stopwatch.Stop();

                _drawCount++;

                _lastDrawMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;

                _averageDrawMilliseconds =
                    UpdateRunningAverage(
                        _averageDrawMilliseconds,
                        _lastDrawMilliseconds,
                        _drawCount);

                CardDiagnosticsRegistry.RecordDraw(
                    Id,
                    Bounds,
                    stateBeforeDraw,
                    _drawCount,
                    _lastDrawMilliseconds,
                    _averageDrawMilliseconds);

                DirtyState =
                    CardDirtyState.None;
            }
        }

        protected abstract void DrawContent(
            MissionRenderContext context,
            Rectangle contentBounds,
            TModel model);

        private void DrawFrame(
            MissionRenderContext context)
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
                    Bounds);

                context.Graphics.DrawRectangle(
                    border,
                    Bounds);

                context.Graphics.DrawString(
                    Title,
                    context.SmallFont,
                    titleBrush,
                    Bounds.Left + 14,
                    Bounds.Top + 12);

                context.Graphics.DrawLine(
                    border,
                    Bounds.Left + 14,
                    Bounds.Top + 39,
                    Bounds.Right - 14,
                    Bounds.Top + 39);
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

            return
                currentAverage +
                (sample -
                 currentAverage) /
                sampleCount;
        }
    }
}
