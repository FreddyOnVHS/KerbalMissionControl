using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;

namespace KMC.MissionControl.Pages
{
    public sealed class AscentPage : IMissionPage
    {
        private const double DefaultTargetApoapsisMeters =
            80000.0;

        private const double MinimumSampleIntervalSeconds =
            0.20;

        private const int MaximumSamples =
            900;

        private readonly List<AscentSample> _samples =
            new List<AscentSample>();

        private string _trackedVesselName =
            string.Empty;

        private double _previousMissionTime =
            double.NaN;

        private double _downrangeMeters;

        public string Name
        {
            get { return "ASCENT GUIDANCE"; }
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

            UpdateHistory(
                telemetry);

            DrawHeader(
                context);

            Rectangle graphBounds =
                context.GetRelativeRectangle(
                    0.045f,
                    0.135f,
                    0.610f,
                    0.675f);

            Rectangle statusBounds =
                context.GetRelativeRectangle(
                    0.675f,
                    0.135f,
                    0.285f,
                    0.675f);

            Rectangle footerBounds =
                context.GetRelativeRectangle(
                    0.045f,
                    0.835f,
                    0.915f,
                    0.115f);

            DrawAscentGraph(
                context,
                graphBounds,
                telemetry);

            DrawGuidancePanel(
                context,
                statusBounds,
                telemetry);

            DrawFooter(
                context,
                footerBounds,
                telemetry);
        }

        private void UpdateHistory(
            MissionTelemetry telemetry)
        {
            string vesselName =
                telemetry.VesselName ??
                string.Empty;

            bool vesselChanged =
                !string.Equals(
                    vesselName,
                    _trackedVesselName,
                    StringComparison.Ordinal);

            bool timeReset =
                IsFinite(_previousMissionTime) &&
                telemetry.MissionTime + 0.5 <
                _previousMissionTime;

            if (vesselChanged ||
                timeReset)
            {
                ResetHistory(
                    vesselName);
            }

            if (!IsFinite(
                    telemetry.MissionTime))
            {
                return;
            }

            if (!IsFinite(
                    _previousMissionTime))
            {
                _previousMissionTime =
                    telemetry.MissionTime;
            }

            double deltaTime =
                telemetry.MissionTime -
                _previousMissionTime;

            if (deltaTime < 0.0 ||
                deltaTime > 10.0)
            {
                deltaTime = 0.0;
            }

            if (deltaTime > 0.0 &&
                IsFinite(
                    telemetry.HorizontalSpeed))
            {
                _downrangeMeters +=
                    Math.Max(
                        0.0,
                        telemetry.HorizontalSpeed) *
                    deltaTime;
            }

            bool shouldSample =
                _samples.Count == 0 ||
                telemetry.MissionTime -
                _samples[_samples.Count - 1]
                    .MissionTime >=
                MinimumSampleIntervalSeconds;

            if (shouldSample)
            {
                _samples.Add(
                    new AscentSample
                    {
                        MissionTime =
                            telemetry.MissionTime,

                        DownrangeMeters =
                            Math.Max(
                                0.0,
                                _downrangeMeters),

                        AltitudeMeters =
                            Math.Max(
                                0.0,
                                telemetry.Altitude),

                        ApoapsisMeters =
                            telemetry.Apoapsis,

                        PitchDegrees =
                            telemetry.Pitch,

                        DynamicPressureKpa =
                            telemetry.DynamicPressureKpa
                    });

                while (_samples.Count >
                       MaximumSamples)
                {
                    _samples.RemoveAt(0);
                }
            }

            _previousMissionTime =
                telemetry.MissionTime;
        }

        private void ResetHistory(
            string vesselName)
        {
            _samples.Clear();

            _trackedVesselName =
                vesselName ??
                string.Empty;

            _previousMissionTime =
                double.NaN;

            _downrangeMeters = 0.0;
        }

        private static void DrawHeader(
            MissionRenderContext context)
        {
            Graphics graphics =
                context.Graphics;

            Rectangle titleBounds =
                context.GetRelativeRectangle(
                    0.045f,
                    0.035f,
                    0.915f,
                    0.070f);

            using (Pen linePen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            {
                graphics.DrawLine(
                    linePen,
                    titleBounds.Left,
                    titleBounds.Bottom,
                    titleBounds.Right,
                    titleBounds.Bottom);
            }

            graphics.DrawString(
                "ASCENT GUIDANCE",
                context.LargeFont,
                new SolidBrush(
                    context.PhosphorColor),
                titleBounds.Left,
                titleBounds.Top);

            string channel =
                "CH 02";

            SizeF channelSize =
                graphics.MeasureString(
                    channel,
                    context.LargeFont);

            using (Brush brush =
                new SolidBrush(
                    context.PhosphorColor))
            {
                graphics.DrawString(
                    channel,
                    context.LargeFont,
                    brush,
                    titleBounds.Right -
                    channelSize.Width,
                    titleBounds.Top);
            }
        }

        private void DrawAscentGraph(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            Graphics graphics =
                context.Graphics;

            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        70,
                        context.DimPhosphorColor),
                    1.0f))
            using (Pen targetPen =
                new Pen(
                    context.DimPhosphorColor,
                    2.0f))
            using (Pen actualPen =
                new Pen(
                    Color.FromArgb(
                        230,
                        255,
                        90,
                        80),
                    2.2f))
            {
                targetPen.DashStyle =
                    DashStyle.Dash;

                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                Rectangle plot =
                    Rectangle.Inflate(
                        bounds,
                        -44,
                        -34);

                plot.Y += 4;
                plot.Height -= 10;

                DrawGrid(
                    graphics,
                    plot,
                    gridPen);

                DrawAxisLabels(
                    context,
                    plot);

                double maxDownrange =
                    CalculateGraphDownrangeLimit(
                        telemetry);

                double maxAltitude =
                    Math.Max(
                        DefaultTargetApoapsisMeters *
                        1.15,
                        GetMaximumActualAltitude() *
                        1.10);

                DrawTargetCurve(
                    graphics,
                    plot,
                    targetPen,
                    maxDownrange,
                    maxAltitude,
                    telemetry);

                DrawActualCurve(
                    graphics,
                    plot,
                    actualPen,
                    maxDownrange,
                    maxAltitude);

                DrawCurrentMarker(
                    context,
                    plot,
                    maxDownrange,
                    maxAltitude,
                    telemetry);

                using (Brush labelBrush =
                    new SolidBrush(
                        context.PhosphorColor))
                {
                    graphics.DrawString(
                        "ALTITUDE VS DOWNRANGE",
                        context.SmallFont,
                        labelBrush,
                        bounds.Left + 10,
                        bounds.Top + 8);
                }

                DrawLegend(
                    context,
                    bounds);
            }
        }

        private static void DrawGrid(
            Graphics graphics,
            Rectangle plot,
            Pen gridPen)
        {
            const int verticalDivisions = 8;
            const int horizontalDivisions = 6;

            for (int index = 0;
                 index <= verticalDivisions;
                 index++)
            {
                int x =
                    plot.Left +
                    plot.Width *
                    index /
                    verticalDivisions;

                graphics.DrawLine(
                    gridPen,
                    x,
                    plot.Top,
                    x,
                    plot.Bottom);
            }

            for (int index = 0;
                 index <= horizontalDivisions;
                 index++)
            {
                int y =
                    plot.Top +
                    plot.Height *
                    index /
                    horizontalDivisions;

                graphics.DrawLine(
                    gridPen,
                    plot.Left,
                    y,
                    plot.Right,
                    y);
            }
        }

        private static void DrawAxisLabels(
            MissionRenderContext context,
            Rectangle plot)
        {
            Graphics graphics =
                context.Graphics;

            using (Brush brush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                graphics.DrawString(
                    "ALT",
                    context.SmallFont,
                    brush,
                    plot.Left - 34,
                    plot.Top - 4);

                string rangeLabel =
                    "DOWNRANGE";

                SizeF size =
                    graphics.MeasureString(
                        rangeLabel,
                        context.SmallFont);

                graphics.DrawString(
                    rangeLabel,
                    context.SmallFont,
                    brush,
                    plot.Right -
                    size.Width,
                    plot.Bottom + 7);
            }
        }

        private void DrawTargetCurve(
            Graphics graphics,
            Rectangle plot,
            Pen pen,
            double maxDownrange,
            double maxAltitude,
            MissionTelemetry telemetry)
        {
            const int pointCount = 120;

            PointF[] points =
                new PointF[pointCount];

            for (int index = 0;
                 index < pointCount;
                 index++)
            {
                double fraction =
                    index /
                    (double)(pointCount - 1);

                double downrange =
                    maxDownrange *
                    fraction;

                double altitude =
                    CalculateTargetAltitude(
                        downrange,
                        telemetry);

                points[index] =
                    MapPoint(
                        plot,
                        downrange,
                        altitude,
                        maxDownrange,
                        maxAltitude);
            }

            graphics.DrawLines(
                pen,
                points);
        }

        private void DrawActualCurve(
            Graphics graphics,
            Rectangle plot,
            Pen pen,
            double maxDownrange,
            double maxAltitude)
        {
            if (_samples.Count < 2)
            {
                return;
            }

            PointF[] points =
                new PointF[_samples.Count];

            for (int index = 0;
                 index < _samples.Count;
                 index++)
            {
                AscentSample sample =
                    _samples[index];

                points[index] =
                    MapPoint(
                        plot,
                        sample.DownrangeMeters,
                        sample.AltitudeMeters,
                        maxDownrange,
                        maxAltitude);
            }

            graphics.DrawLines(
                pen,
                points);
        }

        private void DrawCurrentMarker(
            MissionRenderContext context,
            Rectangle plot,
            double maxDownrange,
            double maxAltitude,
            MissionTelemetry telemetry)
        {
            if (_samples.Count == 0)
            {
                return;
            }

            AscentSample sample =
                _samples[_samples.Count - 1];

            PointF point =
                MapPoint(
                    plot,
                    sample.DownrangeMeters,
                    sample.AltitudeMeters,
                    maxDownrange,
                    maxAltitude);

            RectangleF marker =
                new RectangleF(
                    point.X - 4.0f,
                    point.Y - 4.0f,
                    8.0f,
                    8.0f);

            using (Brush brush =
                new SolidBrush(
                    Color.FromArgb(
                        240,
                        255,
                        110,
                        90)))
            {
                context.Graphics.FillEllipse(
                    brush,
                    marker);
            }
        }

        private static void DrawLegend(
            MissionRenderContext context,
            Rectangle bounds)
        {
            Graphics graphics =
                context.Graphics;

            int y =
                bounds.Bottom - 22;

            using (Pen targetPen =
                new Pen(
                    context.DimPhosphorColor,
                    2.0f))
            using (Pen actualPen =
                new Pen(
                    Color.FromArgb(
                        230,
                        255,
                        90,
                        80),
                    2.0f))
            using (Brush textBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                targetPen.DashStyle =
                    DashStyle.Dash;

                graphics.DrawLine(
                    targetPen,
                    bounds.Left + 12,
                    y,
                    bounds.Left + 42,
                    y);

                graphics.DrawString(
                    "TARGET",
                    context.SmallFont,
                    textBrush,
                    bounds.Left + 48,
                    y - 8);

                graphics.DrawLine(
                    actualPen,
                    bounds.Left + 128,
                    y,
                    bounds.Left + 158,
                    y);

                graphics.DrawString(
                    "ACTUAL",
                    context.SmallFont,
                    textBrush,
                    bounds.Left + 164,
                    y - 8);
            }
        }

        private void DrawGuidancePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            Graphics graphics =
                context.Graphics;

            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Brush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Brush valueBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            {
                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                graphics.DrawString(
                    "FLIGHT DIRECTOR",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 10,
                    bounds.Top + 10);

                int y =
                    bounds.Top + 42;

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "TARGET AP",
                    FormatDistance(
                        DefaultTargetApoapsisMeters));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "DOWNRANGE",
                    FormatDistance(
                        _downrangeMeters));

                double targetAltitude =
                    CalculateTargetAltitude(
                        _downrangeMeters,
                        telemetry);

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "TARGET ALT",
                    FormatDistance(
                        targetAltitude));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "ACTUAL ALT",
                    FormatDistance(
                        telemetry.Altitude));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "ALT ERROR",
                    FormatSignedDistance(
                        telemetry.Altitude -
                        targetAltitude));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "TARGET PITCH",
                    FormatAngle(
                        CalculateTargetPitch(
                            _downrangeMeters,
                            telemetry)));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "ACTUAL PITCH",
                    FormatAngle(
                        telemetry.Pitch));

                DrawPanelRow(
                    context,
                    bounds,
                    ref y,
                    "DYN Q",
                    FormatPressure(
                        telemetry.DynamicPressureKpa));

                y += 8;

                string guidance =
                    DetermineGuidance(
                        telemetry,
                        targetAltitude);

                graphics.DrawString(
                    "GUIDANCE",
                    context.SmallFont,
                    titleBrush,
                    bounds.Left + 10,
                    y);

                Rectangle guidanceBounds =
                    new Rectangle(
                    bounds.Left + 10,
                     y + 18,
                    bounds.Width - 20,
                    Math.Max(
                        20,
                        bounds.Bottom -
                        y -
                        24));

                using (StringFormat format =
                    new StringFormat())
                {
                    format.Trimming =
                        StringTrimming.EllipsisWord;

                    graphics.DrawString(
                        guidance,
                        context.SmallFont,
                        valueBrush,
                        guidanceBounds,
                        format);
                }
            }
        }

        private static void DrawPanelRow(
    MissionRenderContext context,
    Rectangle bounds,
    ref int y,
    string label,
    string value)
        {
            Graphics graphics =
                context.Graphics;

            int left =
                bounds.Left + 10;

            int availableWidth =
                Math.Max(
                    20,
                    bounds.Width - 20);

            Rectangle labelBounds =
                new Rectangle(
                    left,
                    y,
                    availableWidth,
                    16);

            Rectangle valueBounds =
                new Rectangle(
                    left,
                    y + 14,
                    availableWidth,
                    18);

            using (Brush labelBrush =
                new SolidBrush(
                    context.DimPhosphorColor))
            using (Brush valueBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat valueFormat =
                new StringFormat())
            {
                valueFormat.Alignment =
                    StringAlignment.Far;

                valueFormat.LineAlignment =
                    StringAlignment.Near;

                valueFormat.Trimming =
                    StringTrimming.EllipsisCharacter;

                valueFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds);

                graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    valueFormat);
            }

            y += 34;
        }

        private static void DrawFooter(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
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

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    0,
                    "MET",
                    FormatMissionTime(
                        telemetry.MissionTime),
                    labelBrush,
                    valueBrush);

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    1,
                    "STAGE",
                    telemetry.CurrentStage
                        .ToString("00"),
                    labelBrush,
                    valueBrush);

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    2,
                    "VERT VEL",
                    FormatSignedSpeed(
                        telemetry.VerticalSpeed),
                    labelBrush,
                    valueBrush);

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    3,
                    "HORIZ VEL",
                    FormatSpeed(
                        telemetry.HorizontalSpeed),
                    labelBrush,
                    valueBrush);

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    4,
                    "TWR",
                    FormatRatio(
                        telemetry.ThrustToWeightRatio),
                    labelBrush,
                    valueBrush);

                DrawFooterValue(
                    graphics,
                    context,
                    bounds,
                    5,
                    "APOAPSIS",
                    FormatDistance(
                        telemetry.Apoapsis),
                    labelBrush,
                    valueBrush);
            }
        }

        private static void DrawFooterValue(
            Graphics graphics,
            MissionRenderContext context,
            Rectangle bounds,
            int index,
            string label,
            string value,
            Brush labelBrush,
            Brush valueBrush)
        {
            int cellWidth =
                bounds.Width / 6;

            int x =
                bounds.Left +
                cellWidth *
                index +
                10;

            graphics.DrawString(
                label,
                context.SmallFont,
                labelBrush,
                x,
                bounds.Top + 10);

            graphics.DrawString(
                value,
                context.SmallFont,
                valueBrush,
                x,
                bounds.Top + 36);
        }

        private double CalculateGraphDownrangeLimit(
            MissionTelemetry telemetry)
        {
            double current =
                Math.Max(
                    1.0,
                    _downrangeMeters);

            double profileScale =
                CalculateProfileScale(
                    telemetry);

            return Math.Max(
                120000.0,
                Math.Max(
                    current * 1.20,
                    profileScale * 4.5));
        }

        private static double CalculateTargetAltitude(
            double downrangeMeters,
            MissionTelemetry telemetry)
        {
            double profileScale =
                CalculateProfileScale(
                    telemetry);

            double normalized =
                Math.Max(
                    0.0,
                    downrangeMeters) /
                profileScale;

            double altitude =
                DefaultTargetApoapsisMeters *
                (1.0 -
                 Math.Exp(
                     -normalized));

            return Math.Min(
                DefaultTargetApoapsisMeters,
                Math.Max(
                    0.0,
                    altitude));
        }

        private static double CalculateTargetPitch(
            double downrangeMeters,
            MissionTelemetry telemetry)
        {
            double scale =
                CalculateProfileScale(
                    telemetry);

            double slope =
                DefaultTargetApoapsisMeters /
                scale *
                Math.Exp(
                    -Math.Max(
                        0.0,
                        downrangeMeters) /
                    scale);

            double flightPathAngle =
                Math.Atan(
                    slope) *
                180.0 /
                Math.PI;

            /*
             * KSP pitch is measured from the horizon:
             * 90 degrees is vertical and 0 degrees is horizontal.
             */
            return Math.Max(
                0.0,
                Math.Min(
                    90.0,
                    flightPathAngle));
        }

        private static double CalculateProfileScale(
            MissionTelemetry telemetry)
        {
            double twr =
                IsFinite(
                    telemetry.ThrustToWeightRatio)
                    ? telemetry
                        .ThrustToWeightRatio
                    : 1.5;

            twr =
                Math.Max(
                    0.8,
                    Math.Min(
                        3.0,
                        twr));

            double scale =
                52000.0 /
                Math.Sqrt(
                    twr);

            return Math.Max(
                26000.0,
                Math.Min(
                    72000.0,
                    scale));
        }

        private static string DetermineGuidance(
            MissionTelemetry telemetry,
            double targetAltitude)
        {
            if (telemetry.ThrustToWeightRatio <
                1.0 &&
                telemetry.Altitude <
                1000.0)
            {
                return
                    "HOLD: INSUFFICIENT LAUNCH TWR";
            }

            if (telemetry.MissionTime <
                1.0)
            {
                return
                    "AWAITING ASCENT";
            }

            if (telemetry.DynamicPressureKpa >
                35.0)
            {
                return
                    "HIGH DYNAMIC PRESSURE - LIMIT PITCH RATE";
            }

            double altitudeError =
                telemetry.Altitude -
                targetAltitude;

            if (altitudeError >
                6000.0)
            {
                return
                    "PROFILE HIGH - PITCH DOWN GRADUALLY";
            }

            if (altitudeError <
                -6000.0)
            {
                return
                    "PROFILE LOW - HOLD VERTICAL COMPONENT";
            }

            double targetPitch =
                CalculateTargetPitch(
                    Math.Max(
                        0.0,
                        telemetry.HorizontalSpeed *
                        telemetry.MissionTime *
                        0.55),
                    telemetry);

            double pitchError =
                telemetry.Pitch -
                targetPitch;

            if (pitchError >
                12.0)
            {
                return
                    "PITCH HIGH - INCREASE GRAVITY TURN";
            }

            if (pitchError <
                -12.0)
            {
                return
                    "PITCH LOW - REDUCE TURN RATE";
            }

            if (telemetry.Apoapsis >=
                DefaultTargetApoapsisMeters)
            {
                return
                    "TARGET APOAPSIS ACHIEVED - PREPARE MECO";
            }

            return
                "ASCENT PROFILE NOMINAL";
        }

        private double GetMaximumActualAltitude()
        {
            double maximum = 0.0;

            foreach (AscentSample sample in
                _samples)
            {
                maximum =
                    Math.Max(
                        maximum,
                        sample.AltitudeMeters);
            }

            return maximum;
        }

        private static PointF MapPoint(
            Rectangle plot,
            double downrange,
            double altitude,
            double maxDownrange,
            double maxAltitude)
        {
            double xFraction =
                maxDownrange > 0.0
                    ? downrange /
                      maxDownrange
                    : 0.0;

            double yFraction =
                maxAltitude > 0.0
                    ? altitude /
                      maxAltitude
                    : 0.0;

            xFraction =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        xFraction));

            yFraction =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        yFraction));

            return new PointF(
                plot.Left +
                (float)(plot.Width *
                        xFraction),
                plot.Bottom -
                (float)(plot.Height *
                        yFraction));
        }

        private static string FormatDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            double absoluteValue =
                Math.Abs(meters);

            if (absoluteValue >= 1000000.0)
            {
                return
                    (meters / 1000000.0)
                    .ToString("0.00") +
                    " MM";
            }

            if (absoluteValue >= 1000.0)
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
                    .ToString(
                        "+0.0;-0.0;0.0") +
                    " KM";
            }

            return
                meters.ToString(
                    "+0;-0;0") +
                " M";
        }

        private static string FormatSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond
                .ToString("0.0") +
                " M/S";
        }

        private static string FormatSignedSpeed(
            double metersPerSecond)
        {
            if (!IsFinite(metersPerSecond))
            {
                return "---";
            }

            return
                metersPerSecond.ToString(
                    "+0.0;-0.0;0.0") +
                " M/S";
        }

        private static string FormatPressure(
            double kilopascals)
        {
            if (!IsFinite(kilopascals))
            {
                return "---";
            }

            return
                Math.Max(
                    0.0,
                    kilopascals)
                .ToString("0.00") +
                " KPA";
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

        private static string FormatRatio(
            double value)
        {
            if (!IsFinite(value))
            {
                return "---";
            }

            return
                Math.Max(
                    0.0,
                    value)
                .ToString("0.00");
        }

        private static string FormatMissionTime(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                totalSeconds = 0.0;
            }

            int hours =
                (int)(totalSeconds / 3600.0);

            int minutes =
                (int)(totalSeconds % 3600.0) /
                60;

            int seconds =
                (int)(totalSeconds % 60.0);

            return string.Format(
                "{0:000}:{1:00}:{2:00}",
                hours,
                minutes,
                seconds);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private sealed class AscentSample
        {
            public double MissionTime { get; set; }

            public double DownrangeMeters { get; set; }

            public double AltitudeMeters { get; set; }

            public double ApoapsisMeters { get; set; }

            public double PitchDegrees { get; set; }

            public double DynamicPressureKpa { get; set; }
        }
    }
}