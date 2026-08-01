using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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

        private double _planningTwr =
            double.NaN;

        private double _planningProfileScale =
            double.NaN;

        private int _initialStage =
            -1;

        private static readonly object DebugLogSync =
            new object();

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

            CaptureLaunchPlan(
                telemetry);

            WriteAscentDebugSample(
                telemetry);

            DrawHeader(
                context);

            /*
             * Large-format ascent layout:
             *
             *   Main ascent graph       Orbit inset
             *                           Flight Director
             *
             *   Full-width telemetry strip
             */

            Rectangle graphBounds =
                context.GetRelativeRectangle(
                    0.015f,
                    0.090f,
                    0.700f,
                    0.755f);

            Rectangle orbitInsetBounds =
                context.GetRelativeRectangle(
                    0.730f,
                    0.090f,
                    0.255f,
                    0.270f);

            Rectangle statusBounds =
                context.GetRelativeRectangle(
                    0.730f,
                    0.375f,
                    0.255f,
                    0.470f);

            Rectangle footerBounds =
                context.GetRelativeRectangle(
                    0.015f,
                    0.865f,
                    0.970f,
                    0.105f);

            DrawAscentGraph(
                context,
                graphBounds,
                telemetry);

            DrawOrbitInset(
                context,
                orbitInsetBounds,
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

            _planningTwr =
                double.NaN;

            _planningProfileScale =
                double.NaN;

            _initialStage =
                -1;
        }

        private void CaptureLaunchPlan(
            MissionTelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            if (IsFinite(
                    _planningProfileScale))
            {
                return;
            }

            double twr =
                telemetry.ThrustToWeightRatio;

            /*
             * Do not lock the ascent plan while telemetry is still
             * reporting zero thrust on the launchpad.
             */
            if (!IsFinite(twr) ||
                twr < 1.0)
            {
                return;
            }

            _planningTwr =
                Math.Max(
                    0.8,
                    Math.Min(
                        3.0,
                        twr));

            _planningProfileScale =
                CalculateProfileScaleFromTwr(
                    _planningTwr);

            _initialStage =
                telemetry.CurrentStage;
        }

        private void WriteAscentDebugSample(
            MissionTelemetry telemetry)
        {
            if (telemetry == null ||
                _samples.Count == 0)
            {
                return;
            }

            AscentSample sample =
                _samples[_samples.Count - 1];

            /*
             * Only write once per stored ascent sample. The sample cadence
             * is already limited by MinimumSampleIntervalSeconds.
             */
            if (sample.DebugWritten)
            {
                return;
            }

            sample.DebugWritten = true;

            double profileScale =
                GetPlanningProfileScale(
                    telemetry);

            double targetAltitude =
                CalculateTargetAltitude(
                    sample.DownrangeMeters,
                    telemetry);

            double targetPitch =
                CalculateTargetPitch(
                    sample.DownrangeMeters,
                    telemetry);

            try
            {
                string directory =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder
                                .LocalApplicationData),
                        "KMC");

                Directory.CreateDirectory(
                    directory);

                string path =
                    Path.Combine(
                        directory,
                        "ascent-debug.csv");

                lock (DebugLogSync)
                {
                    bool writeHeader =
                        !File.Exists(path);

                    using (StreamWriter writer =
                        new StreamWriter(
                            path,
                            true))
                    {
                        if (writeHeader)
                        {
                            writer.WriteLine(
                                "MET,Stage,InitialStage,AltitudeM," +
                                "DownrangeM,LiveTWR,PlanningTWR," +
                                "ProfileScaleM,TargetAltitudeM," +
                                "TargetPitchDeg,ActualPitchDeg," +
                                "ApoapsisM");
                        }

                        writer.WriteLine(
                            string.Join(
                                ",",
                                telemetry.MissionTime
                                    .ToString("0.000"),
                                telemetry.CurrentStage,
                                _initialStage,
                                telemetry.Altitude
                                    .ToString("0.000"),
                                sample.DownrangeMeters
                                    .ToString("0.000"),
                                telemetry.ThrustToWeightRatio
                                    .ToString("0.000"),
                                IsFinite(_planningTwr)
                                    ? _planningTwr
                                        .ToString("0.000")
                                    : string.Empty,
                                profileScale
                                    .ToString("0.000"),
                                targetAltitude
                                    .ToString("0.000"),
                                targetPitch
                                    .ToString("0.000"),
                                telemetry.Pitch
                                    .ToString("0.000"),
                                telemetry.Apoapsis
                                    .ToString("0.000")));
                    }
                }
            }
            catch
            {
                /*
                 * Diagnostics must never interrupt the mission display.
                 */
            }
        }

        private static void DrawHeader(
            MissionRenderContext context)
        {
            Graphics graphics =
                context.Graphics;

            Rectangle titleBounds =
                context.GetRelativeRectangle(
                    0.015f,
                    0.018f,
                    0.970f,
                    0.055f);

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
                        -48,
                        -38);

                plot.Y += 10;
                plot.Height -= 12;

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

        private static void DrawOrbitInset(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            Graphics graphics =
                context.Graphics;

            float compactSize =
                Math.Max(
                    6.0f,
                    context.SmallFont.Size *
                    0.74f);

            using (Font compactFont =
                new Font(
                    context.SmallFont.FontFamily,
                    compactSize,
                    FontStyle.Regular,
                    GraphicsUnit.Point))
            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen gridPen =
                new Pen(
                    Color.FromArgb(
                        60,
                        context.DimPhosphorColor),
                    1.0f))
            using (Pen orbitPen =
                new Pen(
                    context.DimPhosphorColor,
                    1.5f))
            using (Brush titleBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (Brush bodyBrush =
                new SolidBrush(
                    Color.FromArgb(
                        185,
                        context.DimPhosphorColor)))
            using (Brush vesselBrush =
                new SolidBrush(
                    context.PhosphorColor))
            using (StringFormat rightFormat =
                new StringFormat())
            {
                rightFormat.Alignment =
                    StringAlignment.Far;

                rightFormat.LineAlignment =
                    StringAlignment.Center;

                rightFormat.Trimming =
                    StringTrimming.EllipsisCharacter;

                rightFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                graphics.DrawRectangle(
                    borderPen,
                    bounds);

                int padding = 8;
                int titleHeight = 20;
                int dataHeight = 38;

                graphics.DrawString(
                    "ORBIT TREND",
                    compactFont,
                    titleBrush,
                    bounds.Left + padding,
                    bounds.Top + 5);

                Rectangle plot =
                    new Rectangle(
                        bounds.Left + padding,
                        bounds.Top + titleHeight + 5,
                        bounds.Width - padding * 2,
                        Math.Max(
                            30,
                            bounds.Height -
                            titleHeight -
                            dataHeight -
                            12));

                for (int index = 1;
                     index < 4;
                     index++)
                {
                    int x =
                        plot.Left +
                        plot.Width *
                        index /
                        4;

                    int y =
                        plot.Top +
                        plot.Height *
                        index /
                        4;

                    graphics.DrawLine(
                        gridPen,
                        x,
                        plot.Top,
                        x,
                        plot.Bottom);

                    graphics.DrawLine(
                        gridPen,
                        plot.Left,
                        y,
                        plot.Right,
                        y);
                }

                float centerX =
                    plot.Left +
                    plot.Width * 0.50f;

                float centerY =
                    plot.Top +
                    plot.Height * 0.50f;

                double eccentricity =
                    IsFinite(
                        telemetry.Eccentricity)
                        ? Math.Max(
                            0.0,
                            Math.Min(
                                0.92,
                                telemetry.Eccentricity))
                        : 0.0;

                float semiMajor =
                    plot.Width * 0.39f;

                float semiMinor =
                    Math.Min(
                        plot.Height * 0.39f,
                        semiMajor *
                        (float)Math.Sqrt(
                            Math.Max(
                                0.15,
                                1.0 -
                                eccentricity *
                                eccentricity)));

                RectangleF ellipse =
                    new RectangleF(
                        centerX - semiMajor,
                        centerY - semiMinor,
                        semiMajor * 2.0f,
                        semiMinor * 2.0f);

                graphics.DrawEllipse(
                    orbitPen,
                    ellipse);

                float bodyRadius =
                    Math.Max(
                        4.0f,
                        Math.Min(
                            plot.Width,
                            plot.Height) *
                        0.055f);

                graphics.FillEllipse(
                    bodyBrush,
                    centerX - bodyRadius,
                    centerY - bodyRadius,
                    bodyRadius * 2.0f,
                    bodyRadius * 2.0f);

                double anomalyRadians =
                    telemetry.TrueAnomalyDegrees *
                    Math.PI /
                    180.0;

                if (!IsFinite(
                        anomalyRadians))
                {
                    anomalyRadians = 0.0;
                }

                float vesselX =
                    centerX +
                    semiMajor *
                    (float)Math.Cos(
                        anomalyRadians);

                float vesselY =
                    centerY -
                    semiMinor *
                    (float)Math.Sin(
                        anomalyRadians);

                graphics.FillEllipse(
                    vesselBrush,
                    vesselX - 3.0f,
                    vesselY - 3.0f,
                    6.0f,
                    6.0f);

                int dataTop =
                    bounds.Bottom -
                    dataHeight -
                    3;

                Rectangle apLabelBounds =
                    new Rectangle(
                        bounds.Left + padding,
                        dataTop,
                        26,
                        17);

                Rectangle apValueBounds =
                    new Rectangle(
                        bounds.Left + padding + 28,
                        dataTop,
                        bounds.Width -
                        padding * 2 -
                        28,
                        17);

                Rectangle peLabelBounds =
                    new Rectangle(
                        bounds.Left + padding,
                        dataTop + 17,
                        26,
                        17);

                Rectangle peValueBounds =
                    new Rectangle(
                        bounds.Left + padding + 28,
                        dataTop + 17,
                        bounds.Width -
                        padding * 2 -
                        28,
                        17);

                graphics.DrawString(
                    "AP",
                    compactFont,
                    titleBrush,
                    apLabelBounds);

                graphics.DrawString(
                    FormatDistance(
                        telemetry.Apoapsis),
                    compactFont,
                    titleBrush,
                    apValueBounds,
                    rightFormat);

                graphics.DrawString(
                    "PE",
                    compactFont,
                    titleBrush,
                    peLabelBounds);

                graphics.DrawString(
                    FormatDistance(
                        telemetry.Periapsis),
                    compactFont,
                    titleBrush,
                    peValueBounds,
                    rightFormat);
            }
        }

        private void DrawGuidancePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            Graphics graphics =
                context.Graphics;

            double targetAltitude =
                CalculateTargetAltitude(
                    _downrangeMeters,
                    telemetry);

            string guidance =
                DetermineGuidance(
                    telemetry,
                    targetAltitude);

            float compactSize =
                Math.Max(
                    6.0f,
                    context.SmallFont.Size *
                    0.78f);

            using (Font compactFont =
                new Font(
                    context.SmallFont.FontFamily,
                    compactSize,
                    context.SmallFont.Style,
                    GraphicsUnit.Point))
            using (Pen borderPen =
                new Pen(
                    context.PhosphorColor,
                    1.0f))
            using (Pen dividerPen =
                new Pen(
                    context.DimPhosphorColor,
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

                int padding = 8;

                graphics.DrawString(
                    "FLIGHT DIRECTOR",
                    compactFont,
                    titleBrush,
                    bounds.Left + padding,
                    bounds.Top + 7);

                int rowTop =
                    bounds.Top + 31;

                int guidanceHeight =
                    Math.Max(
                        62,
                        bounds.Height / 4);

                int guidanceTop =
                    bounds.Bottom -
                    guidanceHeight;

                int availableMetricHeight =
                    Math.Max(
                        80,
                        guidanceTop -
                        rowTop -
                        6);

                int rowHeight =
                    Math.Max(
                        18,
                        availableMetricHeight /
                        8);

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "TGT AP",
                    FormatDistance(
                        DefaultTargetApoapsisMeters));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "RANGE",
                    FormatDistance(
                        _downrangeMeters));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "TGT ALT",
                    FormatDistance(
                        targetAltitude));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "ALT",
                    FormatDistance(
                        telemetry.Altitude));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "ALT ERR",
                    FormatSignedDistance(
                        telemetry.Altitude -
                        targetAltitude));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "TGT PITCH",
                    FormatAngle(
                        CalculateTargetPitch(
                            _downrangeMeters,
                            telemetry)));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "PITCH",
                    FormatAngle(
                        telemetry.Pitch));

                DrawCompactPanelRow(
                    graphics,
                    compactFont,
                    labelBrush,
                    valueBrush,
                    bounds,
                    ref rowTop,
                    rowHeight,
                    "DYN Q",
                    FormatPressure(
                        telemetry.DynamicPressureKpa));

                graphics.DrawLine(
                    dividerPen,
                    bounds.Left + padding,
                    guidanceTop,
                    bounds.Right - padding,
                    guidanceTop);

                graphics.DrawString(
                    "GUIDANCE",
                    compactFont,
                    titleBrush,
                    bounds.Left + padding,
                    guidanceTop + 5);

                Rectangle guidanceBounds =
                    new Rectangle(
                        bounds.Left + padding,
                        guidanceTop + 22,
                        bounds.Width -
                        padding * 2,
                        Math.Max(
                            18,
                            bounds.Bottom -
                            guidanceTop -
                            28));

                using (StringFormat guidanceFormat =
                    new StringFormat())
                {
                    guidanceFormat.Trimming =
                        StringTrimming.EllipsisWord;

                    guidanceFormat.FormatFlags =
                        StringFormatFlags.LineLimit;

                    graphics.DrawString(
                        guidance,
                        compactFont,
                        valueBrush,
                        guidanceBounds,
                        guidanceFormat);
                }
            }
        }

        private static void DrawCompactPanelRow(
            Graphics graphics,
            Font font,
            Brush labelBrush,
            Brush valueBrush,
            Rectangle panelBounds,
            ref int y,
            int rowHeight,
            string label,
            string value)
        {
            int padding = 8;

            int left =
                panelBounds.Left +
                padding;

            int right =
                panelBounds.Right -
                padding;

            int availableWidth =
                Math.Max(
                    20,
                    right - left);

            Rectangle labelBounds =
                new Rectangle(
                    left,
                    y,
                    availableWidth / 2,
                    rowHeight);

            Rectangle valueBounds =
                new Rectangle(
                    left +
                    availableWidth / 2,
                    y,
                    availableWidth -
                    availableWidth / 2,
                    rowHeight);

            using (StringFormat labelFormat =
                new StringFormat())
            using (StringFormat valueFormat =
                new StringFormat())
            {
                labelFormat.Alignment =
                    StringAlignment.Near;

                labelFormat.LineAlignment =
                    StringAlignment.Center;

                labelFormat.Trimming =
                    StringTrimming.EllipsisCharacter;

                labelFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

                valueFormat.Alignment =
                    StringAlignment.Far;

                valueFormat.LineAlignment =
                    StringAlignment.Center;

                valueFormat.Trimming =
                    StringTrimming.EllipsisCharacter;

                valueFormat.FormatFlags =
                    StringFormatFlags.NoWrap;

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

            y += rowHeight;
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
                GetPlanningProfileScale(
                    telemetry);

            return Math.Max(
                120000.0,
                Math.Max(
                    current * 1.20,
                    profileScale * 4.5));
        }

        private double CalculateTargetAltitude(
            double downrangeMeters,
            MissionTelemetry telemetry)
        {
            double profileScale =
                GetPlanningProfileScale(
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

        private double CalculateTargetPitch(
            double downrangeMeters,
            MissionTelemetry telemetry)
        {
            double scale =
                GetPlanningProfileScale(
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

        private double GetPlanningProfileScale(
            MissionTelemetry telemetry)
        {
            if (IsFinite(
                    _planningProfileScale))
            {
                return _planningProfileScale;
            }

            double fallbackTwr =
                telemetry != null &&
                IsFinite(
                    telemetry.ThrustToWeightRatio)
                    ? telemetry
                        .ThrustToWeightRatio
                    : 1.5;

            return CalculateProfileScaleFromTwr(
                fallbackTwr);
        }

        private static double CalculateProfileScaleFromTwr(
            double twr)
        {
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

        private string DetermineGuidance(
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

            public bool DebugWritten { get; set; }
        }
    }
}