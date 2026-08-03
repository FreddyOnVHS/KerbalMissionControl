using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the Predicted Burnout panel.
    /// </summary>
    public sealed class PredictionRenderer
    {
        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            PredictionRenderModel model)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (model == null)
            {
                return;
            }

            Graphics graphics =
                context.Graphics;

            float panelFontSize =
                Math.Max(
                    7.0f,
                    context.SmallFont.Size *
                    0.72f);

            using (Font panelFont =
                new Font(
                    context.SmallFont.FontFamily,
                    panelFontSize,
                    FontStyle.Regular,
                    GraphicsUnit.Point))
            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Brush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Brush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Brush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                const int padding = 10;
                const int titleHeight = 28;

                graphics.DrawString(
                    "PREDICTED BURNOUT",
                    panelFont,
                    titleBrush,
                    bounds.Left + padding,
                    bounds.Top + 7);

                Rectangle content =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 3,
                        bounds.Width - padding * 2,
                        bounds.Height -
                        titleHeight -
                        padding - 3);

                string[] labels =
                {
                    "BURN TIME",
                    "BURNOUT VEL",
                    "PREDICTED AP",
                    "TARGET ERR",
                    "CONFIDENCE",
                    "RESULT"
                };

                string[] values =
                {
                    model.IsAvailable
                        ? FormatDurationCompact(
                            model.TimeRemainingSeconds)
                        : "---",

                    model.IsAvailable
                        ? FormatSpeed(
                            model.BurnoutVelocityMetersPerSecond)
                        : "---",

                    model.IsAvailable
                        ? FormatDistance(
                            model.PredictedApoapsisMeters)
                        : "---",

                    model.IsAvailable
                        ? FormatSignedDistance(
                            model.PredictedApoapsisMeters -
                            model.TargetApoapsisMeters)
                        : "---",

                    model.IsAvailable
                        ? model.ConfidencePercent
                            .ToString("0") +
                          " %"
                        : "WAITING",

                    SafeText(
                        model.Status)
                };

                int rowHeight =
                    Math.Max(
                        20,
                        content.Height /
                        labels.Length);

                for (int index = 0;
                     index < labels.Length;
                     index++)
                {
                    DrawSafeDataRow(
                        graphics,
                        panelFont,
                        labelBrush,
                        valueBrush,
                        content,
                        index,
                        rowHeight,
                        labels[index],
                        values[index]);
                }
            }
        }

        private static void DrawSafeDataRow(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            int index,
            int rowHeight,
            string label,
            string value)
        {
            int top =
                bounds.Top +
                index *
                rowHeight;

            int labelWidth =
                bounds.Width * 54 /
                100;

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    top,
                    labelWidth,
                    rowHeight);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + labelWidth,
                    top,
                    bounds.Width - labelWidth,
                    rowHeight);

            using (StringFormat labelFormat =
                CreateSingleLineFormat(
                    StringAlignment.Near))
            using (StringFormat valueFormat =
                CreateSingleLineFormat(
                    StringAlignment.Far))
            {
                graphics.DrawString(
                    label ?? string.Empty,
                    font,
                    labelBrush,
                    labelBounds,
                    labelFormat);

                graphics.DrawString(
                    value ?? string.Empty,
                    font,
                    valueBrush,
                    valueBounds,
                    valueFormat);
            }
        }

        private static StringFormat CreateSingleLineFormat(
            StringAlignment alignment)
        {
            return new StringFormat
            {
                Alignment = alignment,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            };
        }

        private static string FormatDurationCompact(
            double seconds)
        {
            if (!IsFinite(seconds) ||
                seconds < 0.0)
            {
                return "---";
            }

            int totalSeconds =
                (int)Math.Round(seconds);

            int minutes =
                totalSeconds / 60;

            int remainingSeconds =
                totalSeconds % 60;

            return string.Format(
                "{0:00}:{1:00}",
                minutes,
                remainingSeconds);
        }

        private static string FormatSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(
                    metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond.ToString("0") +
                " M/S";
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            double absolute =
                Math.Abs(meters);

            if (absolute >= 1000000.0)
            {
                return
                    (meters / 1000000.0)
                    .ToString("0.00") +
                    " MM";
            }

            if (absolute >= 1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString("0.0") +
                    " KM";
            }

            return
                meters.ToString("0") +
                " M";
        }

        private static string FormatSignedDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            if (Math.Abs(meters) >= 1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString("+0.0;-0.0;0.0") +
                    " KM";
            }

            return
                meters.ToString("+0;-0;0") +
                " M";
        }

        private static string SafeText(
            string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "---"
                : value;
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
