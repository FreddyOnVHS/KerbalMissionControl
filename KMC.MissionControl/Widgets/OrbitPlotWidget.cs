using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace KMC.MissionControl.Widgets
{
    /// <summary>
    /// Apollo-style orbital trajectory display.
    ///
    /// Elliptical and suborbital trajectories are generated from the
    /// vessel's Keplerian elements. The plot is redrawn whenever new
    /// telemetry arrives, allowing the orbit and vessel marker to react
    /// dynamically during flight.
    /// </summary>
    public sealed class OrbitPlotWidget : IMissionWidget
    {
        private const int PanelPadding = 18;
        private const int HeaderHeight = 42;
        private const int PlotPadding = 28;
        private const int OrbitSampleCount = 180;

        public void Draw(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (telemetry == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            DrawPanelFrame(context, bounds);

            Rectangle plotBounds = GetPlotBounds(bounds);

            if (plotBounds.Width <= 40 || plotBounds.Height <= 40)
            {
                return;
            }

            DrawBackgroundGrid(context, plotBounds);

            if (IsGrounded(telemetry))
            {
                DrawGroundedState(context, plotBounds, telemetry);
                return;
            }

            if (!HasSupportedOrbit(telemetry))
            {
                DrawUnavailableState(context, plotBounds, telemetry);
                return;
            }

            DrawKeplerianOrbit(context, plotBounds, telemetry);
        }

        private static Rectangle GetPlotBounds(Rectangle bounds)
        {
            return new Rectangle(
                bounds.Left + PanelPadding + PlotPadding,
                bounds.Top + HeaderHeight + PlotPadding,
                Math.Max(1, bounds.Width - PanelPadding * 2 - PlotPadding * 2),
                Math.Max(1, bounds.Height - HeaderHeight - PanelPadding - PlotPadding * 2));
        }

        private static void DrawPanelFrame(
            MissionRenderContext context,
            Rectangle bounds)
        {
            using (SolidBrush backgroundBrush = new SolidBrush(Color.FromArgb(72, 3, 17, 23)))
            using (Pen borderPen = new Pen(Color.FromArgb(145, context.DimPhosphorColor), 2.0f))
            using (SolidBrush titleBrush = new SolidBrush(context.PhosphorColor))
            {
                context.Graphics.FillRectangle(backgroundBrush, bounds);
                context.Graphics.DrawRectangle(borderPen, bounds);
                context.Graphics.DrawString(
                    "ORBIT PLOT",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + PanelPadding,
                    bounds.Top + 8);

                int dividerY = bounds.Top + HeaderHeight;
                context.Graphics.DrawLine(
                    borderPen,
                    bounds.Left + PanelPadding,
                    dividerY,
                    bounds.Right - PanelPadding,
                    dividerY);
            }
        }

        private static void DrawBackgroundGrid(
            MissionRenderContext context,
            Rectangle bounds)
        {
            const int minorSpacing = 36;
            const int majorSpacing = 108;

            using (Pen majorPen = new Pen(Color.FromArgb(48, context.DimPhosphorColor), 1.0f))
            using (Pen minorPen = new Pen(Color.FromArgb(20, context.DimPhosphorColor), 1.0f))
            {
                majorPen.DashStyle = DashStyle.Dot;
                minorPen.DashStyle = DashStyle.Dot;

                for (int x = bounds.Left; x <= bounds.Right; x += minorSpacing)
                {
                    bool major = (x - bounds.Left) % majorSpacing == 0;
                    context.Graphics.DrawLine(
                        major ? majorPen : minorPen,
                        x,
                        bounds.Top,
                        x,
                        bounds.Bottom);
                }

                for (int y = bounds.Top; y <= bounds.Bottom; y += minorSpacing)
                {
                    bool major = (y - bounds.Top) % majorSpacing == 0;
                    context.Graphics.DrawLine(
                        major ? majorPen : minorPen,
                        bounds.Left,
                        y,
                        bounds.Right,
                        y);
                }
            }
        }

        private static void DrawKeplerianOrbit(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            double eccentricity = Clamp(telemetry.Eccentricity, 0.0, 0.999999);
            double semiMajorAxis = telemetry.SemiMajorAxis;
            double rotationRadians = DegreesToRadians(telemetry.ArgumentOfPeriapsisDegrees);

            List<PointD> modelOrbit = BuildOrbitModel(
                semiMajorAxis,
                eccentricity,
                rotationRadians);

            OrbitTransform transform = CalculateTransform(modelOrbit, plotBounds);
            PointF[] screenOrbit = TransformPoints(modelOrbit, transform);
            PointF bodyPoint = transform.Transform(new PointD(0.0, 0.0));

            double periapsisRadius = semiMajorAxis * (1.0 - eccentricity);
            double apoapsisRadius = semiMajorAxis * (1.0 + eccentricity);

            PointF periapsisPoint = transform.Transform(
                Rotate(new PointD(periapsisRadius, 0.0), rotationRadians));

            PointF apoapsisPoint = transform.Transform(
                Rotate(new PointD(-apoapsisRadius, 0.0), rotationRadians));

            double trueAnomalyRadians = DegreesToRadians(
                NormalizeDegrees(telemetry.TrueAnomalyDegrees));

            PointD vesselModel = CalculateOrbitPoint(
                semiMajorAxis,
                eccentricity,
                trueAnomalyRadians,
                rotationRadians);

            PointF vesselPoint = transform.Transform(vesselModel);

            PointD tangentModel = CalculateOrbitTangent(
                semiMajorAxis,
                eccentricity,
                trueAnomalyRadians,
                rotationRadians);

            PointF tangent = new PointF(
                (float)tangentModel.X,
                (float)-tangentModel.Y);

            DrawReferenceAxes(context, plotBounds, bodyPoint);
            DrawOrbitPath(context, screenOrbit);

            float bodyRadius = Math.Max(
                11.0f,
                Math.Min(plotBounds.Width, plotBounds.Height) * 0.045f);

            DrawCentralBody(
                context,
                bodyPoint,
                bodyRadius,
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

            DrawVesselMarker(context, vesselPoint, tangent);
            DrawOrbitLegend(context, plotBounds, telemetry);
        }

        private static List<PointD> BuildOrbitModel(
            double semiMajorAxis,
            double eccentricity,
            double rotationRadians)
        {
            List<PointD> points = new List<PointD>(OrbitSampleCount + 1);

            for (int index = 0; index <= OrbitSampleCount; index++)
            {
                double trueAnomaly = Math.PI * 2.0 * index / OrbitSampleCount;
                points.Add(CalculateOrbitPoint(
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
            double denominator = 1.0 + eccentricity * Math.Cos(trueAnomaly);

            if (Math.Abs(denominator) < 0.0000001)
            {
                denominator = 0.0000001;
            }

            double radius = semiMajorAxis *
                (1.0 - eccentricity * eccentricity) /
                denominator;

            PointD unrotated = new PointD(
                radius * Math.Cos(trueAnomaly),
                radius * Math.Sin(trueAnomaly));

            return Rotate(unrotated, rotationRadians);
        }

        private static PointD CalculateOrbitTangent(
            double semiMajorAxis,
            double eccentricity,
            double trueAnomaly,
            double rotationRadians)
        {
            const double delta = 0.0005;

            PointD before = CalculateOrbitPoint(
                semiMajorAxis,
                eccentricity,
                trueAnomaly - delta,
                rotationRadians);

            PointD after = CalculateOrbitPoint(
                semiMajorAxis,
                eccentricity,
                trueAnomaly + delta,
                rotationRadians);

            return new PointD(after.X - before.X, after.Y - before.Y);
        }

        private static PointD Rotate(PointD point, double radians)
        {
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);

            return new PointD(
                point.X * cosine - point.Y * sine,
                point.X * sine + point.Y * cosine);
        }

        private static OrbitTransform CalculateTransform(
            IList<PointD> points,
            Rectangle plotBounds)
        {
            double minimumX = double.MaxValue;
            double maximumX = double.MinValue;
            double minimumY = double.MaxValue;
            double maximumY = double.MinValue;

            foreach (PointD point in points)
            {
                minimumX = Math.Min(minimumX, point.X);
                maximumX = Math.Max(maximumX, point.X);
                minimumY = Math.Min(minimumY, point.Y);
                maximumY = Math.Max(maximumY, point.Y);
            }

            minimumX = Math.Min(minimumX, 0.0);
            maximumX = Math.Max(maximumX, 0.0);
            minimumY = Math.Min(minimumY, 0.0);
            maximumY = Math.Max(maximumY, 0.0);

            double modelWidth = Math.Max(1.0, maximumX - minimumX);
            double modelHeight = Math.Max(1.0, maximumY - minimumY);
            const double fitFactor = 0.88;

            double scale = Math.Min(
                plotBounds.Width / modelWidth,
                plotBounds.Height / modelHeight) * fitFactor;

            double modelCenterX = (minimumX + maximumX) / 2.0;
            double modelCenterY = (minimumY + maximumY) / 2.0;
            double screenCenterX = plotBounds.Left + plotBounds.Width / 2.0;
            double screenCenterY = plotBounds.Top + plotBounds.Height / 2.0;

            return new OrbitTransform(
                scale,
                screenCenterX - modelCenterX * scale,
                screenCenterY + modelCenterY * scale);
        }

        private static PointF[] TransformPoints(
            IList<PointD> points,
            OrbitTransform transform)
        {
            PointF[] result = new PointF[points.Count];

            for (int index = 0; index < points.Count; index++)
            {
                result[index] = transform.Transform(points[index]);
            }

            return result;
        }

        private static void DrawReferenceAxes(
            MissionRenderContext context,
            Rectangle plotBounds,
            PointF bodyPoint)
        {
            using (Pen axisPen = new Pen(
                Color.FromArgb(48, context.DimPhosphorColor),
                1.0f))
            {
                axisPen.DashStyle = DashStyle.Dot;
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
            if (points == null || points.Length < 2)
            {
                return;
            }

            using (Pen glowPen = new Pen(
                Color.FromArgb(34, context.PhosphorColor),
                5.0f))
            using (Pen orbitPen = new Pen(
                Color.FromArgb(205, context.PhosphorColor),
                1.5f))
            {
                context.Graphics.DrawLines(glowPen, points);
                context.Graphics.DrawLines(orbitPen, points);
            }
        }

        private static void DrawCentralBody(
            MissionRenderContext context,
            PointF center,
            float radius,
            string bodyName)
        {
            RectangleF bodyBounds = new RectangleF(
                center.X - radius,
                center.Y - radius,
                radius * 2.0f,
                radius * 2.0f);

            using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                bodyBounds,
                Color.FromArgb(190, context.PhosphorColor),
                Color.FromArgb(55, context.DimPhosphorColor),
                LinearGradientMode.ForwardDiagonal))
            using (Pen bodyPen = new Pen(context.PhosphorColor, 2.0f))
            using (SolidBrush labelBrush = new SolidBrush(context.DimPhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
                context.Graphics.FillEllipse(bodyBrush, bodyBounds);
                context.Graphics.DrawEllipse(bodyPen, bodyBounds);

                RectangleF labelBounds = new RectangleF(
                    center.X - 100.0f,
                    bodyBounds.Bottom + 5.0f,
                    200.0f,
                    context.SmallFont.Height + 6.0f);

                context.Graphics.DrawString(
                    FormatBodyName(bodyName),
                    context.SmallFont,
                    labelBrush,
                    labelBounds,
                    format);
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
            RectangleF markerBounds = new RectangleF(
                point.X - markerRadius,
                point.Y - markerRadius,
                markerRadius * 2.0f,
                markerRadius * 2.0f);

            string text = label + "  " + FormatDistance(altitude);
            const float labelWidth = 170.0f;

            float labelX = apoapsis
                ? point.X - labelWidth - 10.0f
                : point.X + 10.0f;

            float labelY = apoapsis
                ? point.Y - context.SmallFont.Height - 10.0f
                : point.Y + 9.0f;

            labelX = ClampFloat(
                labelX,
                plotBounds.Left,
                plotBounds.Right - labelWidth);

            labelY = ClampFloat(
                labelY,
                plotBounds.Top,
                plotBounds.Bottom - context.SmallFont.Height - 4.0f);

            using (SolidBrush markerBrush = new SolidBrush(context.PhosphorColor))
            using (SolidBrush textBrush = new SolidBrush(context.DimPhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = apoapsis
                    ? StringAlignment.Far
                    : StringAlignment.Near;

                context.Graphics.FillEllipse(markerBrush, markerBounds);
                context.Graphics.DrawString(
                    text,
                    context.SmallFont,
                    textBrush,
                    new RectangleF(
                        labelX,
                        labelY,
                        labelWidth,
                        context.SmallFont.Height + 6.0f),
                    format);
            }
        }

        private static void DrawVesselMarker(
            MissionRenderContext context,
            PointF vesselPoint,
            PointF tangent)
        {
            PointF direction = Normalize(tangent);
            const float markerLength = 15.0f;
            const float markerHalfWidth = 6.0f;

            PointF perpendicular = new PointF(-direction.Y, direction.X);
            PointF nose = new PointF(
                vesselPoint.X + direction.X * markerLength,
                vesselPoint.Y + direction.Y * markerLength);
            PointF left = new PointF(
                vesselPoint.X - direction.X * 6.0f + perpendicular.X * markerHalfWidth,
                vesselPoint.Y - direction.Y * 6.0f + perpendicular.Y * markerHalfWidth);
            PointF right = new PointF(
                vesselPoint.X - direction.X * 6.0f - perpendicular.X * markerHalfWidth,
                vesselPoint.Y - direction.Y * 6.0f - perpendicular.Y * markerHalfWidth);

            using (GraphicsPath path = new GraphicsPath())
            using (SolidBrush glowBrush = new SolidBrush(
                Color.FromArgb(65, context.PhosphorColor)))
            using (SolidBrush markerBrush = new SolidBrush(context.PhosphorColor))
            {
                path.AddPolygon(new[] { nose, left, right });
                context.Graphics.FillEllipse(
                    glowBrush,
                    vesselPoint.X - 14.0f,
                    vesselPoint.Y - 14.0f,
                    28.0f,
                    28.0f);
                context.Graphics.FillPath(markerBrush, path);
            }
        }

        private static void DrawOrbitLegend(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            const int legendWidth = 270;
            const int legendHeight = 88;
            const int legendPadding = 10;
            const int labelWidth = 105;
            const int rowHeight = 24;

            Rectangle legendBounds = new Rectangle(
                plotBounds.Left + 10,
                plotBounds.Bottom - legendHeight - 10,
                Math.Min(legendWidth, plotBounds.Width - 20),
                legendHeight);

            using (SolidBrush backgroundBrush = new SolidBrush(
                Color.FromArgb(175, 2, 13, 18)))
            using (Pen borderPen = new Pen(
                Color.FromArgb(80, context.DimPhosphorColor),
                1.0f))
            {
                context.Graphics.FillRectangle(backgroundBrush, legendBounds);
                context.Graphics.DrawRectangle(borderPen, legendBounds);
            }

            int labelX = legendBounds.Left + legendPadding;
            int valueX = labelX + labelWidth;
            int rowY = legendBounds.Top + 7;

            DrawLegendField(
                context,
                "BODY",
                FormatBodyName(telemetry.BodyName),
                labelX,
                valueX,
                rowY);

            rowY += rowHeight;
            DrawLegendField(
                context,
                "VESSEL",
                FormatVesselName(telemetry.VesselName),
                labelX,
                valueX,
                rowY);

            rowY += rowHeight;
            DrawLegendField(
                context,
                "TYPE",
                GetOrbitType(telemetry),
                labelX,
                valueX,
                rowY);
        }

        private static void DrawLegendField(
            MissionRenderContext context,
            string label,
            string value,
            int labelX,
            int valueX,
            int y)
        {
            using (SolidBrush labelBrush = new SolidBrush(context.DimPhosphorColor))
            using (SolidBrush valueBrush = new SolidBrush(context.PhosphorColor))
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

        private static void DrawGroundedState(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            PointF bodyPoint = new PointF(
                plotBounds.Left + plotBounds.Width / 2.0f,
                plotBounds.Top + plotBounds.Height / 2.0f - 22.0f);

            DrawCentralBody(context, bodyPoint, 18.0f, telemetry.BodyName);
            DrawCenteredStatus(
                context,
                plotBounds,
                "VESSEL GROUNDED",
                "AWAITING ASCENT");
        }

        private static void DrawUnavailableState(
            MissionRenderContext context,
            Rectangle plotBounds,
            MissionTelemetry telemetry)
        {
            PointF bodyPoint = new PointF(
                plotBounds.Left + plotBounds.Width / 2.0f,
                plotBounds.Top + plotBounds.Height / 2.0f - 22.0f);

            DrawCentralBody(context, bodyPoint, 18.0f, telemetry.BodyName);

            string detail = telemetry.Eccentricity >= 1.0
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
            RectangleF titleBounds = new RectangleF(
                bounds.Left,
                bounds.Top + bounds.Height * 0.68f,
                bounds.Width,
                context.SmallFont.Height + 8.0f);

            RectangleF detailBounds = new RectangleF(
                bounds.Left,
                titleBounds.Bottom + 2.0f,
                bounds.Width,
                context.SmallFont.Height + 8.0f);

            using (SolidBrush titleBrush = new SolidBrush(context.PhosphorColor))
            using (SolidBrush detailBrush = new SolidBrush(context.DimPhosphorColor))
            using (StringFormat format = new StringFormat())
            {
                format.Alignment = StringAlignment.Center;
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

        private static bool IsGrounded(MissionTelemetry telemetry)
        {
            return telemetry.Altitude < 500.0 &&
                Math.Abs(telemetry.SurfaceSpeed) < 1.0 &&
                Math.Abs(telemetry.VerticalSpeed) < 1.0;
        }

        private static bool HasSupportedOrbit(MissionTelemetry telemetry)
        {
            return IsFinite(telemetry.Eccentricity) &&
                IsFinite(telemetry.SemiMajorAxis) &&
                IsFinite(telemetry.TrueAnomalyDegrees) &&
                IsFinite(telemetry.ArgumentOfPeriapsisDegrees) &&
                telemetry.Eccentricity >= 0.0 &&
                telemetry.Eccentricity < 1.0 &&
                telemetry.SemiMajorAxis > 0.0;
        }

        private static string GetOrbitType(MissionTelemetry telemetry)
        {
            if (telemetry.Periapsis < 0.0)
            {
                return "SUBORBITAL";
            }

            if (telemetry.Eccentricity <= 0.03)
            {
                return "CIRCULAR";
            }

            return "ELLIPTICAL";
        }

        private static string FormatDistance(double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            double absolute = Math.Abs(value);

            if (absolute >= 1000000.0)
            {
                return (value / 1000000.0).ToString("0.00") + " MM";
            }

            if (absolute >= 1000.0)
            {
                return (value / 1000.0).ToString("0.0") + " KM";
            }

            return value.ToString("0") + " M";
        }

        private static string FormatBodyName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "BODY";
            }

            return value.Trim().ToUpperInvariant();
        }

        private static string FormatVesselName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "VESSEL";
            }

            string result = value.Trim().ToUpperInvariant();

            if (result.Length > 18)
            {
                result = result.Substring(0, 18);
            }

            return result;
        }

        private static PointF Normalize(PointF value)
        {
            double length = Math.Sqrt(value.X * value.X + value.Y * value.Y);

            if (length <= 0.0001)
            {
                return new PointF(1.0f, 0.0f);
            }

            return new PointF(
                (float)(value.X / length),
                (float)(value.Y / length));
        }

        private static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double NormalizeDegrees(double value)
        {
            double normalized = value % 360.0;

            if (normalized < 0.0)
            {
                normalized += 360.0;
            }

            return normalized;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static float ClampFloat(
            float value,
            float minimum,
            float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private struct PointD
        {
            public PointD(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }
            public double Y { get; }
        }

        private struct OrbitTransform
        {
            public OrbitTransform(
                double scale,
                double offsetX,
                double offsetY)
            {
                Scale = scale;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public double Scale { get; }
            public double OffsetX { get; }
            public double OffsetY { get; }

            public PointF Transform(PointD point)
            {
                return new PointF(
                    (float)(OffsetX + point.X * Scale),
                    (float)(OffsetY - point.Y * Scale));
            }
        }
    }
}