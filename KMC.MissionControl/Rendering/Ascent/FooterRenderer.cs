using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the full-width ascent telemetry footer.
    /// </summary>
    public sealed class FooterRenderer
    {
        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            FooterRenderModel model)
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

            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
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

                string[] labels =
                {
                    "MET",
                    "STAGE",
                    "ALTITUDE",
                    "DOWNRANGE",
                    "VERT VEL",
                    "HORZ VEL",
                    "TWR",
                    "G FORCE",
                    "APOAPSIS",
                    "FUEL",
                    "STATUS"
                };

                string[] values =
                {
                    FormatMissionTime(
                        model.MissionTimeSeconds),

                    model.CurrentStage
                        .ToString("00"),

                    FormatDistance(
                        model.AltitudeMeters),

                    FormatDistance(
                        model.DownrangeMeters),

                    FormatSignedSpeed(
                        model.VerticalSpeedMetersPerSecond),

                    FormatSpeed(
                        model.HorizontalSpeedMetersPerSecond),

                    FormatRatio(
                        model.ThrustToWeightRatio),

                    FormatGForceCompact(
                        model.GForce),

                    FormatDistance(
                        model.ApoapsisMeters),

                    IsFinite(
                        model.FuelPercent)
                        ? model.FuelPercent
                            .ToString("0") +
                          " %"
                        : "---",

                    SafeText(
                        model.Status)
                };

                int count =
                    labels.Length;

                int cellWidth =
                    bounds.Width /
                    count;

                for (int index = 0;
                     index < count;
                     index++)
                {
                    int left =
                        bounds.Left +
                        index *
                        cellWidth;

                    int width =
                        index ==
                        count - 1
                            ? bounds.Right -
                              left
                            : cellWidth;

                    DrawFooterCell(
                        graphics,
                        context,
                        new Rectangle(
                            left,
                            bounds.Top,
                            width,
                            bounds.Height),
                        labels[index],
                        values[index],
                        labelBrush,
                        valueBrush);
                }
            }
        }

        private static void DrawFooterCell(
            Graphics graphics,
            MissionRenderContext context,
            Rectangle bounds,
            string label,
            string value,
            Brush labelBrush,
            Brush valueBrush)
        {
            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left + 4,
                    bounds.Top + 8,
                    bounds.Width - 8,
                    Math.Max(
                        16,
                        bounds.Height / 3));

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left + 4,
                    labelBounds.Bottom,
                    bounds.Width - 8,
                    bounds.Bottom -
                    labelBounds.Bottom -
                    5);

            using (StringFormat format =
                new StringFormat
                {
                    Alignment =
                        StringAlignment.Center,

                    LineAlignment =
                        StringAlignment.Center,

                    Trimming =
                        StringTrimming.EllipsisCharacter,

                    FormatFlags =
                        StringFormatFlags.NoWrap
                })
            {
                graphics.DrawString(
                    label ?? string.Empty,
                    context.SmallFont,
                    labelBrush,
                    labelBounds,
                    format);

                graphics.DrawString(
                    value ?? string.Empty,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    format);
            }
        }

        private static string FormatMissionTime(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            seconds =
                Math.Max(
                    0.0,
                    seconds);

            int totalSeconds =
                (int)Math.Floor(seconds);

            int hours =
                totalSeconds /
                3600;

            int minutes =
                (totalSeconds %
                 3600) /
                60;

            int remainingSeconds =
                totalSeconds %
                60;

            return string.Format(
                "{0:00}:{1:00}:{2:00}",
                hours,
                minutes,
                remainingSeconds);
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

        private static string FormatSignedSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(
                    metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond
                    .ToString("+0;-0;0") +
                " M/S";
        }

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return value.ToString("0.00");
        }

        private static string FormatGForceCompact(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString("0.00") +
                " G";
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
