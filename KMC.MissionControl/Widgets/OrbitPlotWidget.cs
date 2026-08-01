using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Widgets
{
    /// <summary>
    /// Apollo-style orbital trajectory display with a fixed-body camera.
    ///
    /// Kerbin remains anchored in the plot while the predicted trajectory
    /// grows, contracts, and rotates around it as telemetry changes.
    /// </summary>
    public sealed class OrbitPlotWidget : IMissionWidget
    {
        private const int PanelPadding = 18;
        private const int HeaderHeight = 42;
        private const int PlotPadding = 28;
        private const int OrbitSampleCount = 180;
        private const double RotationLockAltitude = 10000.0;
        private const double RotationBlendEndAltitude = 25000.0;
        private const double CircularOrbitThreshold = 0.01;

        private bool _hasPlotRotation;
        private double _plotRotationDegrees;

        /*
         * The camera will not zoom closer than this many body radii.
         * This lets low-altitude trajectories visibly grow away from
         * the surface before the camera begins zooming out.
         */
        private const double MinimumViewBodyRadii = 1.28;

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

            if (plotBounds.Width <= 40 ||
                plotBounds.Height <= 40)
            {
                return;
            }

            DrawBackgroundGrid(
                context,
                plotBounds);

            if (IsGrounded(
                telemetry))
            {
                _hasPlotRotation =
                    false;

                DrawGroundedState(
                    context,
                    plotBounds,
                    telemetry);

                return;
            }

            if (!HasSupportedOrbit(
                    telemetry))
            {
                DrawUnavailableState(
                    context,
                    plotBounds,
                    telemetry);

                return;
            }

            DrawKeplerianOrbit(
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
                        145,
                        context.DimPhosphorColor),
                    2.0f))
            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                

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
            const int minorSpacing = 36;
            const int majorSpacing = 108;

            using (Pen majorPen =
                new Pen(
                    Color.FromArgb(
                        48,
                        context.DimPhosphorColor),
                    1.0f))
            using (Pen minorPen =
                new Pen(
                    Color.FromArgb(
                        20,
                        context.DimPhosphorColor),
                    1.0f))
            {
                majorPen.DashStyle =
                    DashStyle.Dot;

                minorPen.DashStyle =
                    DashStyle.Dot;

                for (int x = bounds.Left;
                     x <= bounds.Right;
                     x += minorSpacing)
                {
                    bool major =
                        (x - bounds.Left) %
                        majorSpacing ==
                        0;

                    context.Graphics.DrawLine(
                        major
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
                    bool major =
                        (y - bounds.Top) %
                        majorSpacing ==
                        0;

                    context.Graphics.DrawLine(
                        major
                            ? majorPen
                            : minorPen,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private void DrawKeplerianOrbit(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            double eccentricity =
                Clamp(
                    telemetry.Eccentricity,
                    0.0,
                    0.999999);

            double semiMajorAxis =
                telemetry.SemiMajorAxis;

            double plotRotationDegrees =
                ResolvePlotRotationDegrees(
                telemetry);

            double rotationRadians =
                DegreesToRadians(
                    plotRotationDegrees);

            double periapsisRadius =
                semiMajorAxis *
                (1.0 -
                 eccentricity);

            double apoapsisRadius =
                semiMajorAxis *
                (1.0 +
                 eccentricity);

            double bodyRadius =
                EstimateBodyRadius(
                    telemetry,
                    periapsisRadius,
                    apoapsisRadius);

            List<PointD> orbitPoints =
                BuildOrbitModel(
                    semiMajorAxis,
                    eccentricity,
                    rotationRadians);

            OrbitCamera camera =
                CreateFixedBodyCamera(
                    plotBounds,
                    orbitPoints,
                    bodyRadius);

            PointF[] screenOrbit =
                TransformPoints(
                    orbitPoints,
                    camera);

            PointF bodyPoint =
                camera.BodyScreenPosition;

            PointD periapsisModel =
                Rotate(
                    new PointD(
                        periapsisRadius,
                        0.0),
                    rotationRadians);

            PointD apoapsisModel =
                Rotate(
                    new PointD(
                        -apoapsisRadius,
                        0.0),
                    rotationRadians);

            PointF periapsisPoint =
                camera.WorldToScreen(
                    periapsisModel);

            PointF apoapsisPoint =
                camera.WorldToScreen(
                    apoapsisModel);

            double trueAnomalyRadians =
                DegreesToRadians(
                    NormalizeDegrees(
                        telemetry.TrueAnomalyDegrees));

            PointD vesselModel =
                CalculateOrbitPoint(
                    semiMajorAxis,
                    eccentricity,
                    trueAnomalyRadians,
                    rotationRadians);

            PointF vesselPoint =
                camera.WorldToScreen(
                    vesselModel);

            PointD tangentModel =
                CalculateOrbitTangent(
                    semiMajorAxis,
                    eccentricity,
                    trueAnomalyRadians,
                    rotationRadians);

            PointF tangent =
                new PointF(
                    (float)tangentModel.X,
                    (float)-tangentModel.Y);

            DrawReferenceAxes(
                context,
                plotBounds,
                bodyPoint);

            DrawOrbitPath(
                context,
                screenOrbit);

            float bodyRadiusPixels =
                (float)(
                    bodyRadius *
                    camera.PixelsPerMeter);

            bodyRadiusPixels =
                ClampFloat(
                    bodyRadiusPixels,
                    12.0f,
                    Math.Min(
                        plotBounds.Width,
                        plotBounds.Height) *
                    0.42f);

            DrawCentralBody(
                context,
                bodyPoint,
                bodyRadiusPixels,
                telemetry.BodyName);

            DrawApsisMarker(
                context,
                apoapsisPoint,
                "AP",
                telemetry.Apoapsis,
                true,
                plotBounds);

            DrawApsisMarker(
                context,
                periapsisPoint,
                "PE",
                telemetry.Periapsis,
                false,
                plotBounds);

            DrawVesselMarker(
                context,
                vesselPoint,
                tangent);

            DrawOrbitLegend(
                context,
                plotBounds,
                telemetry);
        }

        private double ResolvePlotRotationDegrees(
    MissionTelemetry telemetry)
        {
            double targetDegrees =
                NormalizeDegrees(
                    telemetry
                        .ArgumentOfPeriapsisDegrees);

            if (!_hasPlotRotation)
            {
                _plotRotationDegrees =
                    targetDegrees;

                _hasPlotRotation =
                    true;

                return _plotRotationDegrees;
            }

            /*
             * Argument of periapsis becomes visually unstable for an almost
             * circular orbit because the location of periapsis is poorly defined.
             * Hold the last useful display orientation in that condition.
             */
            if (telemetry.Eccentricity <=
                CircularOrbitThreshold)
            {
                return _plotRotationDegrees;
            }

            /*
             * During the first portion of ascent, hold the orientation captured
             * immediately after liftoff. KSP's orbital frame can rotate rapidly
             * while the trajectory is still dominated by surface rotation,
             * atmosphere, and continuous thrust.
             */
            if (telemetry.Altitude <=
                RotationLockAltitude)
            {
                return _plotRotationDegrees;
            }

            double blend =
                (telemetry.Altitude -
                 RotationLockAltitude) /
                (RotationBlendEndAltitude -
                 RotationLockAltitude);

            blend =
                Clamp(
                    blend,
                    0.0,
                    1.0);

            /*
             * Use shortest-angle interpolation so a transition such as
             * 359 degrees to 1 degree moves two degrees rather than rotating
             * backward through almost a complete circle.
             */
            double angleDifference =
                GetShortestAngleDifference(
                    _plotRotationDegrees,
                    targetDegrees);

            double interpolationAmount =
                0.02 +
                blend *
                0.16;

            double maximumStep =
                0.5 +
                blend *
                7.5;

            double requestedStep =
                angleDifference *
                interpolationAmount;

            double appliedStep =
                Clamp(
                    requestedStep,
                    -maximumStep,
                    maximumStep);

            _plotRotationDegrees =
                NormalizeDegrees(
                    _plotRotationDegrees +
                    appliedStep);

            return _plotRotationDegrees;
        }

        private static double EstimateBodyRadius(
            MissionTelemetry telemetry,
            double periapsisRadius,
            double apoapsisRadius)
        {
            /*
             * KSP sends apsis altitudes above the body's reference surface,
             * while the Keplerian radii are measured from the body's center.
             * Their difference therefore gives us the body's reference radius.
             */
            double fromApoapsis =
                apoapsisRadius -
                telemetry.Apoapsis;

            double fromPeriapsis =
                periapsisRadius -
                telemetry.Periapsis;

            bool apoapsisValid =
                IsFinite(
                    fromApoapsis) &&
                fromApoapsis >
                1000.0;

            bool periapsisValid =
                IsFinite(
                    fromPeriapsis) &&
                fromPeriapsis >
                1000.0;

            if (apoapsisValid &&
                periapsisValid)
            {
                return
                    (fromApoapsis +
                     fromPeriapsis) /
                    2.0;
            }

            if (apoapsisValid)
            {
                return fromApoapsis;
            }

            if (periapsisValid)
            {
                return fromPeriapsis;
            }

            /*
             * Fallback only. Normal live telemetry should use one of the
             * derived values above.
             */
            return EstimateFallbackBodyRadius(
                telemetry);
        }

        private static double EstimateFallbackBodyRadius(
            MissionTelemetry telemetry)
        {
            return Math.Max(
                1000.0,
                telemetry.SemiMajorAxis *
                0.5);
        }

        private static OrbitCamera CreateFixedBodyCamera(
            Rectangle plotBounds,
            IList<PointD> orbitPoints,
            double bodyRadius)
        {
            /*
             * The body remains fixed in the left third of the panel.
             * The camera scale changes only when the trajectory reaches
             * the current view boundary.
             */
            PointF bodyScreenPosition =
                new PointF(
                    plotBounds.Left +
                    plotBounds.Width *
                    0.30f,

                    plotBounds.Top +
                    plotBounds.Height *
                    0.50f);

            double minimumExtent =
                bodyRadius *
                MinimumViewBodyRadii;

            double positiveX =
                minimumExtent;

            double negativeX =
                minimumExtent;

            double positiveY =
                minimumExtent;

            double negativeY =
                minimumExtent;

            foreach (PointD point in orbitPoints)
            {
                if (point.X >= 0.0)
                {
                    positiveX =
                        Math.Max(
                            positiveX,
                            point.X);
                }
                else
                {
                    negativeX =
                        Math.Max(
                            negativeX,
                            -point.X);
                }

                if (point.Y >= 0.0)
                {
                    positiveY =
                        Math.Max(
                            positiveY,
                            point.Y);
                }
                else
                {
                    negativeY =
                        Math.Max(
                            negativeY,
                            -point.Y);
                }
            }

            double availableLeft =
                Math.Max(
                    1.0,
                    bodyScreenPosition.X -
                    plotBounds.Left -
                    12.0);

            double availableRight =
                Math.Max(
                    1.0,
                    plotBounds.Right -
                    bodyScreenPosition.X -
                    12.0);

            double availableTop =
                Math.Max(
                    1.0,
                    bodyScreenPosition.Y -
                    plotBounds.Top -
                    12.0);

            double availableBottom =
                Math.Max(
                    1.0,
                    plotBounds.Bottom -
                    bodyScreenPosition.Y -
                    12.0);

            double pixelsPerMeter =
                Math.Min(
                    Math.Min(
                        availableRight /
                        positiveX,

                        availableLeft /
                        negativeX),

                    Math.Min(
                        availableTop /
                        positiveY,

                        availableBottom /
                        negativeY));

            return new OrbitCamera(
                bodyScreenPosition,
                Math.Max(
                    0.0000001,
                    pixelsPerMeter));
        }

        private static List<PointD> BuildOrbitModel(
            double semiMajorAxis,
            double eccentricity,
            double rotationRadians)
        {
            List<PointD> points =
                new List<PointD>(
                    OrbitSampleCount +
                    1);

            for (int index = 0;
                 index <= OrbitSampleCount;
                 index++)
            {
                double trueAnomaly =
                    Math.PI *
                    2.0 *
                    index /
                    OrbitSampleCount;

                points.Add(
                    CalculateOrbitPoint(
                        semiMajorAxis,
                        eccentricity,
                        trueAnomaly,
                        rotationRadians));
            }

            return points;
        }

        private static PointD CalculateOrbitPoint(
            double semiMajorAxis,
            double eccentricity,
            double trueAnomaly,
            double rotationRadians)
        {
            double denominator =
                1.0 +
                eccentricity *
                Math.Cos(
                    trueAnomaly);

            if (Math.Abs(
                    denominator) <
                0.0000001)
            {
                denominator =
                    0.0000001;
            }

            double radius =
                semiMajorAxis *
                (1.0 -
                 eccentricity *
                 eccentricity) /
                denominator;

            PointD unrotated =
                new PointD(
                    radius *
                    Math.Cos(
                        trueAnomaly),

                    radius *
                    Math.Sin(
                        trueAnomaly));

            return Rotate(
                unrotated,
                rotationRadians);
        }

        private static PointD CalculateOrbitTangent(
            double semiMajorAxis,
            double eccentricity,
            double trueAnomaly,
            double rotationRadians)
        {
            const double delta = 0.0005;

            PointD before =
                CalculateOrbitPoint(
                    semiMajorAxis,
                    eccentricity,
                    trueAnomaly -
                    delta,
                    rotationRadians);

            PointD after =
                CalculateOrbitPoint(
                    semiMajorAxis,
                    eccentricity,
                    trueAnomaly +
                    delta,
                    rotationRadians);

            return new PointD(
                after.X -
                before.X,
                after.Y -
                before.Y);
        }

        private static PointD Rotate(
            PointD point,
            double radians)
        {
            double cosine =
                Math.Cos(
                    radians);

            double sine =
                Math.Sin(
                    radians);

            return new PointD(
                point.X *
                cosine -
                point.Y *
                sine,

                point.X *
                sine +
                point.Y *
                cosine);
        }

        private static PointF[] TransformPoints(
            IList<PointD> points,
            OrbitCamera camera)
        {
            PointF[] result =
                new PointF[
                    points.Count];

            for (int index = 0;
                 index < points.Count;
                 index++)
            {
                result[index] =
                    camera.WorldToScreen(
                        points[index]);
            }

            return result;
        }

        private static void DrawReferenceAxes(
            MissionRenderContext context,
            Rectangle plotBounds,
            PointF bodyPoint)
        {
            using (Pen axisPen =
                new Pen(
                    Color.FromArgb(
                        48,
                        context.DimPhosphorColor),
                    1.0f))
            {
                axisPen.DashStyle =
                    DashStyle.Dot;

                context.Graphics.DrawLine(
                    axisPen,
                    plotBounds.Left,
                    bodyPoint.Y,
                    plotBounds.Right,
                    bodyPoint.Y);

                context.Graphics.DrawLine(
                    axisPen,
                    bodyPoint.X,
                    plotBounds.Top,
                    bodyPoint.X,
                    plotBounds.Bottom);
            }
        }

        private static void DrawOrbitPath(
            MissionRenderContext context,
            PointF[] points)
        {
            if (points == null ||
                points.Length <
                2)
            {
                return;
            }

            using (Pen glowPen =
                new Pen(
                    Color.FromArgb(
                        34,
                        context.PhosphorColor),
                    5.0f))
            using (Pen orbitPen =
                new Pen(
                    Color.FromArgb(
                        205,
                        context.PhosphorColor),
                    1.5f))
            {
                context.Graphics.DrawLines(
                    glowPen,
                    points);

                context.Graphics.DrawLines(
                    orbitPen,
                    points);
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
                    center.X - radius,
                    center.Y - radius,
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
            {
                context.Graphics.FillEllipse(
                    bodyBrush,
                    bodyBounds);

                context.Graphics.DrawEllipse(
                    bodyPen,
                    bodyBounds);
            }
        }

        private static void DrawApsisMarker(
            MissionRenderContext context,
            PointF point,
            string label,
            double altitude,
            bool apoapsis,
            Rectangle plotBounds)
        {
            const float markerRadius = 4.5f;
            const float labelWidth = 170.0f;

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

            string text =
                label +
                "  " +
                FormatDistance(
                    altitude);

            float labelX =
                apoapsis
                    ? point.X -
                      labelWidth -
                      10.0f
                    : point.X +
                      10.0f;

            float labelY =
                apoapsis
                    ? point.Y -
                      context.SmallFont.Height -
                      10.0f
                    : point.Y +
                      9.0f;

            labelX =
                ClampFloat(
                    labelX,
                    plotBounds.Left,
                    plotBounds.Right -
                    labelWidth);

            labelY =
                ClampFloat(
                    labelY,
                    plotBounds.Top,
                    plotBounds.Bottom -
                    context.SmallFont.Height -
                    4.0f);

            using (SolidBrush markerBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (SolidBrush textBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    apoapsis
                        ? StringAlignment.Far
                        : StringAlignment.Near;

                context.Graphics.FillEllipse(
                    markerBrush,
                    markerBounds);

                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    textBrush,
                    new RectangleF(
                        labelX,
                        labelY,
                        labelWidth,
                        context.SmallFont.Height +
                        6.0f),
                    format);
            }
        }

        private static void DrawVesselMarker(
            MissionRenderContext context,
            PointF vesselPoint,
            PointF tangent)
        {
            PointF direction =
                Normalize(
                    tangent);

            const float markerLength = 15.0f;
            const float markerHalfWidth = 6.0f;

            PointF perpendicular =
                new PointF(
                    -direction.Y,
                    direction.X);

            PointF nose =
                new PointF(
                    vesselPoint.X +
                    direction.X *
                    markerLength,

                    vesselPoint.Y +
                    direction.Y *
                    markerLength);

            PointF left =
                new PointF(
                    vesselPoint.X -
                    direction.X *
                    6.0f +
                    perpendicular.X *
                    markerHalfWidth,

                    vesselPoint.Y -
                    direction.Y *
                    6.0f +
                    perpendicular.Y *
                    markerHalfWidth);

            PointF right =
                new PointF(
                    vesselPoint.X -
                    direction.X *
                    6.0f -
                    perpendicular.X *
                    markerHalfWidth,

                    vesselPoint.Y -
                    direction.Y *
                    6.0f -
                    perpendicular.Y *
                    markerHalfWidth);

            using (GraphicsPath path =
                new GraphicsPath())
            using (SolidBrush glowBrush =
                new SolidBrush(
                    Color.FromArgb(
                        65,
                        context.PhosphorColor)))
            using (SolidBrush markerBrush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                path.AddPolygon(
                    new[]
                    {
                        nose,
                        left,
                        right
                    });

                context.Graphics.FillEllipse(
                    glowBrush,
                    vesselPoint.X -
                    14.0f,
                    vesselPoint.Y -
                    14.0f,
                    28.0f,
                    28.0f);

                context.Graphics.FillPath(
                    markerBrush,
                    path);
            }
        }

        private static void DrawOrbitLegend(
    MissionRenderContext context,
    Rectangle plotBounds,
    MissionTelemetry telemetry)
        {
            const int horizontalPadding = 10;
            const int verticalPadding = 3;
            const int sectionGap = 14;
            const int dividerGap = 8;
            const int legendWidth = 240;

            /*
             * MissionRenderContext does not currently expose TinyFont,
             * so derive a smaller legend font from SmallFont.
             */
            float legendFontSize =
                Math.Max(
                    6.0f,
                    context.SmallFont.Size -
                    2.0f);

            using (Font legendFont =
                new Font(
                    context.SmallFont.FontFamily,
                    legendFontSize,
                    context.SmallFont.Style,
                    GraphicsUnit.Point))
            using (Pen dividerPen =
                new Pen(
                    Color.FromArgb(
                        80,
                        context.DimPhosphorColor),
                    1.0f))
            {
                int rowHeight =
                    legendFont.Height +
                    2;

                int legendHeight =
                    verticalPadding * 2 +
                    rowHeight;

                Rectangle legendBounds =
                    new Rectangle(
                        plotBounds.Left +
                        (plotBounds.Width -
                         legendWidth) /
                        2,

                        plotBounds.Bottom -
                        legendHeight -
                        6,

                        legendWidth,
                        legendHeight);

                int contentY =
                    legendBounds.Top +
                    verticalPadding;

                int currentX =
                    legendBounds.Left +
                    horizontalPadding;

                currentX =
                    DrawCompactLegendField(
                        context,
                        legendFont,
                        "BODY",
                        FormatBodyName(
                            telemetry.BodyName),
                        currentX,
                        contentY,
                        rowHeight);

                int dividerX =
                    currentX +
                    dividerGap;

                context.Graphics.DrawLine(
                    dividerPen,
                    dividerX,
                    legendBounds.Top +
                    2,
                    dividerX,
                    legendBounds.Bottom -
                    2);

                currentX =
                    dividerX +
                    sectionGap;

                DrawCompactLegendField(
                    context,
                    legendFont,
                    "TYPE",
                    GetOrbitType(
                        telemetry),
                    currentX,
                    contentY,
                    rowHeight);
            }
        }

        private static int DrawCompactLegendField(
    MissionRenderContext context,
    Font legendFont,
    string label,
    string value,
    int x,
    int y,
    int rowHeight)
        {
            const int labelValueGap = 7;

            SizeF labelSize =
                context.Graphics.MeasureString(
                    label,
                    legendFont);

            SizeF valueSize =
                context.Graphics.MeasureString(
                    value,
                    legendFont);

            int labelWidth =
                (int)Math.Ceiling(
                    labelSize.Width);

            int valueWidth =
                (int)Math.Ceiling(
                    valueSize.Width);

            RectangleF labelBounds =
                new RectangleF(
                    x,
                    y,
                    labelWidth +
                    2,
                    rowHeight);

            int valueX =
                x +
                labelWidth +
                labelValueGap;

            RectangleF valueBounds =
                new RectangleF(
                    valueX,
                    y,
                    valueWidth +
                    4,
                    rowHeight);

            using (SolidBrush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (SolidBrush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Near;

                format.LineAlignment =
                    StringAlignment.Center;

                format.FormatFlags =
                    StringFormatFlags.NoWrap;

                context.Graphics.DrawString(
                    label,
                    legendFont,
                    labelBrush,
                    labelBounds,
                    format);

                context.Graphics.DrawString(
                    value,
                    legendFont,
                    valueBrush,
                    valueBounds,
                    format);
            }

            return
                valueX +
                valueWidth;
        }

        private static void DrawGroundedState(
    MissionRenderContext context,
    Rectangle plotBounds,
    MissionTelemetry telemetry)
        {
            float bodyRadius =
                Math.Min(
                    plotBounds.Width,
                    plotBounds.Height) *
                0.20f;

            PointF bodyPoint =
                new PointF(
                    plotBounds.Left +
                    plotBounds.Width *
                    0.28f,

                    plotBounds.Top +
                    plotBounds.Height *
                    0.42f);

            DrawCentralBody(
                context,
                bodyPoint,
                bodyRadius,
                telemetry.BodyName);

            Rectangle statusBounds =
                new Rectangle(
                    (int)(
                        plotBounds.Left +
                        plotBounds.Width *
                        0.48f),

                    (int)(
                        plotBounds.Top +
                        plotBounds.Height *
                        0.32f),

                    (int)(
                        plotBounds.Width *
                        0.47f),

                    (int)(
                        plotBounds.Height *
                        0.30f));

            DrawCenteredStatus(
                context,
                statusBounds,
                "VESSEL GROUNDED",
                "AWAITING ASCENT");
        }

        private static void DrawUnavailableState(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            PointF bodyPoint =
                new PointF(
                    plotBounds.Left +
                    plotBounds.Width *
                    0.30f,

                    plotBounds.Top +
                    plotBounds.Height *
                    0.50f);

            DrawCentralBody(
                context,
                bodyPoint,
                Math.Min(
                    plotBounds.Width,
                    plotBounds.Height) *
                0.18f,
                telemetry.BodyName);

            string detail =
                telemetry.Eccentricity >=
                1.0
                    ? "OPEN TRAJECTORY"
                    : "ORBIT DATA UNAVAILABLE";

            DrawCenteredStatus(
                context,
                plotBounds,
                "NO ORBIT SOLUTION",
                detail);
        }

        private static void DrawCenteredStatus(
    MissionRenderContext context,
    Rectangle bounds,
    string title,
    string detail)
        {
            int rowHeight =
                context.SmallFont.Height +
                8;

            int totalHeight =
                rowHeight *
                2;

            int startY =
                bounds.Top +
                Math.Max(
                    0,
                    (bounds.Height -
                     totalHeight) /
                    2);

            RectangleF titleBounds =
                new RectangleF(
                    bounds.Left,
                    startY,
                    bounds.Width,
                    rowHeight);

            RectangleF detailBounds =
                new RectangleF(
                    bounds.Left,
                    startY +
                    rowHeight,
                    bounds.Width,
                    rowHeight);

            using (SolidBrush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (SolidBrush detailBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (StringFormat format =
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                format.FormatFlags =
                    StringFormatFlags.NoWrap;

                context.Graphics.DrawString(
                    title,
                    context.SmallFont,
                    titleBrush,
                    titleBounds,
                    format);

                context.Graphics.DrawString(
                    detail,
                    context.SmallFont,
                    detailBrush,
                    detailBounds,
                    format);
            }
        }

        private static bool IsGrounded(
            MissionTelemetry telemetry)
        {
            return
                telemetry.Altitude <
                500.0 &&
                Math.Abs(
                    telemetry.SurfaceSpeed) <
                1.0 &&
                Math.Abs(
                    telemetry.VerticalSpeed) <
                1.0;
        }

        private static bool HasSupportedOrbit(
            MissionTelemetry telemetry)
        {
            return
                IsFinite(
                    telemetry.Eccentricity) &&
                IsFinite(
                    telemetry.SemiMajorAxis) &&
                IsFinite(
                    telemetry.TrueAnomalyDegrees) &&
                IsFinite(
                    telemetry
                        .ArgumentOfPeriapsisDegrees) &&
                telemetry.Eccentricity >=
                0.0 &&
                telemetry.Eccentricity <
                1.0 &&
                telemetry.SemiMajorAxis >
                0.0;
        }

        private static string GetOrbitType(
            MissionTelemetry telemetry)
        {
            if (telemetry.Periapsis <
                0.0)
            {
                return "SUBORBITAL";
            }

            if (telemetry.Eccentricity <=
                0.03)
            {
                return "CIRCULAR";
            }

            return "ELLIPTICAL";
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

            if (absolute >=
                1000000.0)
            {
                return
                    (value /
                     1000000.0)
                    .ToString(
                        "0.00") +
                    " MM";
            }

            if (absolute >=
                1000.0)
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

            if (result.Length >
                22)
            {
                result =
                    result.Substring(
                        0,
                        22);
            }

            return result;
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

            if (length <=
                0.0001)
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

        private static double DegreesToRadians(
            double degrees)
        {
            return
                degrees *
                Math.PI /
                180.0;
        }

        private static double NormalizeDegrees(
            double value)
        {
            double normalized =
                value %
                360.0;

            if (normalized <
                0.0)
            {
                normalized +=
                    360.0;
            }

            return normalized;
        }

        private static double GetShortestAngleDifference(
    double fromDegrees,
    double toDegrees)
        {
            double difference =
                NormalizeDegrees(
                    toDegrees) -
                NormalizeDegrees(
                    fromDegrees);

            if (difference >
                180.0)
            {
                difference -=
                    360.0;
            }
            else if (difference <
                     -180.0)
            {
                difference +=
                    360.0;
            }

            return difference;
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

        private static float ClampFloat(
            float value,
            float minimum,
            float maximum)
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

        private struct PointD
        {
            public PointD(
                double x,
                double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }

            public double Y { get; }
        }

        private struct OrbitCamera
        {
            public OrbitCamera(
                PointF bodyScreenPosition,
                double pixelsPerMeter)
            {
                BodyScreenPosition =
                    bodyScreenPosition;

                PixelsPerMeter =
                    pixelsPerMeter;
            }

            public PointF BodyScreenPosition { get; }

            public double PixelsPerMeter { get; }

            public PointF WorldToScreen(
                PointD worldPoint)
            {
                return new PointF(
                    BodyScreenPosition.X +
                    (float)(
                        worldPoint.X *
                        PixelsPerMeter),

                    BodyScreenPosition.Y -
                    (float)(
                        worldPoint.Y *
                        PixelsPerMeter));
            }
        }
    }
}