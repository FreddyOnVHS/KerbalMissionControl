using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Widgets;
using System;
using System.Drawing;

namespace KMC.MissionControl.Pages
{
    public sealed class OrbitPage : IMissionPage
    {
        private const int PanelGap = 24;
        private const int PanelTopOffset = 80;
        private const int PanelBottomMargin = 10;
        private const int LeftPanelWidth = 455;
        private const int PanelPadding = 18;
        private const int PanelHeaderHeight = 42;
        private const int FieldRowHeight = 32;
        private const int LabelWidth = 205;

        private readonly OrbitPlotWidget _orbitPlotWidget;

        public OrbitPage()
        {
            _orbitPlotWidget =
                new OrbitPlotWidget();
        }

        public string Name
        {
            get
            {
                return "ORBIT DATA";
            }
        }

        public void Draw(
            MissionRenderContext context,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(
                    nameof(context));
            }

            if (telemetry == null)
            {
                return;
            }

            MissionPageLayout layout =
                new MissionPageLayout(
                    context);

            layout.DrawHeader(
                Name,
                "CH 01");

            Rectangle contentBounds =
                context.ContentBounds;

            int panelTop =
                contentBounds.Top +
                PanelTopOffset;

            int panelHeight =
                Math.Max(
                    1,
                    contentBounds.Bottom -
                    PanelBottomMargin -
                    panelTop);

            int safeLeftWidth =
                Math.Min(
                    LeftPanelWidth,
                    Math.Max(
                        300,
                        contentBounds.Width -
                        PanelGap -
                        300));

            Rectangle telemetryBounds =
                new Rectangle(
                    contentBounds.Left,
                    panelTop,
                    safeLeftWidth,
                    panelHeight);

            Rectangle orbitBounds =
                new Rectangle(
                    telemetryBounds.Right +
                    PanelGap,
                    panelTop,
                    Math.Max(
                        1,
                        contentBounds.Right -
                        telemetryBounds.Right -
                        PanelGap),
                    panelHeight);

            DrawTelemetryPanel(
                context,
                telemetryBounds,
                telemetry);

            _orbitPlotWidget.Draw(
                context,
                orbitBounds,
                telemetry);
        }

        private static void DrawTelemetryPanel(
    MissionRenderContext context,
    Rectangle bounds,
    MissionTelemetry telemetry)
        {
            DrawPanelFrame(
                context,
                bounds,
                "FDO ORBITAL DATA");

            const int columnGap = 18;
            const int columnLabelWidth = 104;

            int innerLeft =
                bounds.Left +
                PanelPadding;

            int innerRight =
                bounds.Right -
                PanelPadding;

            int availableWidth =
                innerRight -
                innerLeft;

            int columnWidth =
                (availableWidth -
                 columnGap) /
                2;

            int leftLabelX =
                innerLeft;

            int leftValueX =
                leftLabelX +
                columnLabelWidth;

            int rightLabelX =
                innerLeft +
                columnWidth +
                columnGap;

            int rightValueX =
                rightLabelX +
                columnLabelWidth;

            int dividerX =
                innerLeft +
                columnWidth +
                columnGap /
                2;

            int fieldTop =
                bounds.Top +
                PanelHeaderHeight +
                13;

            using (Pen dividerPen =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawLine(
                    dividerPen,
                    dividerX,
                    fieldTop - 4,
                    dividerX,
                    bounds.Bottom -
                    PanelPadding);
            }

            /*
             * Left column:
             * general flight and trajectory data.
             */

            DrawField(
                context,
                "VESSEL",
                FormatText(
                    telemetry.VesselName),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "ECC",
                FormatRatio(
                    telemetry.Eccentricity),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "BODY",
                FormatText(
                    telemetry.BodyName),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "SMA",
                FormatDistance(
                    telemetry.SemiMajorAxis),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "MET",
                FormatMissionTime(
                    telemetry.MissionTime),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "INCL",
                FormatOrbitAngle(
                    telemetry.InclinationDegrees),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "STAGE",
                telemetry.CurrentStage.ToString(
                    "00"),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "LAN",
                FormatOrbitAngle(
                    telemetry
                        .LongitudeOfAscendingNodeDegrees),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawSectionDivider(
                context,
                bounds,
                fieldTop - 3);

            fieldTop +=
                8;

            DrawField(
                context,
                "ALT",
                FormatDistance(
                    telemetry.Altitude),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "ARG PE",
                FormatOrbitAngle(
                    telemetry
                        .ArgumentOfPeriapsisDegrees),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "AP",
                FormatDistance(
                    telemetry.Apoapsis),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "TRUE AN",
                FormatOrbitAngle(
                    telemetry.TrueAnomalyDegrees),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "PE",
                FormatDistance(
                    telemetry.Periapsis),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "PERIOD",
                FormatDuration(
                    telemetry.OrbitalPeriod),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "T TO AP",
                FormatDuration(
                    telemetry.TimeToApoapsis),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "T TO PE",
                FormatDuration(
                    telemetry.TimeToPeriapsis),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawSectionDivider(
                context,
                bounds,
                fieldTop - 3);

            fieldTop +=
                8;

            DrawField(
                context,
                "ORB VEL",
                FormatSpeed(
                    telemetry.OrbitalSpeed),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "PITCH",
                FormatSignedAngle(
                    telemetry.Pitch),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "VERT VEL",
                FormatSignedSpeed(
                    telemetry.VerticalSpeed),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "HEAD",
                FormatHeading(
                    telemetry.Heading),
                rightLabelX,
                rightValueX,
                fieldTop);

            fieldTop +=
                FieldRowHeight;

            DrawField(
                context,
                "SURF VEL",
                FormatSpeed(
                    telemetry.SurfaceSpeed),
                leftLabelX,
                leftValueX,
                fieldTop);

            DrawField(
                context,
                "ROLL",
                FormatSignedAngle(
                    telemetry.Roll),
                rightLabelX,
                rightValueX,
                fieldTop);
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds,
            string title)
        {
            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        72,
                        3,
                        17,
                        23)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        155,
                        context.DimPhosphorColor),
                    2.0f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.FillRectangle(
                    backgroundBrush,
                    bounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    bounds);

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    bounds.Left +
                    PanelPadding,
                    bounds.Top +
                    8);

                int dividerY =
                    bounds.Top +
                    PanelHeaderHeight;

                context.Graphics.DrawLine(
                    borderPen,
                    bounds.Left +
                    PanelPadding,
                    dividerY,
                    bounds.Right -
                    PanelPadding,
                    dividerY);
            }
        }

        private static void DrawSectionDivider(
            MissionRenderContext context,
            Rectangle panelBounds,
            int y)
        {
            using (Pen dividerPen =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor),
                    1.0f))
            {
                context.Graphics.DrawLine(
                    dividerPen,
                    panelBounds.Left +
                    PanelPadding,
                    y,
                    panelBounds.Right -
                    PanelPadding,
                    y);
            }
        }

        private static void DrawField(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelX,
                    y);

                context.Graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueX,
                    y);
            }
        }

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return value.ToString(
                "0.00000");
        }

        private static string FormatDistance(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double absoluteValue =
                Math.Abs(value);

            if (absoluteValue >= 1000000.0)
            {
                return
                    (value /
                     1000000.0)
                    .ToString(
                        "0.00") +
                    " MM";
            }

            if (absoluteValue >= 1000.0)
            {
                return
                    (value /
                     1000.0)
                    .ToString(
                        "0.0") +
                    " KM";
            }

            return
                value.ToString(
                    "N0") +
                " M";
        }

        private static string FormatSpeed(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString(
                    "N1") +
                " M/S";
        }

        private static string FormatSignedSpeed(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString(
                    "+0.0;-0.0;0.0") +
                " M/S";
        }

        private static string FormatOrbitAngle(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double normalized =
                value %
                360.0;

            if (normalized < 0.0)
            {
                normalized +=
                    360.0;
            }

            return
                normalized.ToString(
                    "000.00") +
                "°";
        }

        private static string FormatSignedAngle(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                value.ToString(
                    "+0.0;-0.0;0.0") +
                "°";
        }

        private static string FormatHeading(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double normalized =
                value %
                360.0;

            if (normalized < 0.0)
            {
                normalized +=
                    360.0;
            }

            return
                normalized.ToString(
                    "000.0") +
                "°";
        }

        private static string FormatMissionTime(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                totalSeconds =
                    0.0;
            }

            int hours =
                (int)(
                    totalSeconds /
                    3600.0);

            int minutes =
                (int)(
                    totalSeconds %
                    3600.0) /
                60;

            int seconds =
                (int)(
                    totalSeconds %
                    60.0);

            return string.Format(
                "{0:000}:{1:00}:{2:00}",
                hours,
                minutes,
                seconds);
        }

        private static string FormatDuration(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                return "---";
            }

            int hours =
                (int)(
                    totalSeconds /
                    3600.0);

            int minutes =
                (int)(
                    totalSeconds %
                    3600.0) /
                60;

            int seconds =
                (int)(
                    totalSeconds %
                    60.0);

            if (hours > 0)
            {
                return string.Format(
                    "{0:00}:{1:00}:{2:00}",
                    hours,
                    minutes,
                    seconds);
            }

            return string.Format(
                "{0:00}:{1:00}",
                minutes,
                seconds);
        }

        private static string FormatText(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "---";
            }

            string result =
                value
                    .Trim()
                    .ToUpperInvariant();

            if (result.Length > 18)
            {
                result =
                    result.Substring(
                        0,
                        18);
            }

            return result;
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