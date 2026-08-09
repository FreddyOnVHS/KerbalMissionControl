using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
    public sealed class FlightDirectorRenderer
    {
        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            FlightDirectorRenderModel model)
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
            using (Pen dividerPen =
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
            using (Brush alertBrush =
                new SolidBrush(
                    Color.FromArgb(
                        255,
                        255,
                        176,
                        64)))
            {
                DrawFlash(
                    graphics,
                    bounds,
                    model);

                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                const int padding = 10;
                const int titleHeight = 30;

                string title =
                    model.Available
                        ? "FLIGHT DIRECTOR / " +
                          SafeText(
                              model.FlightPhase)
                        : "FLIGHT DIRECTOR / ENGINE WAIT";

                graphics.DrawString(
                    title,
                    panelFont,
                    titleBrush,
                    bounds.Left + padding,
                    bounds.Top + 7);

                Rectangle content =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 4,
                        bounds.Width - padding * 2,
                        bounds.Height -
                        titleHeight -
                        padding - 4);

                int dividerX =
                    content.Left +
                    content.Width *
                    46 /
                    100;

                graphics.DrawLine(
                    dividerPen,
                    dividerX,
                    content.Top,
                    dividerX,
                    content.Bottom);

                Rectangle metricsBounds =
                    new Rectangle(
                        content.Left,
                        content.Top,
                        dividerX -
                        content.Left -
                        12,
                        content.Height);

                Rectangle commandBounds =
                    new Rectangle(
                        dividerX + 12,
                        content.Top,
                        content.Right -
                        dividerX -
                        12,
                        content.Height);

                DrawMetrics(
                    graphics,
                    panelFont,
                    labelBrush,
                    valueBrush,
                    metricsBounds,
                    model);

                DrawCommands(
                    graphics,
                    panelFont,
                    labelBrush,
                    valueBrush,
                    alertBrush,
                    commandBounds,
                    model);
            }
        }

        private static void DrawFlash(
            Graphics graphics,
            Rectangle bounds,
            FlightDirectorRenderModel model)
        {
            if (!model.FlashAlert)
            {
                return;
            }

            bool visible =
                ((int)(
                    model.MissionTimeSeconds *
                    8.0) %
                 2) ==
                0;

            if (!visible)
            {
                return;
            }

            using (Brush flashBrush =
                new SolidBrush(
                    Color.FromArgb(
                        48,
                        255,
                        176,
                        64)))
            {
                graphics.FillRectangle(
                    flashBrush,
                    bounds);
            }
        }

        private static void DrawMetrics(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            FlightDirectorRenderModel model)
        {
            string[] labels =
            {
                "TGT AP",
                "RANGE",
                "TGT ALT",
                "ALT ERR",
                "AP ERR",
                "NOM PITCH",
                "CMD PITCH",
                "RECOVERY"
            };

            string[] values =
            {
                model.Available
                    ? FormatDistance(
                        model.TargetApoapsisMeters)
                    : "---",

                model.Available
                    ? FormatDistance(
                        model.DownrangeMeters)
                    : "---",

                model.Available
                    ? FormatDistance(
                        model.TargetAltitudeMeters)
                    : "---",

                model.Available
                    ? FormatSignedDistance(
                        model.AltitudeErrorMeters)
                    : "---",

                model.Available
                    ? FormatSignedDistance(
                        model.ApoapsisErrorMeters)
                    : "---",

                model.Available
                    ? FormatAngle(
                        model.NominalPitchDegrees)
                    : "---",

                model.Available
                    ? FormatAngle(
                        model.RecommendedPitchDegrees)
                    : "---",

                model.Available
                    ? model.RecoveryAuthorityPercent
                        .ToString("0") +
                      "%"
                    : "---"
            };

            DrawRows(
                graphics,
                font,
                labelBrush,
                valueBrush,
                bounds,
                labels,
                values,
                true);
        }

        private static void DrawCommands(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Brush alertBrush,
            Rectangle bounds,
            FlightDirectorRenderModel model)
        {
            string blend =
                model.PredictiveGuidanceBlended
                    ? (model.PredictiveBlendFraction *
                       100.0)
                        .ToString("0") +
                      "%"
                    : "---";

            string[] labels =
            {
                "STEERING",
                "THROTTLE",
                "STATUS",
                "NEXT",
                "PRED BLEND",
                "TARGET",
                "CUTOFF",
                "HANDOFF"
            };

            string[] values =
            {
                model.Available
                    ? SafeText(
                        model.Command)
                    : "WAITING FOR ENGINE ASCENT",

                model.Available
                    ? SafeText(
                        model.ThrottleCommand)
                    : "---",

                model.Available
                    ? SafeText(
                        model.Status)
                    : "---",

                model.Available
                    ? SafeText(
                        model.NextEvent)
                    : "---",

                blend,

                model.Available
                    ? model.IsTargetAchievable
                        ? "ACHIEVABLE"
                        : "NOT RECOVERABLE"
                    : "---",

                model.Available
                    ? model.CutoffRequired
                        ? "REQUIRED"
                        : model.CoastLockoutActive
                            ? "LOCKED OUT"
                            : "NO"
                    : "---",

                model.Available
                    ? model.OrbitHandoffRequired
                        ? "ORBIT"
                        : "---"
                    : "---"
            };

            int rowHeight =
                Math.Max(
                    1,
                    bounds.Height /
                    Math.Max(
                        1,
                        labels.Length));

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                bool alert =
                    model.Available &&
                    ((labels[index] ==
                      "CUTOFF" &&
                      model.CutoffRequired) ||
                     (labels[index] ==
                      "TARGET" &&
                      !model.IsTargetAchievable));

                DrawRow(
                    graphics,
                    font,
                    labelBrush,
                    alert
                        ? alertBrush
                        : valueBrush,
                    bounds,
                    index,
                    rowHeight,
                    labels[index],
                    values[index],
                    false);
            }
        }

        private static void DrawRows(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            string[] labels,
            string[] values,
            bool rightAlignValue)
        {
            int rowHeight =
                Math.Max(
                    1,
                    bounds.Height /
                    Math.Max(
                        1,
                        labels.Length));

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                DrawRow(
                    graphics,
                    font,
                    labelBrush,
                    valueBrush,
                    bounds,
                    index,
                    rowHeight,
                    labels[index],
                    values[index],
                    rightAlignValue);
            }
        }

        private static void DrawRow(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            int index,
            int rowHeight,
            string label,
            string value,
            bool rightAlignValue)
        {
            int top =
                bounds.Top +
                index *
                rowHeight;

            if (top >=
                bounds.Bottom)
            {
                return;
            }

            int safeRowHeight =
                Math.Max(
                    1,
                    Math.Min(
                        rowHeight,
                        bounds.Bottom -
                        top));

            int labelWidth =
                bounds.Width *
                36 /
                100;

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    top,
                    labelWidth,
                    safeRowHeight);

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left +
                    labelWidth,
                    top,
                    bounds.Width -
                    labelWidth,
                    safeRowHeight);

            using (StringFormat labelFormat =
                CreateFormat(
                    StringAlignment.Near))
            using (StringFormat valueFormat =
                CreateFormat(
                    rightAlignValue
                        ? StringAlignment.Far
                        : StringAlignment.Near))
            {
                graphics.DrawString(
                    label,
                    font,
                    labelBrush,
                    labelBounds,
                    labelFormat);

                graphics.DrawString(
                    value,
                    font,
                    valueBrush,
                    valueBounds,
                    valueFormat);
            }
        }

        private static StringFormat CreateFormat(
            StringAlignment alignment)
        {
            return new StringFormat
            {
                Alignment =
                    alignment,
                LineAlignment =
                    StringAlignment.Center,
                Trimming =
                    StringTrimming.EllipsisCharacter,
                FormatFlags =
                    StringFormatFlags.NoWrap
            };
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(
                    meters))
            {
                return "---";
            }

            if (Math.Abs(
                    meters) >=
                1000.0)
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
            if (!IsFinite(
                    meters))
            {
                return "---";
            }

            if (Math.Abs(
                    meters) >=
                1000.0)
            {
                return
                    (meters / 1000.0)
                    .ToString(
                        "+0.0;-0.0;0.0") +
                    " KM";
            }

            return
                meters.ToString(
                    "+0;-0;0") +
                " M";
        }

        private static string FormatAngle(
            double degrees)
        {
            if (!IsFinite(
                    degrees))
            {
                return "---";
            }

            return
                degrees.ToString("0.0") +
                "°";
        }

        private static string SafeText(
            string value)
        {
            return
                string.IsNullOrWhiteSpace(
                    value)
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
