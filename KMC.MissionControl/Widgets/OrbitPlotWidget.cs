using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Widgets
{
    /// <summary>
    /// Apollo-style schematic orbit plot.
    ///
    /// The plot uses apoapsis, periapsis, altitude, and vertical velocity
    /// to provide an approximate visual representation of the current orbit.
    /// It is intended for situational awareness rather than precision
    /// navigation.
    /// </summary>
    public sealed class OrbitPlotWidget : IMissionWidget
    {
        private const int PanelPadding = 18;
        private const int HeaderHeight = 42;
        private const int PlotPadding = 34;

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

            DrawPanelFrame(
                context,
                bounds);

            Rectangle plotBounds =
                GetPlotBounds(
                    bounds);

            DrawBackgroundGrid(
                context,
                plotBounds);

            if (plotBounds.Width <= 20 ||
                plotBounds.Height <= 20)
            {
                return;
            }

            bool hasOrbitData =
                HasUsableOrbitData(
                    telemetry);

            if (!hasOrbitData)
            {
                DrawPlaceholderOrbit(
                    context,
                    plotBounds,
                    telemetry);

                return;
            }

            DrawOrbit(
                context,
                plotBounds,
                telemetry);
        }

        private static Rectangle GetPlotBounds(
            Rectangle bounds)
        {
            return new Rectangle(
                bounds.Left +
                PanelPadding +
                PlotPadding,

                bounds.Top +
                HeaderHeight +
                PlotPadding,

                Math.Max(
                    1,
                    bounds.Width -
                    PanelPadding * 2 -
                    PlotPadding * 2),

                Math.Max(
                    1,
                    bounds.Height -
                    HeaderHeight -
                    PanelPadding -
                    PlotPadding * 2));
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds)
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
                    "ORBIT PLOT",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left +
                    PanelPadding,
                    bounds.Top +
                    8);

                int dividerY =
                    bounds.Top +
                    HeaderHeight;

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

        private static void DrawBackgroundGrid(
    MissionRenderContext context,
    Rectangle bounds)
        {
            if (bounds.Width <= 0 ||
                bounds.Height <= 0)
            {
                return;
            }

            Color majorGridColor =
                Color.FromArgb(
                    38,
                    context.DimPhosphorColor);

            Color minorGridColor =
                Color.FromArgb(
                    20,
                    context.DimPhosphorColor);

            using (Pen majorPen =
                new Pen(
                    majorGridColor,
                    1.0f))
            using (Pen minorPen =
                new Pen(
                    minorGridColor,
                    1.0f))
            {
                majorPen.DashStyle =
                    DashStyle.Dot;

                minorPen.DashStyle =
                    DashStyle.Dot;

                const int minorSpacing = 36;
                const int majorSpacing = 108;

                for (int x = bounds.Left;
                     x <= bounds.Right;
                     x += minorSpacing)
                {
                    bool isMajor =
                        (x - bounds.Left) %
                        majorSpacing ==
                        0;

                    context.Graphics.DrawLine(
                        isMajor
                            ? majorPen
                            : minorPen,
                        x,
                        bounds.Top,
                        x,
                        bounds.Bottom);
                }

                for (int y = bounds.Top;
                     y <= bounds.Bottom;
                     y += minorSpacing)
                {
                    bool isMajor =
                        (y - bounds.Top) %
                        majorSpacing ==
                        0;

                    context.Graphics.DrawLine(
                        isMajor
                            ? majorPen
                            : minorPen,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private static void DrawOrbit(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            RectangleF orbitBounds =
                CalculateOrbitBounds(
                    plotBounds,
                    telemetry);

            PointF bodyCenter =
                CalculateBodyCenter(
                    orbitBounds,
                    telemetry);

            float bodyRadius =
                Math.Max(
                    10.0f,
                    Math.Min(
                        orbitBounds.Width,
                        orbitBounds.Height) *
                    0.065f);

            DrawReferenceAxes(
                context,
                plotBounds,
                bodyCenter);

            DrawOrbitPath(
                context,
                orbitBounds);

            DrawCentralBody(
                context,
                bodyCenter,
                bodyRadius,
                telemetry.BodyName);

            PointF apoapsisPoint =
                new PointF(
                    orbitBounds.Right,
                    orbitBounds.Top +
                    orbitBounds.Height /
                    2.0f);

            PointF periapsisPoint =
                new PointF(
                    orbitBounds.Left,
                    orbitBounds.Top +
                    orbitBounds.Height /
                    2.0f);

            DrawApsisMarker(
                context,
                apoapsisPoint,
                "AP",
                telemetry.Apoapsis,
                true);

            DrawApsisMarker(
                context,
                periapsisPoint,
                "PE",
                telemetry.Periapsis,
                false);

            double vesselAngle =
                CalculateVesselAngle(
                    telemetry);

            PointF vesselPoint =
                GetEllipsePoint(
                    orbitBounds,
                    vesselAngle);

            DrawVesselMarker(
                context,
                orbitBounds,
                vesselPoint,
                vesselAngle,
                telemetry);
        }

        private static RectangleF CalculateOrbitBounds(
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            double apoapsis =
                Math.Max(
                    1.0,
                    telemetry.Apoapsis);

            double periapsis =
                Math.Max(
                    0.0,
                    telemetry.Periapsis);

            double minimumRadius =
                Math.Max(
                    1.0,
                    periapsis);

            double maximumRadius =
                Math.Max(
                    minimumRadius,
                    apoapsis);

            double eccentricityFactor =
                maximumRadius <= 0.0
                    ? 0.0
                    : (maximumRadius -
                       minimumRadius) /
                      maximumRadius;

            eccentricityFactor =
                Clamp(
                    eccentricityFactor,
                    0.0,
                    0.72);

            float width =
                plotBounds.Width;

            float heightFactor =
                (float)(
                    0.82 -
                    eccentricityFactor *
                    0.42);

            float height =
                Math.Max(
                    plotBounds.Height *
                    0.34f,
                    plotBounds.Height *
                    heightFactor);

            height =
                Math.Min(
                    plotBounds.Height,
                    height);

            return new RectangleF(
                plotBounds.Left,
                plotBounds.Top +
                (plotBounds.Height -
                 height) /
                2.0f,
                width,
                height);
        }

        private static PointF CalculateBodyCenter(
            RectangleF orbitBounds,
            MissionTelemetry telemetry)
        {
            double apoapsis =
                Math.Max(
                    1.0,
                    telemetry.Apoapsis);

            double periapsis =
                Math.Max(
                    0.0,
                    telemetry.Periapsis);

            double eccentricity =
                (apoapsis -
                 periapsis) /
                Math.Max(
                    1.0,
                    apoapsis +
                    periapsis);

            eccentricity =
                Clamp(
                    eccentricity,
                    0.0,
                    0.72);

            float focusOffset =
                (float)(
                    orbitBounds.Width /
                    2.0 *
                    eccentricity);

            return new PointF(
                orbitBounds.Left +
                orbitBounds.Width /
                2.0f +
                focusOffset,
                orbitBounds.Top +
                orbitBounds.Height /
                2.0f);
        }

        private static void DrawReferenceAxes(
            MissionRenderContext context,
            Rectangle plotBounds,
            PointF bodyCenter)
        {
            Color axisColor =
                Color.FromArgb(
                    55,
                    context.DimPhosphorColor);

            using (Pen axisPen =
                new Pen(
                    axisColor,
                    1.0f))
            {
                axisPen.DashStyle =
                    DashStyle.Dot;

                context.Graphics.DrawLine(
                    axisPen,
                    plotBounds.Left,
                    bodyCenter.Y,
                    plotBounds.Right,
                    bodyCenter.Y);

                context.Graphics.DrawLine(
                    axisPen,
                    bodyCenter.X,
                    plotBounds.Top,
                    bodyCenter.X,
                    plotBounds.Bottom);
            }
        }

        private static void DrawOrbitPath(
            MissionRenderContext context,
            RectangleF orbitBounds)
        {
            using (Pen glowPen =
                new Pen(
                    Color.FromArgb(
                        45,
                        context.PhosphorColor),
                    7.0f))
            using (Pen orbitPen =
                new Pen(
                    Color.FromArgb(
                        220,
                        context.PhosphorColor),
                    2.0f))
            {
                context.Graphics.DrawEllipse(
                    glowPen,
                    orbitBounds);

                context.Graphics.DrawEllipse(
                    orbitPen,
                    orbitBounds);
            }
        }

        private static void DrawCentralBody(
            MissionRenderContext context,
            PointF center,
            float radius,
            string bodyName)
        {
            RectangleF bodyBounds =
                new RectangleF(
                    center.X -
                    radius,
                    center.Y -
                    radius,
                    radius * 2.0f,
                    radius * 2.0f);

            using (LinearGradientBrush bodyBrush =
                new LinearGradientBrush(
                    bodyBounds,
                    Color.FromArgb(
                        190,
                        context.PhosphorColor),
                    Color.FromArgb(
                        55,
                        context.DimPhosphorColor),
                    LinearGradientMode.ForwardDiagonal))
            using (Pen bodyPen =
                new Pen(
                    context.PhosphorColor,
                    2.0f))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat centerFormat =
                new StringFormat())
            {
                centerFormat.Alignment =
                    StringAlignment.Center;

                context.Graphics.FillEllipse(
                    bodyBrush,
                    bodyBounds);

                context.Graphics.DrawEllipse(
                    bodyPen,
                    bodyBounds);

                string label =
                    FormatBodyName(
                        bodyName);

                RectangleF labelBounds =
                    new RectangleF(
                        center.X -
                        100.0f,
                        bodyBounds.Bottom +
                        6.0f,
                        200.0f,
                        context.SmallFont.Height +
                        6.0f);

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds,
                    centerFormat);
            }
        }

        private static void DrawApsisMarker(
            MissionRenderContext context,
            PointF point,
            string label,
            double altitude,
            bool placeRight)
        {
            const float markerRadius = 5.0f;

            RectangleF markerBounds =
                new RectangleF(
                    point.X -
                    markerRadius,
                    point.Y -
                    markerRadius,
                    markerRadius * 2.0f,
                    markerRadius * 2.0f);

            using (SolidBrush markerBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (SolidBrush textBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                context.Graphics.FillEllipse(
                    markerBrush,
                    markerBounds);

                format.Alignment =
                    placeRight
                        ? StringAlignment.Near
                        : StringAlignment.Far;

                string text =
                    label +
                    "  " +
                    FormatDistance(
                        altitude);

                RectangleF textBounds =
                    new RectangleF(
                        placeRight
                            ? point.X + 10.0f
                            : point.X - 190.0f,
                        point.Y -
                        context.SmallFont.Height -
                        4.0f,
                        180.0f,
                        context.SmallFont.Height +
                        8.0f);

                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    textBrush,
                    textBounds,
                    format);
            }
        }

        private static void DrawVesselMarker(
            MissionRenderContext context,
            RectangleF orbitBounds,
            PointF vesselPoint,
            double vesselAngle,
            MissionTelemetry telemetry)
        {
            PointF tangent =
                CalculateTangent(
                    orbitBounds,
                    vesselAngle);

            PointF normal =
                Normalize(
                    tangent);

            float markerLength =
                16.0f;

            PointF nose =
                new PointF(
                    vesselPoint.X +
                    normal.X *
                    markerLength,

                    vesselPoint.Y +
                    normal.Y *
                    markerLength);

            PointF perpendicular =
                new PointF(
                    -normal.Y,
                    normal.X);

            PointF left =
                new PointF(
                    vesselPoint.X -
                    normal.X *
                    7.0f +
                    perpendicular.X *
                    7.0f,

                    vesselPoint.Y -
                    normal.Y *
                    7.0f +
                    perpendicular.Y *
                    7.0f);

            PointF right =
                new PointF(
                    vesselPoint.X -
                    normal.X *
                    7.0f -
                    perpendicular.X *
                    7.0f,

                    vesselPoint.Y -
                    normal.Y *
                    7.0f -
                    perpendicular.Y *
                    7.0f);

            using (GraphicsPath vesselPath =
                new GraphicsPath())
            using (SolidBrush glowBrush =
                new SolidBrush(
                    Color.FromArgb(
                        70,
                        context.PhosphorColor)))
            using (SolidBrush vesselBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                vesselPath.AddPolygon(
                    new[]
                    {
                        nose,
                        left,
                        right
                    });

                RectangleF glowBounds =
                    new RectangleF(
                        vesselPoint.X -
                        15.0f,
                        vesselPoint.Y -
                        15.0f,
                        30.0f,
                        30.0f);

                context.Graphics.FillEllipse(
                    glowBrush,
                    glowBounds);

                context.Graphics.FillPath(
                    vesselBrush,
                    vesselPath);

                string vesselLabel =
                    FormatVesselName(
                        telemetry.VesselName);

                context.Graphics.DrawString(
                    vesselLabel,
                    context.SmallFont,
                    labelBrush,
                    vesselPoint.X +
                    14.0f,
                    vesselPoint.Y +
                    10.0f);
            }
        }

        private static double CalculateVesselAngle(
            MissionTelemetry telemetry)
        {
            double periapsis =
                telemetry.Periapsis;

            double apoapsis =
                telemetry.Apoapsis;

            double altitudeRange =
                apoapsis -
                periapsis;

            if (!IsFinite(altitudeRange) ||
                Math.Abs(altitudeRange) <
                1.0)
            {
                return
                    Math.PI /
                    3.0;
            }

            double altitudeFraction =
                (telemetry.Altitude -
                 periapsis) /
                altitudeRange;

            altitudeFraction =
                Clamp(
                    altitudeFraction,
                    0.0,
                    1.0);

            double cosine =
                altitudeFraction *
                2.0 -
                1.0;

            double angle =
                Math.Acos(
                    Clamp(
                        cosine,
                        -1.0,
                        1.0));

            /*
             * Positive vertical speed is treated as the ascending half of
             * the schematic orbit. Negative speed uses the descending half.
             */
            if (telemetry.VerticalSpeed < 0.0)
            {
                angle =
                    -angle;
            }

            return angle;
        }

        private static PointF GetEllipsePoint(
            RectangleF bounds,
            double angle)
        {
            double centerX =
                bounds.Left +
                bounds.Width /
                2.0;

            double centerY =
                bounds.Top +
                bounds.Height /
                2.0;

            double radiusX =
                bounds.Width /
                2.0;

            double radiusY =
                bounds.Height /
                2.0;

            return new PointF(
                (float)(
                    centerX +
                    Math.Cos(angle) *
                    radiusX),

                (float)(
                    centerY -
                    Math.Sin(angle) *
                    radiusY));
        }

        private static PointF CalculateTangent(
            RectangleF bounds,
            double angle)
        {
            float x =
                (float)(
                    -Math.Sin(angle) *
                    bounds.Width /
                    2.0);

            float y =
                (float)(
                    -Math.Cos(angle) *
                    bounds.Height /
                    2.0);

            return new PointF(
                x,
                y);
        }

        private static PointF Normalize(
            PointF value)
        {
            double length =
                Math.Sqrt(
                    value.X *
                    value.X +
                    value.Y *
                    value.Y);

            if (length <= 0.0001)
            {
                return new PointF(
                    1.0f,
                    0.0f);
            }

            return new PointF(
                (float)(
                    value.X /
                    length),

                (float)(
                    value.Y /
                    length));
        }

        private static void DrawPlaceholderOrbit(
    MissionRenderContext context,
    Rectangle bounds,
    MissionTelemetry telemetry)
        {
            RectangleF orbitBounds =
                new RectangleF(
                    bounds.Left +
                    bounds.Width *
                    0.08f,
                    bounds.Top +
                    bounds.Height *
                    0.20f,
                    bounds.Width *
                    0.84f,
                    bounds.Height *
                    0.60f);

            PointF bodyCenter =
                new PointF(
                    orbitBounds.Left +
                    orbitBounds.Width *
                    0.57f,
                    orbitBounds.Top +
                    orbitBounds.Height /
                    2.0f);

            float bodyRadius =
                Math.Max(
                    13.0f,
                    Math.Min(
                        bounds.Width,
                        bounds.Height) *
                    0.055f);

            using (Pen orbitPen =
                new Pen(
                    Color.FromArgb(
                        92,
                        context.DimPhosphorColor),
                    2.0f))
            {
                orbitPen.DashStyle =
                    DashStyle.Dash;

                context.Graphics.DrawEllipse(
                    orbitPen,
                    orbitBounds);
            }

            DrawCentralBody(
                context,
                bodyCenter,
                bodyRadius,
                telemetry.BodyName);

            PointF periapsisPoint =
                new PointF(
                    orbitBounds.Left,
                    orbitBounds.Top +
                    orbitBounds.Height /
                    2.0f);

            PointF apoapsisPoint =
                new PointF(
                    orbitBounds.Right,
                    orbitBounds.Top +
                    orbitBounds.Height /
                    2.0f);

            DrawPlaceholderApsisMarker(
                context,
                periapsisPoint,
                "PE",
                false);

            DrawPlaceholderApsisMarker(
                context,
                apoapsisPoint,
                "AP",
                true);

            DrawNoOrbitSolutionLabel(
                context,
                bounds);
        }

        private static void DrawPlaceholderApsisMarker(
    MissionRenderContext context,
    PointF point,
    string label,
    bool placeRight)
        {
            const float markerRadius = 4.0f;

            RectangleF markerBounds =
                new RectangleF(
                    point.X -
                    markerRadius,
                    point.Y -
                    markerRadius,
                    markerRadius *
                    2.0f,
                    markerRadius *
                    2.0f);

            using (SolidBrush markerBrush =
                new SolidBrush(
                    Color.FromArgb(
                        115,
                        context.DimPhosphorColor)))
            using (SolidBrush labelBrush =
                new SolidBrush(
                    Color.FromArgb(
                        135,
                        context.DimPhosphorColor)))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    placeRight
                        ? StringAlignment.Near
                        : StringAlignment.Far;

                context.Graphics.FillEllipse(
                    markerBrush,
                    markerBounds);

                RectangleF labelBounds =
                    new RectangleF(
                        placeRight
                            ? point.X + 10.0f
                            : point.X - 80.0f,
                        point.Y -
                        context.SmallFont.Height -
                        3.0f,
                        70.0f,
                        context.SmallFont.Height +
                        6.0f);

                context.Graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds,
                    format);
            }
        }

        private static void DrawNoOrbitSolutionLabel(
    MissionRenderContext context,
    Rectangle bounds)
        {
            int labelWidth =
                Math.Min(
                    300,
                    bounds.Width -
                    20);

            Rectangle labelBounds =
                new Rectangle(
                    bounds.Left +
                    (bounds.Width -
                     labelWidth) /
                    2,
                    bounds.Bottom -
                    58,
                    labelWidth,
                    34);

            using (SolidBrush backgroundBrush =
                new SolidBrush(
                    Color.FromArgb(
                        150,
                        2,
                        13,
                        18)))
            using (Pen borderPen =
                new Pen(
                    Color.FromArgb(
                        90,
                        context.DimPhosphorColor),
                    1.0f))
            using (SolidBrush textBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                context.Graphics.FillRectangle(
                    backgroundBrush,
                    labelBounds);

                context.Graphics.DrawRectangle(
                    borderPen,
                    labelBounds);

                context.Graphics.DrawString(
                    "NO ORBIT SOLUTION",
                    context.SmallFont,
                    textBrush,
                    labelBounds,
                    format);
            }
        }

        private static bool HasUsableOrbitData(
            MissionTelemetry telemetry)
        {
            return
                IsFinite(
                    telemetry.Apoapsis) &&
                IsFinite(
                    telemetry.Periapsis) &&
                telemetry.Apoapsis > 0.0 &&
                telemetry.Apoapsis >=
                telemetry.Periapsis;
        }

        private static string FormatDistance(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double absolute =
                Math.Abs(value);

            if (absolute >= 1000000.0)
            {
                return
                    (value /
                     1000000.0)
                    .ToString(
                        "0.00") +
                    " MM";
            }

            if (absolute >= 1000.0)
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
                    "0") +
                " M";
        }

        private static string FormatBodyName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "BODY";
            }

            return value
                .Trim()
                .ToUpperInvariant();
        }

        private static string FormatVesselName(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "VESSEL";
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