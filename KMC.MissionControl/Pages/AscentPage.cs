using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using KMC.MissionControl.Guidance;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Ascent;

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

        private int _predictionStage =
            -1;

        private double _predictionStageStartTime =
            double.NaN;

        private readonly MissionPlanner _missionPlanner =
            new MissionPlanner();

        private readonly FlightDirectorRenderer _flightDirectorRenderer =
            new FlightDirectorRenderer();

        private readonly PredictionRenderer _predictionRenderer =
            new PredictionRenderer();

        private readonly OrbitTrendRenderer _orbitTrendRenderer =
            new OrbitTrendRenderer();

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

            /*
             * Phase 5 approved widescreen layout.
             *
             * The panel allocations intentionally include generous
             * spacing so future data does not overlap or clip.
             */
            Rectangle graphBounds =
                context.GetRelativeRectangle(
                    0.008f,
                    0.070f,
                    0.555f,
                    0.765f);

            Rectangle orbitInsetBounds =
                context.GetRelativeRectangle(
                    0.575f,
                    0.070f,
                    0.417f,
                    0.220f);

            Rectangle statusBounds =
                context.GetRelativeRectangle(
                    0.575f,
                    0.302f,
                    0.417f,
                    0.325f);

            Rectangle predictionBounds =
                context.GetRelativeRectangle(
                    0.575f,
                    0.639f,
                    0.417f,
                    0.196f);

            Rectangle footerBounds =
                context.GetRelativeRectangle(
                    0.008f,
                    0.850f,
                    0.984f,
                    0.140f);

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

            DrawPredictivePanel(
                context,
                predictionBounds,
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

            if (timeReset)
            {
                ResetHistory(
                    vesselName);
            }
            else if (vesselChanged)
            {
                /*
                 * KSP may change the active vessel name during staging,
                 * separation, docking, or control-point transfer.
                 *
                 * Mission time is still moving forward, so this is the
                 * same ascent. Preserve downrange, profile, samples, and
                 * predictor history. Only update the tracked name.
                 */
                _trackedVesselName =
                    vesselName;
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
                            telemetry.DynamicPressureKpa,

                        StageLiquidFuelAmount =
                            telemetry.StageLiquidFuelAmount,

                        StageOxidizerAmount =
                            telemetry.StageOxidizerAmount,

                        OrbitalSpeedMetersPerSecond =
                            telemetry.OrbitalSpeed,

                        VesselMassTonnes =
                            telemetry.VesselMass,

                        CurrentThrustKilonewtons =
                            telemetry.CurrentThrust,

                        AverageSpecificImpulseSeconds =
                            telemetry.AverageSpecificImpulse,

                        StageNumber =
                            telemetry.CurrentStage
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

            _predictionStage =
                -1;

            _predictionStageStartTime =
                double.NaN;
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

            BurnoutPrediction prediction =
                CalculateBurnoutPrediction(
                    telemetry);

            MissionPlannerResult missionPlan =
                _missionPlanner.CreatePlan(
                    telemetry,
                    targetAltitude,
                    targetPitch,
                    DefaultTargetApoapsisMeters);

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

                /*
                 * Output:
                 * %LOCALAPPDATA%\KMC\ascent-debug.csv
                 */

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
                                "ApoapsisM,BurnTimeRemainingS," +
                                "PredictedBurnoutVelocityMps," +
                                "PredictedApoapsisM," +
                                "PredictionTargetErrorM," +
                                "PredictionConfidencePercent," +
                                "PredictionStatus," +
                                "PlannerNominalPitchDeg," +
                                "PlannerRecommendedPitchDeg," +
                                "PlannerPitchCorrectionDeg," +
                                "PlannerRecoveryAuthorityPercent," +
                                "PlannerTargetAchievable," +
                                "PlannerFlightPhase," +
                                "PlannerThrottleCommandPercent," +
                                "PlannerCutoffRequired," +
                                "PlannerCoastLockoutActive," +
                                "PlannerCommand," +
                                "PlannerThrottleCommand," +
                                "PlannerStatus," +
                                "PlannerNextEvent," +
                                "CircularizationAvailable," +
                                "CircularizationDeltaV," +
                                "CircularizationBurnTimeS," +
                                "CircularizationIgnitionInS," +
                                "CircularizationPeriapsisErrorM," +
                                "CircularizationPitchDeg," +
                                "MecoCountdownSeconds," +
                                "FlashAlert," +
                                "PredictedShutdownApoapsisM," +
                                "PredictedShutdownPeriapsisM," +
                                "PredictedOrbitErrorM," +
                                "OrbitalEnergyError");
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
                                    .ToString("0.000"),
                                prediction.IsAvailable
                                    ? prediction
                                        .TimeRemainingSeconds
                                        .ToString("0.000")
                                    : string.Empty,
                                prediction.IsAvailable
                                    ? prediction
                                        .BurnoutVelocityMetersPerSecond
                                        .ToString("0.000")
                                    : string.Empty,
                                prediction.IsAvailable
                                    ? prediction
                                        .PredictedApoapsisMeters
                                        .ToString("0.000")
                                    : string.Empty,
                                prediction.IsAvailable
                                    ? (prediction
                                        .PredictedApoapsisMeters -
                                       DefaultTargetApoapsisMeters)
                                        .ToString("0.000")
                                    : string.Empty,
                                prediction.IsAvailable
                                    ? prediction
                                        .ConfidencePercent
                                        .ToString("0.000")
                                    : string.Empty,
                                EscapeCsvField(
                                    prediction.Status),
                                missionPlan
                                    .NominalPitchDegrees
                                    .ToString("0.000"),
                                missionPlan
                                    .RecommendedPitchDegrees
                                    .ToString("0.000"),
                                missionPlan
                                    .PitchCorrectionDegrees
                                    .ToString("0.000"),
                                missionPlan
                                    .RecoveryAuthorityPercent
                                    .ToString("0.000"),
                                missionPlan
                                    .IsTargetAchievable
                                    ? "1"
                                    : "0",
                                EscapeCsvField(
                                    missionPlan.FlightPhase),
                                missionPlan
                                    .ThrottleCommandPercent
                                    .ToString("0.000"),
                                missionPlan
                                    .CutoffRequired
                                    ? "1"
                                    : "0",
                                missionPlan
                                    .CoastLockoutActive
                                    ? "1"
                                    : "0",
                                EscapeCsvField(
                                    missionPlan.Command),
                                EscapeCsvField(
                                    missionPlan.ThrottleCommand),
                                EscapeCsvField(
                                    missionPlan.Status),
                                EscapeCsvField(
                                    missionPlan.NextEvent),
                                missionPlan
                                    .CircularizationAvailable
                                    ? "1"
                                    : "0",
                                missionPlan
                                    .CircularizationDeltaV
                                    .ToString("0.000"),
                                missionPlan
                                    .CircularizationBurnTimeSeconds
                                    .ToString("0.000"),
                                missionPlan
                                    .CircularizationIgnitionInSeconds
                                    .ToString("0.000"),
                                missionPlan
                                    .CircularizationPeriapsisErrorMeters
                                    .ToString("0.000"),
                                missionPlan
                                    .CircularizationPitchDegrees
                                    .ToString("0.000"),
                                missionPlan
                                    .MecoCountdownSeconds,
                                missionPlan
                                    .FlashAlert
                                    ? "1"
                                    : "0",
                                missionPlan
                                    .PredictedShutdownApoapsisMeters
                                    .ToString("0.000"),
                                missionPlan
                                    .PredictedShutdownPeriapsisMeters
                                    .ToString("0.000"),
                                missionPlan
                                    .PredictedOrbitErrorMeters
                                    .ToString("0.000"),
                                missionPlan
                                    .OrbitalEnergyError
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

        private static string EscapeCsvField(
            string value)
        {
            if (string.IsNullOrEmpty(
                    value))
            {
                return string.Empty;
            }

            bool requiresQuotes =
                value.IndexOf(',') >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0;

            if (!requiresQuotes)
            {
                return value;
            }

            return
                "\"" +
                value.Replace(
                    "\"",
                    "\"\"") +
                "\"";
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
                        -64,
                        -54);

                plot.Y += 18;
                plot.Height -= 18;

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

        private void DrawOrbitInset(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            OrbitTrendRenderModel model =
                new OrbitTrendRenderModel
                {
                    Eccentricity =
                        telemetry.Eccentricity,

                    TrueAnomalyDegrees =
                        telemetry.TrueAnomalyDegrees,

                    ApoapsisMeters =
                        telemetry.Apoapsis,

                    PeriapsisMeters =
                        telemetry.Periapsis,

                    InclinationDegrees =
                        telemetry.InclinationDegrees
                };

            _orbitTrendRenderer.Draw(
                context,
                bounds,
                model);
        }

        private void DrawGuidancePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            double targetAltitude =
                CalculateTargetAltitude(
                    _downrangeMeters,
                    telemetry);

            double targetPitch =
                CalculateTargetPitch(
                    _downrangeMeters,
                    telemetry);

            MissionPlannerResult missionPlan =
                _missionPlanner.CreatePlan(
                    telemetry,
                    targetAltitude,
                    targetPitch,
                    DefaultTargetApoapsisMeters);

            FlightDirectorRenderModel model =
                new FlightDirectorRenderModel
                {
                    TargetApoapsisMeters =
                        DefaultTargetApoapsisMeters,

                    DownrangeMeters =
                        _downrangeMeters,

                    TargetAltitudeMeters =
                        targetAltitude,

                    ActualAltitudeMeters =
                        telemetry.Altitude,

                    ActualPitchDegrees =
                        telemetry.Pitch,

                    DynamicPressureKpa =
                        telemetry.DynamicPressureKpa,

                    MissionTimeSeconds =
                        telemetry.MissionTime,

                    Plan =
                        missionPlan
                };

            _flightDirectorRenderer.Draw(
                context,
                bounds,
                model);
        }

        private void DrawPredictivePanel(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            BurnoutPrediction prediction =
                CalculateBurnoutPrediction(
                    telemetry);

            PredictionRenderModel model =
                new PredictionRenderModel
                {
                    IsAvailable =
                        prediction.IsAvailable,

                    TimeRemainingSeconds =
                        prediction.TimeRemainingSeconds,

                    BurnoutVelocityMetersPerSecond =
                        prediction.BurnoutVelocityMetersPerSecond,

                    PredictedApoapsisMeters =
                        prediction.PredictedApoapsisMeters,

                    TargetApoapsisMeters =
                        DefaultTargetApoapsisMeters,

                    ConfidencePercent =
                        prediction.ConfidencePercent,

                    Status =
                        prediction.Status
                };

            _predictionRenderer.Draw(
                context,
                bounds,
                model);
        }

        private BurnoutPrediction CalculateBurnoutPrediction(
            MissionTelemetry telemetry)
        {
            BurnoutPrediction result =
                new BurnoutPrediction
                {
                    Status =
                        "COLLECTING DATA"
                };

            if (telemetry == null)
            {
                return result;
            }

            if (_predictionStage !=
                telemetry.CurrentStage)
            {
                _predictionStage =
                    telemetry.CurrentStage;

                _predictionStageStartTime =
                    telemetry.MissionTime;

                result.Status =
                    "STAGE TREND RESET";

                return result;
            }

            if (!IsFinite(
                    _predictionStageStartTime))
            {
                _predictionStageStartTime =
                    telemetry.MissionTime;
            }

            double stageAge =
                telemetry.MissionTime -
                _predictionStageStartTime;

            if (stageAge < 2.5)
            {
                result.Status =
                    "COLLECTING STAGE DATA";

                return result;
            }

            List<AscentSample> window =
                GetPredictionWindow(
                    telemetry.CurrentStage,
                    telemetry.MissionTime,
                    6.0);

            if (window.Count < 8)
            {
                result.Status =
                    "COLLECTING DATA";

                return result;
            }

            AscentSample newest =
                window[window.Count - 1];

            double elapsed =
                newest.MissionTime -
                window[0].MissionTime;

            if (elapsed < 1.5)
            {
                return result;
            }

            double liquidFuelRate =
                CalculateConsumptionRate(
                    window,
                    sample =>
                        sample.StageLiquidFuelAmount);

            double oxidizerRate =
                CalculateConsumptionRate(
                    window,
                    sample =>
                        sample.StageOxidizerAmount);

            double liquidFuelTime =
                liquidFuelRate > 0.0001
                    ? newest.StageLiquidFuelAmount /
                      liquidFuelRate
                    : double.PositiveInfinity;

            double oxidizerTime =
                oxidizerRate > 0.0001
                    ? newest.StageOxidizerAmount /
                      oxidizerRate
                    : double.PositiveInfinity;

            double timeRemaining =
                Math.Min(
                    liquidFuelTime,
                    oxidizerTime);

            if (!IsFinite(timeRemaining) ||
                timeRemaining <= 0.0 ||
                timeRemaining > 1800.0)
            {
                result.Status =
                    telemetry.CurrentThrust > 0.1
                        ? "FUEL TREND UNAVAILABLE"
                        : "ENGINE OFF";

                return result;
            }

            RegressionResult apoapsisTrend =
                CalculateRegression(
                    window,
                    sample =>
                        sample.ApoapsisMeters);

            RegressionResult velocityTrend =
                CalculateRegression(
                    window,
                    sample =>
                        sample.OrbitalSpeedMetersPerSecond);

            if (!apoapsisTrend.IsValid ||
                !velocityTrend.IsValid)
            {
                result.Status =
                    "TREND UNSTABLE";

                return result;
            }

            double predictedApoapsis =
                newest.ApoapsisMeters +
                apoapsisTrend.SlopePerSecond *
                timeRemaining;

            double predictedVelocity =
                newest.OrbitalSpeedMetersPerSecond +
                velocityTrend.SlopePerSecond *
                timeRemaining;

            double fuelConsistency =
                CalculateFuelConsistency(
                    window);

            double trendQuality =
                Math.Min(
                    apoapsisTrend.RSquared,
                    velocityTrend.RSquared);

            double sampleQuality =
                Math.Min(
                    1.0,
                    window.Count /
                    24.0);

            double confidence =
                100.0 *
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        trendQuality *
                        0.55 +
                        fuelConsistency *
                        0.25 +
                        sampleQuality *
                        0.20));

            result.IsAvailable = true;
            result.HasFuelTrend = true;
            result.TimeRemainingSeconds =
                timeRemaining;
            result.PredictedApoapsisMeters =
                Math.Max(
                    newest.ApoapsisMeters,
                    predictedApoapsis);
            result.BurnoutVelocityMetersPerSecond =
                Math.Max(
                    0.0,
                    predictedVelocity);
            result.ConfidencePercent =
                confidence;

            double targetError =
                result.PredictedApoapsisMeters -
                DefaultTargetApoapsisMeters;

            if (confidence < 35.0)
            {
                result.Status =
                    "LOW CONFIDENCE";
            }
            else if (targetError < -5000.0)
            {
                result.Status =
                    "TARGET AT RISK";
            }
            else if (targetError > 8000.0)
            {
                result.Status =
                    "OVERSHOOT LIKELY";
            }
            else
            {
                result.Status =
                    "TARGET ACHIEVABLE";
            }

            return result;
        }

        private List<AscentSample> GetPredictionWindow(
            int stage,
            double currentMissionTime,
            double windowSeconds)
        {
            List<AscentSample> result =
                new List<AscentSample>();

            double earliestTime =
                currentMissionTime -
                windowSeconds;

            for (int index =
                    _samples.Count - 1;
                 index >= 0;
                 index--)
            {
                AscentSample sample =
                    _samples[index];

                if (sample.MissionTime <
                    earliestTime)
                {
                    break;
                }

                if (sample.StageNumber ==
                    stage)
                {
                    result.Add(
                        sample);
                }
            }

            result.Reverse();

            return result;
        }

        private static double CalculateConsumptionRate(
            IList<AscentSample> samples,
            Func<AscentSample, double> selector)
        {
            RegressionResult trend =
                CalculateRegression(
                    samples,
                    selector);

            if (!trend.IsValid)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                -trend.SlopePerSecond);
        }

        private static double CalculateFuelConsistency(
            IList<AscentSample> samples)
        {
            RegressionResult liquidFuel =
                CalculateRegression(
                    samples,
                    sample =>
                        sample.StageLiquidFuelAmount);

            RegressionResult oxidizer =
                CalculateRegression(
                    samples,
                    sample =>
                        sample.StageOxidizerAmount);

            double best =
                Math.Max(
                    liquidFuel.RSquared,
                    oxidizer.RSquared);

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    best));
        }

        private static RegressionResult CalculateRegression(
            IList<AscentSample> samples,
            Func<AscentSample, double> selector)
        {
            RegressionResult result =
                new RegressionResult();

            if (samples == null ||
                selector == null ||
                samples.Count < 3)
            {
                return result;
            }

            double origin =
                samples[0].MissionTime;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXX = 0.0;
            double sumXY = 0.0;

            int count = 0;

            for (int index = 0;
                 index < samples.Count;
                 index++)
            {
                double x =
                    samples[index].MissionTime -
                    origin;

                double y =
                    selector(
                        samples[index]);

                if (!IsFinite(x) ||
                    !IsFinite(y))
                {
                    continue;
                }

                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
                count++;
            }

            if (count < 3)
            {
                return result;
            }

            double denominator =
                count *
                sumXX -
                sumX *
                sumX;

            if (Math.Abs(denominator) <
                0.000001)
            {
                return result;
            }

            double slope =
                (count *
                 sumXY -
                 sumX *
                 sumY) /
                denominator;

            double intercept =
                (sumY -
                 slope *
                 sumX) /
                count;

            double meanY =
                sumY /
                count;

            double totalVariation = 0.0;
            double residualVariation = 0.0;

            for (int index = 0;
                 index < samples.Count;
                 index++)
            {
                double x =
                    samples[index].MissionTime -
                    origin;

                double y =
                    selector(
                        samples[index]);

                if (!IsFinite(x) ||
                    !IsFinite(y))
                {
                    continue;
                }

                double fitted =
                    intercept +
                    slope *
                    x;

                double totalError =
                    y -
                    meanY;

                double residualError =
                    y -
                    fitted;

                totalVariation +=
                    totalError *
                    totalError;

                residualVariation +=
                    residualError *
                    residualError;
            }

            double rSquared =
                totalVariation > 0.000001
                    ? 1.0 -
                      residualVariation /
                      totalVariation
                    : 1.0;

            result.IsValid = true;
            result.SlopePerSecond =
                slope;
            result.RSquared =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        rSquared));

            return result;
        }

        private static string FormatDurationCompact(
            double totalSeconds)
        {
            if (!IsFinite(totalSeconds) ||
                totalSeconds < 0.0)
            {
                return "---";
            }

            if (totalSeconds < 100.0)
            {
                return
                    totalSeconds.ToString("0.0") +
                    " S";
            }

            int minutes =
                (int)(totalSeconds / 60.0);

            int seconds =
                (int)(totalSeconds % 60.0);

            return string.Format(
                "{0:00}:{1:00}",
                minutes,
                seconds);
        }

        private void DrawFooter(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            Graphics graphics =
                context.Graphics;

            double fuelPercent =
                CalculateStageFuelPercent(
                    telemetry);

            string status =
                telemetry.MissionTime < 1.0
                    ? "PAD"
                    : telemetry.Apoapsis >=
                      DefaultTargetApoapsisMeters
                        ? "TARGET AP"
                        : "ASCENT";

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
                        telemetry.MissionTime),
                    telemetry.CurrentStage
                        .ToString("00"),
                    FormatDistance(
                        telemetry.Altitude),
                    FormatDistance(
                        _downrangeMeters),
                    FormatSignedSpeed(
                        telemetry.VerticalSpeed),
                    FormatSpeed(
                        telemetry.HorizontalSpeed),
                    FormatRatio(
                        telemetry.ThrustToWeightRatio),
                    FormatGForceCompact(
                        telemetry.GForce),
                    FormatDistance(
                        telemetry.Apoapsis),
                    IsFinite(fuelPercent)
                        ? fuelPercent
                            .ToString("0") +
                          " %"
                        : "---",
                    status
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
                new StringFormat())
            {
                format.Alignment =
                    StringAlignment.Center;

                format.LineAlignment =
                    StringAlignment.Center;

                format.Trimming =
                    StringTrimming.EllipsisCharacter;

                format.FormatFlags =
                    StringFormatFlags.NoWrap;

                graphics.DrawString(
                    label,
                    context.SmallFont,
                    labelBrush,
                    labelBounds,
                    format);

                graphics.DrawString(
                    value,
                    context.SmallFont,
                    valueBrush,
                    valueBounds,
                    format);
            }
        }

        private static double CalculateStageFuelPercent(
            MissionTelemetry telemetry)
        {
            double amount =
                Math.Max(
                    0.0,
                    telemetry.StageLiquidFuelAmount) +
                Math.Max(
                    0.0,
                    telemetry.StageOxidizerAmount);

            double capacity =
                Math.Max(
                    0.0,
                    telemetry.StageLiquidFuelCapacity) +
                Math.Max(
                    0.0,
                    telemetry.StageOxidizerCapacity);

            if (capacity <= 0.0)
            {
                return double.NaN;
            }

            return
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        amount /
                        capacity *
                        100.0));
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

            public double StageLiquidFuelAmount { get; set; }

            public double StageOxidizerAmount { get; set; }

            public double OrbitalSpeedMetersPerSecond { get; set; }

            public double VesselMassTonnes { get; set; }

            public double CurrentThrustKilonewtons { get; set; }

            public double AverageSpecificImpulseSeconds { get; set; }

            public int StageNumber { get; set; }

            public bool DebugWritten { get; set; }
        }

        private sealed class BurnoutPrediction
        {
            public bool IsAvailable { get; set; }

            public bool HasFuelTrend { get; set; }

            public double TimeRemainingSeconds { get; set; }

            public double BurnoutVelocityMetersPerSecond { get; set; }

            public double PredictedApoapsisMeters { get; set; }

            public double ConfidencePercent { get; set; }

            public string Status { get; set; }
        }

        private sealed class RegressionResult
        {
            public bool IsValid { get; set; }

            public double SlopePerSecond { get; set; }

            public double RSquared { get; set; }
        }
    }
}
