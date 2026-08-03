using System;
using System.Drawing;
using KMC.MissionControl.Diagnostics;
using KMC.MissionControl.Flight;
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

        private double _planningTwr =
            double.NaN;

        private double _planningProfileScale =
            double.NaN;

        private int _initialStage =
            -1;

        private readonly AscentFlightHistory _flightHistory =
            new AscentFlightHistory();

        private readonly AscentBurnoutPredictor _burnoutPredictor =
            new AscentBurnoutPredictor();

        private readonly MissionPlanner _missionPlanner =
            new MissionPlanner();

        private readonly FlightDirectorRenderer _flightDirectorRenderer =
            new FlightDirectorRenderer();

        private readonly PredictionRenderer _predictionRenderer =
            new PredictionRenderer();

        private readonly OrbitTrendRenderer _orbitTrendRenderer =
            new OrbitTrendRenderer();

        private readonly FooterRenderer _footerRenderer =
            new FooterRenderer();

        private readonly AscentHeaderRenderer _headerRenderer =
            new AscentHeaderRenderer();

        private readonly AscentGraphRenderer _ascentGraphRenderer =
            new AscentGraphRenderer();

        private readonly AscentDebugLogger _debugLogger =
            new AscentDebugLogger();

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

            _headerRenderer.Draw(
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
            AscentLayout layout =
                AscentLayout.Create(
                    context);

            DrawAscentGraph(
                context,
                layout.Graph,
                telemetry);

            DrawOrbitInset(
                context,
                layout.OrbitTrend,
                telemetry);

            DrawGuidancePanel(
                context,
                layout.FlightDirector,
                telemetry);

            DrawPredictivePanel(
                context,
                layout.Prediction,
                telemetry);

            DrawFooter(
                context,
                layout.Footer,
                telemetry);
        }

        private void UpdateHistory(
            MissionTelemetry telemetry)
        {
            AscentFlightHistoryUpdate update =
                _flightHistory.Update(
                    telemetry);

            if (!update.MissionReset)
            {
                return;
            }

            /*
             * The history component owns trajectory state. Page-specific
             * planning and prediction state still resets here.
             */
            _planningTwr =
                double.NaN;

            _planningProfileScale =
                double.NaN;

            _initialStage =
                -1;

            _burnoutPredictor.Reset();
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
                _flightHistory.Samples.Count == 0)
            {
                return;
            }

            AscentHistorySample sample =
                _flightHistory.Samples[_flightHistory.Samples.Count - 1];

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
                _burnoutPredictor.Calculate(
                    telemetry,
                    _flightHistory.Samples,
                    DefaultTargetApoapsisMeters);

            MissionPlannerResult missionPlan =
                _missionPlanner.CreatePlan(
                    telemetry,
                    targetAltitude,
                    targetPitch,
                    DefaultTargetApoapsisMeters);

            AscentDebugRecord record =
                new AscentDebugRecord
                {
                    MissionTimeSeconds =
                        telemetry.MissionTime,

                    Stage =
                        telemetry.CurrentStage,

                    InitialStage =
                        _initialStage,

                    AltitudeMeters =
                        telemetry.Altitude,

                    DownrangeMeters =
                        sample.DownrangeMeters,

                    LiveThrustToWeightRatio =
                        telemetry.ThrustToWeightRatio,

                    PlanningThrustToWeightRatio =
                        _planningTwr,

                    ProfileScaleMeters =
                        profileScale,

                    TargetAltitudeMeters =
                        targetAltitude,

                    TargetPitchDegrees =
                        targetPitch,

                    ActualPitchDegrees =
                        telemetry.Pitch,

                    ApoapsisMeters =
                        telemetry.Apoapsis,

                    PredictionAvailable =
                        prediction.IsAvailable,

                    BurnTimeRemainingSeconds =
                        prediction.TimeRemainingSeconds,

                    PredictedBurnoutVelocityMetersPerSecond =
                        prediction.BurnoutVelocityMetersPerSecond,

                    PredictedApoapsisMeters =
                        prediction.PredictedApoapsisMeters,

                    PredictionTargetErrorMeters =
                        prediction.PredictedApoapsisMeters -
                        DefaultTargetApoapsisMeters,

                    PredictionConfidencePercent =
                        prediction.ConfidencePercent,

                    PredictionStatus =
                        prediction.Status,

                    MissionPlan =
                        missionPlan
                };

            _debugLogger.Write(
                record);
        }

        private void DrawAscentGraph(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry)
        {
            double maxDownrange =
                CalculateGraphDownrangeLimit(
                    telemetry);

            double maxAltitude =
                Math.Max(
                    DefaultTargetApoapsisMeters *
                    1.15,
                    GetMaximumActualAltitude() *
                    1.10);

            const int targetPointCount =
                120;

            AscentGraphPoint[] targetPoints =
                new AscentGraphPoint[
                    targetPointCount];

            for (int index = 0;
                 index < targetPointCount;
                 index++)
            {
                double fraction =
                    index /
                    (double)(
                        targetPointCount -
                        1);

                double downrange =
                    maxDownrange *
                    fraction;

                targetPoints[index] =
                    new AscentGraphPoint
                    {
                        DownrangeMeters =
                            downrange,

                        AltitudeMeters =
                            CalculateTargetAltitude(
                                downrange,
                                telemetry)
                    };
            }

            AscentGraphPoint[] actualPoints =
                new AscentGraphPoint[
                    _flightHistory.Samples.Count];

            for (int index = 0;
                 index < _flightHistory.Samples.Count;
                 index++)
            {
                AscentHistorySample sample =
                    _flightHistory.Samples[index];

                actualPoints[index] =
                    new AscentGraphPoint
                    {
                        DownrangeMeters =
                            sample.DownrangeMeters,

                        AltitudeMeters =
                            sample.AltitudeMeters
                    };
            }

            AscentGraphRenderModel model =
                new AscentGraphRenderModel
                {
                    MaximumDownrangeMeters =
                        maxDownrange,

                    MaximumAltitudeMeters =
                        maxAltitude,

                    TargetPoints =
                        targetPoints,

                    ActualPoints =
                        actualPoints
                };

            _ascentGraphRenderer.Draw(
                context,
                bounds,
                model);
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
                    _flightHistory.DownrangeMeters,
                    telemetry);

            double targetPitch =
                CalculateTargetPitch(
                    _flightHistory.DownrangeMeters,
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
                        _flightHistory.DownrangeMeters,

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
                _burnoutPredictor.Calculate(
                    telemetry,
                    _flightHistory.Samples,
                    DefaultTargetApoapsisMeters);

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

            FooterRenderModel model =
                new FooterRenderModel
                {
                    MissionTimeSeconds =
                        telemetry.MissionTime,

                    CurrentStage =
                        telemetry.CurrentStage,

                    AltitudeMeters =
                        telemetry.Altitude,

                    DownrangeMeters =
                        _flightHistory.DownrangeMeters,

                    VerticalSpeedMetersPerSecond =
                        telemetry.VerticalSpeed,

                    HorizontalSpeedMetersPerSecond =
                        telemetry.HorizontalSpeed,

                    ThrustToWeightRatio =
                        telemetry.ThrustToWeightRatio,

                    GForce =
                        telemetry.GForce,

                    ApoapsisMeters =
                        telemetry.Apoapsis,

                    FuelPercent =
                        fuelPercent,

                    Status =
                        status
                };

            _footerRenderer.Draw(
                context,
                bounds,
                model);
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
                    _flightHistory.DownrangeMeters);

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

            foreach (AscentHistorySample sample in
                _flightHistory.Samples)
            {
                maximum =
                    Math.Max(
                        maximum,
                        sample.AltitudeMeters);
            }

            return maximum;
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

    }
}
