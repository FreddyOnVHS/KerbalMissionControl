using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering
{
    /// <summary>
    /// Standard Apollo-style text layout for mission pages.
    ///
    /// All values are expressed in virtual 1280 x 720 coordinates.
    /// </summary>
    public sealed class MissionPageLayout
    {
        private const int HorizontalMargin = 28;
        private const int HeaderHeight = 62;
        private const int RowHeight = 43;
        private const int SectionGap = 28;
        private const int ColumnGap = 34;

        private readonly MissionRenderContext _context;
        private readonly Rectangle _bounds;

        private readonly int _leftLabelX;
        private readonly int _leftValueX;
        private readonly int _rightLabelX;
        private readonly int _rightValueX;

        private int _currentY;

        public MissionPageLayout(
            MissionRenderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            _context = context;
            _bounds = context.ContentBounds;

            int halfWidth =
                _bounds.Width / 2;

            int labelValueGap =
                Math.Max(
                    190,
                    halfWidth / 3);

            _leftLabelX =
                _bounds.Left +
                HorizontalMargin;

            _leftValueX =
                _leftLabelX +
                labelValueGap;

            _rightLabelX =
                _bounds.Left +
                halfWidth +
                ColumnGap;

            _rightValueX =
                _rightLabelX +
                labelValueGap;

            _currentY =
                _bounds.Top +
                HeaderHeight +
                SectionGap;
        }

        public int CurrentY
        {
            get
            {
                return _currentY;
            }
        }

        public Rectangle ContentBounds
        {
            get
            {
                return _bounds;
            }
        }

        public void DrawHeader(
            string title,
            string channel)
        {
            string safeTitle =
                FormatText(title);

            string safeChannel =
                FormatText(channel);

            int left =
                _bounds.Left +
                HorizontalMargin;

            int right =
                _bounds.Right -
                HorizontalMargin;

            int top =
                _bounds.Top + 5;

            using (SolidBrush brush =
                new SolidBrush(
                    _context.PhosphorColor))
            using (StringFormat rightFormat =
                new StringFormat())
            {
                rightFormat.Alignment =
                    StringAlignment.Far;

                rightFormat.LineAlignment =
                    StringAlignment.Near;

                _context.Graphics.DrawString(
                    safeTitle,
                    _context.SmallFont,
                    brush,
                    left,
                    top);

                _context.Graphics.DrawString(
                    safeChannel,
                    _context.SmallFont,
                    brush,
                    right,
                    top,
                    rightFormat);
            }

            int dividerY =
                _bounds.Top + 50;

            using (Pen pen =
                new Pen(
                    _context.DimPhosphorColor,
                    2.0f))
            {
                _context.Graphics.DrawLine(
                    pen,
                    left,
                    dividerY,
                    right,
                    dividerY);
            }
        }

        public void Row(
            string leftLabel,
            string leftValue,
            string rightLabel,
            string rightValue)
        {
            DrawField(
                leftLabel,
                leftValue,
                _leftLabelX,
                _leftValueX,
                _currentY);

            DrawField(
                rightLabel,
                rightValue,
                _rightLabelX,
                _rightValueX,
                _currentY);

            _currentY +=
                RowHeight;
        }

        public void Row(
            string leftLabel,
            string leftValue)
        {
            DrawField(
                leftLabel,
                leftValue,
                _leftLabelX,
                _leftValueX,
                _currentY);

            _currentY +=
                RowHeight;
        }

        public void Space()
        {
            _currentY +=
                SectionGap;
        }

        /// <summary>
        /// Reserves a rectangular region beneath the current text row.
        /// Future widgets can draw inside the returned rectangle.
        /// </summary>
        public Rectangle ReserveRegion(
            int height)
        {
            int safeHeight =
                Math.Max(
                    0,
                    height);

            Rectangle region =
                new Rectangle(
                    _bounds.Left +
                    HorizontalMargin,
                    _currentY,
                    Math.Max(
                        0,
                        _bounds.Width -
                        HorizontalMargin * 2),
                    safeHeight);

            _currentY +=
                safeHeight;

            return region;
        }

        /// <summary>
        /// Reserves a region and adds standard spacing after it.
        /// </summary>
        public Rectangle ReserveSection(
            int height)
        {
            Rectangle region =
                ReserveRegion(
                    height);

            Space();

            return region;
        }

        private void DrawField(
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            string safeLabel =
                FormatText(label);

            string safeValue =
                FormatText(value);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    _context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    _context.PhosphorColor))
            {
                _context.Graphics.DrawString(
                    safeLabel,
                    _context.LargeFont,
                    labelBrush,
                    labelX,
                    y);

                _context.Graphics.DrawString(
                    safeValue,
                    _context.LargeFont,
                    valueBrush,
                    valueX,
                    y);
            }
        }

        private static string FormatText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "---";
            }

            return value
                .Trim()
                .ToUpperInvariant();
        }
    }
}