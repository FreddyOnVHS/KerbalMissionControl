using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering
{
    public sealed class MissionPageLayout
    {
        private readonly MissionRenderContext _context;
        private readonly Rectangle _bounds;

        private readonly int _leftLabelX;
        private readonly int _leftValueX;
        private readonly int _rightLabelX;
        private readonly int _rightValueX;

        private int _currentY;

        private const int HorizontalMargin = 14;
        private const int LabelValueGap = 120;
        private const int HeaderHeight = 34;
        private const int RowHeight = 24;
        private const int SectionGap = 18;

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

            _leftLabelX =
                _bounds.Left +
                HorizontalMargin;

            _leftValueX =
                _leftLabelX +
                LabelValueGap;

            _rightLabelX =
                _bounds.Left +
                (_bounds.Width / 2) +
                8;

            _rightValueX =
                _rightLabelX +
                LabelValueGap;

            _currentY =
                _bounds.Top +
                HeaderHeight +
                SectionGap;
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
                _bounds.Top + 4;

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
                    _context.LargeFont,
                    brush,
                    left,
                    top);

                _context.Graphics.DrawString(
                    safeChannel,
                    _context.LargeFont,
                    brush,
                    right,
                    top,
                    rightFormat);
            }

            int dividerY =
                _bounds.Top + 28;

            using (Pen pen =
                new Pen(
                    _context.DimPhosphorColor,
                    1f))
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

            _currentY += RowHeight;
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

            _currentY += RowHeight;
        }

        public void Space()
        {
            _currentY += SectionGap;
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

            using (SolidBrush brush =
                new SolidBrush(
                    _context.PhosphorColor))
            {
                _context.Graphics.DrawString(
                    safeLabel,
                    _context.LargeFont,
                    brush,
                    labelX,
                    y);

                _context.Graphics.DrawString(
                    safeValue,
                    _context.LargeFont,
                    brush,
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