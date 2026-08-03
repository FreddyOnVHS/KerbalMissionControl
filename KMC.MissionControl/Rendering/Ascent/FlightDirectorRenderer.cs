using System;
using System.Drawing;
using KMC.MissionControl.Guidance;

namespace KMC.MissionControl.Rendering.Ascent
{
    /// <summary>
    /// Stateless renderer for the right-side Flight Director panel.
    ///
    /// All guidance decisions are supplied through FlightDirectorRenderModel.
    /// This class only formats and draws the prepared values.
    /// </summary>
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

            MissionPlannerResult plan =
                model.Plan ??
                new MissionPlannerResult
                {
                    Command = "---",
                    ThrottleCommand = "---",
                    Status = "---",
                    NextEvent = "---",
                    FlightPhase = string.Empty
                };

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
            {
                DrawMecoFlash(
                    graphics,
                    bounds,
                    context,
                    model,
                    plan);

                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                int padding = 10;
                int titleHeight = 28;

                graphics.DrawString(
                    "FLIGHT DIRECTOR",
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
                    content.Width * 50 /
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
                    model,
                    plan);

                DrawCommands(
                    graphics,
                    panelFont,
                    labelBrush,
                    valueBrush,
                    commandBounds,
                    plan);
            }
        }

        private static void DrawMecoFlash(
            Graphics graphics,
            Rectangle bounds,
            MissionRenderContext context,
            FlightDirectorRenderModel model,
            MissionPlannerResult plan)
        {
            if (!plan.FlashAlert)
            {
                return;
            }

            bool visible =
                ((int)(model.MissionTimeSeconds * 8.0) %
                 2) == 0;

            if (!visible)
            {
                return;
            }

            using (Brush flashBrush =
                new SolidBrush(
                    Color.FromArgb(
                        78,
                        context.PhosphorColor)))
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
            FlightDirectorRenderModel model,
            MissionPlannerResult plan)
        {
            string[] labels =
            {
                "TGT AP",
                "RANGE",
                "TGT ALT",
                "ALT",
                "ALT ERR",
                "TGT PITCH",
                "PITCH",
                "DYN Q"
            };

            string[] values =
            {
                FormatDistance(
                    model.TargetApoapsisMeters),

                FormatDistance(
                    model.DownrangeMeters),

                FormatDistance(
                    model.TargetAltitudeMeters),

                FormatDistance(
                    model.ActualAltitudeMeters),

                FormatSignedDistance(
                    model.ActualAltitudeMeters -
                    model.TargetAltitudeMeters),

                FormatAngle(
                    plan.RecommendedPitchDegrees),

                FormatAngle(
                    model.ActualPitchDegrees),

                FormatPressure(
                    model.DynamicPressureKpa)
            };

            int rowHeight =
                Math.Max(
                    18,
                    bounds.Height /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                DrawSafeDataRow(
                    graphics,
                    font,
                    labelBrush,
                    valueBrush,
                    bounds,
                    index,
                    rowHeight,
                    labels[index],
                    values[index]);
            }
        }

        private static void DrawCommands(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle bounds,
            MissionPlannerResult plan)
        {
            string[] labels =
            {
                "GUIDANCE",
                "STEERING",
                "THROTTLE",
                "STATUS"
            };

            string guidanceValue =
                IsPostMecoPhase(
                    plan.FlightPhase) ||
                string.Equals(
                    plan.FlightPhase,
                    "MECO COUNTDOWN",
                    StringComparison.Ordinal)
                    ? SafeText(
                        plan.NextEvent)
                    : FormatAngle(
                        plan.RecommendedPitchDegrees);

            string[] values =
            {
                guidanceValue,
                SafeText(plan.Command),
                SafeText(plan.ThrottleCommand),
                GetCompactGuidanceStatus(
                    plan.Status)
            };

            int rowHeight =
                Math.Max(
                    24,
                    bounds.Height /
                    labels.Length);

            for (int index = 0;
                 index < labels.Length;
                 index++)
            {
                DrawCommandRow(
                    graphics,
                    font,
                    labelBrush,
                    valueBrush,
                    bounds,
                    index,
                    rowHeight,
                    labels[index],
                    values[index]);
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

        private static void DrawCommandRow(
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

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left,
                    top,
                    bounds.Width,
                    Math.Max(
                        13,
                        rowHeight / 2));

            Rectangle valueBounds =
                new Rectangle(
                    bounds.Left,
                    labelBounds.Bottom,
                    bounds.Width,
                    Math.Max(
                        13,
                        rowHeight -
                        labelBounds.Height));

            using (StringFormat labelFormat =
                CreateSingleLineFormat(
                    StringAlignment.Near))
            using (StringFormat valueFormat =
                CreateSingleLineFormat(
                    StringAlignment.Near))
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

        private static bool IsPostMecoPhase(
            string phase)
        {
            return
                string.Equals(
                    phase,
                    "MECO",
                    StringComparison.Ordinal) ||
                string.Equals(
                    phase,
                    "COAST TO APOAPSIS",
                    StringComparison.Ordinal) ||
                string.Equals(
                    phase,
                    "CIRCULARIZATION READY",
                    StringComparison.Ordinal) ||
                string.Equals(
                    phase,
                    "CIRCULARIZATION BURN",
                    StringComparison.Ordinal) ||
                string.Equals(
                    phase,
                    "ORBIT ACHIEVED",
                    StringComparison.Ordinal);
        }

        private static string GetCompactGuidanceStatus(
            string guidance)
        {
            if (string.IsNullOrWhiteSpace(
                    guidance))
            {
                return "---";
            }

            switch (guidance)
            {
                case "HOLD: INSUFFICIENT LAUNCH TWR":
                    return "LOW LAUNCH TWR";

                case "HIGH DYNAMIC PRESSURE - LIMIT PITCH RATE":
                    return "HIGH DYN Q";

                case "PROFILE HIGH - PITCH DOWN GRADUALLY":
                    return "PROFILE HIGH";

                case "PROFILE LOW - HOLD VERTICAL COMPONENT":
                    return "PROFILE LOW";

                case "PITCH HIGH - INCREASE GRAVITY TURN":
                    return "PITCH HIGH";

                case "PITCH LOW - REDUCE TURN RATE":
                    return "PITCH LOW";

                case "TARGET APOAPSIS ACHIEVED - PREPARE MECO":
                    return "PREPARE MECO";

                case "ASCENT PROFILE NOMINAL":
                    return "NOMINAL";

                case "AWAITING ASCENT":
                    return "AWAIT ASCENT";

                case "PREPARE FOR MECO 5":
                case "PREPARE FOR MECO 4":
                case "PREPARE FOR MECO 3":
                case "PREPARE FOR MECO 2":
                case "PREPARE FOR MECO 1":
                    return guidance;

                case "CUTOFF REQUIRED":
                    return "MECO";

                case "COAST - NO REIGNITION":
                    return "COAST LOCKED";

                case "TARGET APPROACH":
                    return "TARGET APPROACH";

                case "AWAIT LIFTOFF":
                    return "AWAIT LIFTOFF";

                case "MECO CONFIRMED":
                    return "COAST SETUP";

                case "PREPARE CIRCULARIZATION":
                    return "PREP CIRC BURN";

                case "IGNITION APPROACHING":
                    return "IGNITION SOON";

                case "CIRCULARIZATION GO":
                    return "IGNITE NOW";

                case "RAISE PERIAPSIS":
                    return "CIRC BURN";

                case "CIRC BURN REQUIRED":
                    return "IGNITE NOW";

                case "ORBIT TARGET REACHED":
                    return "CUTOFF NOW";

                case "ORBIT CUTOFF":
                    return "CUTOFF NOW";

                case "ORBIT NOMINAL":
                    return "ORBIT NOMINAL";

                case "UNPLANNED IGNITION":
                    return "EARLY IGNITION";

                default:
                    return guidance;
            }
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

        private static string FormatAngle(
            double degrees)
        {
            if (!IsFinite(degrees))
            {
                return "---";
            }

            return
                degrees.ToString("0.0") +
                "°";
        }

        private static string FormatPressure(
            double kilopascals)
        {
            if (!IsFinite(kilopascals))
            {
                return "---";
            }

            return
                kilopascals.ToString("0.0") +
                " KPA";
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
