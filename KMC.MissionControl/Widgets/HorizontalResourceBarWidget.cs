using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Widgets
{
    /// <summary>
    /// Apollo-style horizontal resource bar. The exact percentage remains
    /// visible as text while the bar provides quick at-a-glance awareness.
    /// </summary>
    public sealed class HorizontalResourceBarWidget : IMissionWidget
    {
        private readonly string _label;
        private readonly Func<MissionTelemetry, double> _amountSelector;
        private readonly Func<MissionTelemetry, double> _capacitySelector;

        public HorizontalResourceBarWidget(
            string label,
            Func<MissionTelemetry, double> amountSelector,
            Func<MissionTelemetry, double> capacitySelector)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException(
                    "A widget label is required.",
                    nameof(label));
            }

            _amountSelector = amountSelector ??
                throw new ArgumentNullException(
                    nameof(amountSelector));

            _capacitySelector = capacitySelector ??
                throw new ArgumentNullException(
                    nameof(capacitySelector));

            _label = label.Trim().ToUpperInvariant();
        }

        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null ||
                bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            double amount =
                _amountSelector(telemetry);

            double capacity =
                _capacitySelector(telemetry);

            bool hasData =
                IsFinite(amount) &&
                IsFinite(capacity) &&
                capacity > 0.0;

            double fraction =
                hasData
                    ? Clamp(amount / capacity, 0.0, 1.0)
                    : 0.0;

            string valueText =
                hasData
                    ? (fraction * 100.0).ToString("0.0") + "%"
                    : "---";

            int textHeight =
                Math.Max(
                    22,
                    context.SmallFont.Height + 2);

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    bounds.Top,
                    Math.Max(0, bounds.Width - 130),
                    textHeight);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Right - 130,
                    bounds.Top,
                    130,
                    textHeight);

            int barTop =
                bounds.Top + textHeight + 7;

            int barHeight =
                Math.Max(
                    16,
                    Math.Min(
                        24,
                        bounds.Bottom - barTop));

            Rectangle barBounds =
                new Rectangle(
                    bounds.Left,
                    barTop,
                    bounds.Width,
                    barHeight);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat rightFormat =
                new StringFormat())
            {
                rightFormat.Alignment =
                    StringAlignment.Far;

                rightFormat.LineAlignment =
                    StringAlignment.Near;

                context.Graphics.DrawString(
                    _label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds);

                context.Graphics.DrawString(
                    valueText,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    rightFormat);
            }

            DrawBar(
                context,
                barBounds,
                fraction,
                hasData);
        }

        private static void DrawBar(
            MissionRenderContext context,
            Rectangle bounds,
            double fraction,
            bool hasData)
        {
            if (bounds.Width <= 2 ||
                bounds.Height <= 2)
            {
                return;
            }

            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        105,
                        13,
                        35,
                        38)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        140,
                        context.DimPhosphorColor),
                    2.0f))
            {
                context.Graphics.FillRectangle(
                    backgroundBrush,
                    bounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    bounds);
            }

            if (!hasData ||
                fraction <= 0.0)
            {
                return;
            }

            Rectangle fillBounds =
                Rectangle.Inflate(
                    bounds,
                    -3,
                    -3);

            fillBounds.Width =
                Math.Max(
                    1,
                    (int)Math.Round(
                        fillBounds.Width * fraction));

            Color topColor =
                GetFillColor(
                    fraction,
                    225);

            Color bottomColor =
                GetFillColor(
                    fraction,
                    145);

            using (LinearGradientBrush fillBrush =
                new LinearGradientBrush(
                    fillBounds,
                    topColor,
                    bottomColor,
                    LinearGradientMode.Vertical))
            {
                context.Graphics.FillRectangle(
                    fillBrush,
                    fillBounds);
            }
        }

        private static Color GetFillColor(
            double fraction,
            int intensity)
        {
            if (fraction <= 0.15)
            {
                return Color.FromArgb(
                    intensity,
                    235,
                    62,
                    48);
            }

            if (fraction <= 0.35)
            {
                return Color.FromArgb(
                    intensity,
                    230,
                    185,
                    45);
            }

            return Color.FromArgb(
                intensity,
                45,
                215,
                95);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
