using System;
using System.Drawing;

namespace KMC.MissionControl.Rendering.Ascent
{
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
                    7.5f,
                    context.SmallFont.Size *
                    0.76f);

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
            using (Brush sourceBrush =
                new SolidBrush(
                    Color.FromArgb(
                        255,
                        255,
                        176,
                        64)))
            {
                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                const int padding = 10;
                const int titleHeight = 30;

                graphics.DrawString(
                    "ASCENT PREDICTION / ENGINE",
                    panelFont,
                    titleBrush,
                    bounds.Left + padding,
                    bounds.Top + 7);

                Rectangle content =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 4,
                        bounds.Width -
                        padding * 2,
                        bounds.Height -
                        titleHeight -
                        padding - 4);

                int dividerX =
                    content.Left +
                    content.Width /
                    2;

                graphics.DrawLine(
                    dividerPen,
                    dividerX,
                    content.Top,
                    dividerX,
                    content.Bottom);

                Rectangle burnout =
                    new Rectangle(
                        content.Left,
                        content.Top,
                        dividerX -
                        content.Left -
                        12,
                        content.Height);

                Rectangle powered =
                    new Rectangle(
                        dividerX + 12,
                        content.Top,
                        content.Right -
                        dividerX -
                        12,
                        content.Height);

                DrawBurnout(
                    graphics,
                    panelFont,
                    labelBrush,
                    valueBrush,
                    sourceBrush,
                    burnout,
                    model);

                DrawPowered(
                    graphics,
                    panelFont,
                    labelBrush,
                    valueBrush,
                    sourceBrush,
                    powered,
                    model);
            }
        }

        private static void DrawBurnout(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Brush sourceBrush,
            Rectangle bounds,
            PredictionRenderModel model)
        {
            string[] labels =
            {
                "STAGE BURNOUT",
                "BURN REMAIN",
                "BURNOUT VEL",
                "PRED AP",
                "TARGET ERR",
                "CONFIDENCE",
                "STATUS",
                "EVIDENCE"
            };

            string[] values =
            {
                model.BurnoutAvailable
                    ? "AVAILABLE"
                    : "WAITING",

                model.BurnoutAvailable
                    ? FormatDuration(
                        model.BurnTimeRemainingSeconds)
                    : "---",

                model.BurnoutAvailable
                    ? FormatSpeed(
                        model.BurnoutVelocityMetersPerSecond)
                    : "---",

                model.BurnoutAvailable
                    ? FormatDistance(
                        model.BurnoutPredictedApoapsisMeters)
                    : "---",

                model.BurnoutAvailable
                    ? FormatSignedDistance(
                        model.BurnoutTargetErrorMeters)
                    : "---",

                model.BurnoutAvailable
                    ? model.BurnoutConfidencePercent
                        .ToString("0") +
                      "%"
                    : "---",

                SafeText(
                    model.BurnoutStatus),

                SafeText(
                    model.BurnoutEvidence)
            };

            DrawRows(
                graphics,
                font,
                labelBrush,
                valueBrush,
                sourceBrush,
                bounds,
                labels,
                values,
                7);
        }

        private static void DrawPowered(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Brush sourceBrush,
            Rectangle bounds,
            PredictionRenderModel model)
        {
            string mode =
                model.PoweredAvailable
                    ? SafeText(
                        model.PoweredMode)
                    : SafeText(
                        model.PoweredInactiveReason);

            string timing =
                model.PoweredAvailable
                    ? FormatDuration(
                        model.PoweredFlightSeconds) +
                      " + " +
                      FormatDuration(
                        model.CoastFlightSeconds)
                    : "---";

            string convergence =
                model.ConvergenceKnown
                    ? FormatDistance(
                        model.ConvergenceMeters)
                    : "---";

            string[] labels =
            {
                "POWERED TRAJ",
                "MODE",
                "PRED AP",
                "PRED PE",
                "ORBIT ERR",
                "CMD PITCH",
                "PWR+COAST",
                "CONF / CONV",
                "THRUST SRC"
            };

            string[] values =
            {
                model.PoweredAvailable
                    ? model.TargetCutoffReached
                        ? "TARGET CUTOFF"
                        : "AVAILABLE"
                    : "INACTIVE",

                mode,

                model.PoweredAvailable
                    ? FormatDistance(
                        model.PoweredPredictedApoapsisMeters)
                    : "---",

                model.PoweredAvailable
                    ? FormatDistance(
                        model.PoweredPredictedPeriapsisMeters)
                    : "---",

                model.PoweredAvailable
                    ? FormatSignedDistance(
                        model.PoweredOrbitErrorMeters)
                    : "---",

                model.PoweredAvailable
                    ? FormatAngle(
                        model.PoweredRecommendedPitchDegrees)
                    : "---",

                timing,

                model.PoweredAvailable
                    ? model.PoweredConfidencePercent
                        .ToString("0") +
                      "% / " +
                      convergence
                    : "---",

                SafeText(
                    model.ThrustEvidence)
            };

            DrawRows(
                graphics,
                font,
                labelBrush,
                valueBrush,
                sourceBrush,
                bounds,
                labels,
                values,
                8);
        }

        private static void DrawRows(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Brush sourceBrush,
            Rectangle bounds,
            string[] labels,
            string[] values,
            int sourceRowIndex)
        {
            int rowHeight =
                Math.Max(
                    19,
                    bounds.Height /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                int top =
                    bounds.Top +
                    index *
                    rowHeight;

                int labelWidth =
                    bounds.Width *
                    44 /
                    100;

                Rectangle labelBounds =
                    new Rectangle(
                        bounds.Left,
                        top,
                        labelWidth,
                        rowHeight);

                Rectangle valueBounds =
                    new Rectangle(
                        bounds.Left +
                        labelWidth,
                        top,
                        bounds.Width -
                        labelWidth,
                        rowHeight);

                using (StringFormat labelFormat =
                    CreateFormat(
                        StringAlignment.Near))
                using (StringFormat valueFormat =
                    CreateFormat(
                        StringAlignment.Far))
                {
                    graphics.DrawString(
                        labels[index],
                        font,
                        labelBrush,
                        labelBounds,
                        labelFormat);

                    graphics.DrawString(
                        values[index],
                        font,
                        index ==
                        sourceRowIndex
                            ? sourceBrush
                            : valueBrush,
                        valueBounds,
                        valueFormat);
                }
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

        private static string FormatDuration(
            double seconds)
        {
            if (!IsFinite(
                    seconds) ||
                seconds <
                    0.0)
            {
                return "---";
            }

            if (seconds <
                100.0)
            {
                return
                    seconds.ToString("0.0") +
                    " S";
            }

            int total =
                (int)Math.Round(
                    seconds);

            return string.Format(
                "{0:00}:{1:00}",
                total / 60,
                total % 60);
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
                metersPerSecond
                    .ToString("0") +
                " M/S";
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
