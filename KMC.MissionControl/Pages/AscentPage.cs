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
        private readonly MissionTarget _missionTarget =
            new MissionTarget(
                80000.0);

        private readonly AscentProfilePlanner _profilePlanner =
            new AscentProfilePlanner();

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

        /// <summary>
        /// Requested ascent apoapsis in meters.
        ///
        /// The current UI still uses the default 80 km value, but future
        /// controls can change this property and every dependent calculation
        /// will use the new target.
        /// </summary>
        public double TargetApoapsisMeters
        {
            get
            {
                return _missionTarget
                    .TargetApoapsisMeters;
            }

            set
            {
                if (Math.Abs(
                        _missionTarget
                            .TargetApoapsisMeters -
                        value) <
                    0.001)
                {
                    return;
                }

                _missionTarget
                    .TargetApoapsisMeters =
                    value;

                _profilePlanner.Reset();
                _burnoutPredictor.Reset();
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
            _profilePlanner.Reset();

            _initialStage =
                -1;

            _burnoutPredictor.Reset();
        }

        private void CaptureLaunchPlan(
            MissionTelemetry telemetry)
        {
            if (!_profilePlanner.CaptureLaunchPlan(
                    telemetry))
            {
                return;
            }

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
                _profilePlanner.GetProfileScale(
                    telemetry);

            double targetAltitude =
                _profilePlanner.CalculateTargetAltitude(
                    sample.DownrangeMeters,
                    telemetry,
                    _missionTarget.TargetApoapsisMeters);

            double targetPitch =
                _profilePlanner.CalculateTargetPitch(
                    sample.DownrangeMeters,
                    telemetry,
                    _missionTarget.TargetApoapsisMeters);

            BurnoutPrediction prediction =
                _burnoutPredictor.Calculate(
                    telemetry,
                    _flightHistory.Samples,
                    _missionTarget.TargetApoapsisMeters);

            MissionPlannerResult missionPlan =
                _missionPlanner.CreatePlan(
                    telemetry,
                    targetAltitude,
                    targetPitch,
                    _missionTarget.TargetApoapsisMeters);

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
                        _profilePlanner.PlanningThrustToWeightRatio,

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
                        _missionTarget.TargetApoapsisMeters,

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
                    _missionTarget.TargetApoapsisMeters *
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
                            _profilePlanner.CalculateTargetAltitude(
                                downrange,
                                telemetry,
                                _missionTarget.TargetApoapsisMeters)
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
                _profilePlanner.CalculateTargetAltitude(
                    _flightHistory.DownrangeMeters,
                    telemetry,
                    _missionTarget.TargetApoapsisMeters);

            double targetPitch =
                _profilePlanner.CalculateTargetPitch(
                    _flightHistory.DownrangeMeters,
                    telemetry,
                    _missionTarget.TargetApoapsisMeters);

            MissionPlannerResult missionPlan =
                _missionPlanner.CreatePlan(
                    telemetry,
                    targetAltitude,
                    targetPitch,
                    _missionTarget.TargetApoapsisMeters);

            FlightDirectorRenderModel model =
                new FlightDirectorRenderModel
                {
                    TargetApoapsisMeters =
                        _missionTarget.TargetApoapsisMeters,

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
                    _missionTarget.TargetApoapsisMeters);

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
                        _missionTarget.TargetApoapsisMeters,

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
                      _missionTarget.TargetApoapsisMeters
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
                _profilePlanner.GetProfileScale(
                    telemetry);

            return Math.Max(
                120000.0,
                Math.Max(
                    current * 1.20,
                    profileScale * 4.5));
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
                _profilePlanner.CalculateTargetPitch(
                    Math.Max(
                        0.0,
                        telemetry.HorizontalSpeed *
                        telemetry.MissionTime *
                        0.55),
                    telemetry,
                    _missionTarget.TargetApoapsisMeters);

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
                _missionTarget.TargetApoapsisMeters)
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
