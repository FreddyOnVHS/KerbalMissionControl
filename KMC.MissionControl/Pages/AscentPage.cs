using System;
using System.Drawing;
using KMC.Engine.Analysis;
using KMC.Engine.Ascent;
using KMC.MissionControl.Engineering;
using KMC.MissionControl.Models;
using KMC.MissionControl.Rendering;
using KMC.MissionControl.Rendering.Ascent;

namespace KMC.MissionControl.Pages
{
    public sealed class AscentPage :
        IMissionPage,
        IMissionPageCanvasProvider
    {
        private const double DefaultTargetApoapsisMeters =
            80000.0;

        private readonly FlightDirectorRenderer
            _flightDirectorRenderer =
                new FlightDirectorRenderer();

        private readonly PredictionRenderer
            _predictionRenderer =
                new PredictionRenderer();

        private readonly OrbitTrendRenderer
            _orbitTrendRenderer =
                new OrbitTrendRenderer();

        private readonly NavballRenderer
            _navballRenderer =
                new NavballRenderer();

        private readonly FooterRenderer
            _footerRenderer =
                new FooterRenderer();

        private readonly AscentHeaderRenderer
            _headerRenderer =
                new AscentHeaderRenderer();

        private readonly AscentGraphRenderer
            _ascentGraphRenderer =
                new AscentGraphRenderer();

        public string Name
        {
            get
            {
                return
                    "ASCENT GUIDANCE";
            }
        }

        public Size PreferredVirtualCanvasSize
        {
            get
            {
                /*
                 * Match POWER: Size.Empty selects MissionDisplay's responsive
                 * full-viewport canvas instead of fitting ASCENT into a fixed
                 * logical aspect ratio.
                 */
                return Size.Empty;
            }
        }

        public MissionPageContentProfile ContentProfile
        {
            get
            {
                return
                    MissionPageContentProfile
                        .DenseEngineering;
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

            AscentModel ascent =
                GetLatestAscent();

            _headerRenderer.Draw(
                context);

            AscentLayout layout =
                AscentLayout.Create(
                    context);

            DrawAscentGraph(
                context,
                layout.Graph,
                ascent);

            DrawNavball(
                context,
                layout.Navball,
                telemetry,
                ascent);

            DrawOrbitInset(
                context,
                layout.OrbitTrend,
                telemetry);

            DrawGuidancePanel(
                context,
                layout.FlightDirector,
                ascent);

            DrawPredictivePanel(
                context,
                layout.Prediction,
                ascent);

            DrawFooter(
                context,
                layout.Footer,
                telemetry,
                ascent);
        }

        private static AscentModel GetLatestAscent()
        {
            AnalysisPipelineResult result;

            if (!EngineeringSnapshotStore
                    .TryGetLatest(
                        out result) ||
                result == null ||
                result.Snapshot == null ||
                result.Snapshot.Ascent == null ||
                !result.Snapshot.Ascent.Available)
            {
                return null;
            }

            return
                result.Snapshot.Ascent;
        }

        private void DrawAscentGraph(
            MissionRenderContext context,
            Rectangle bounds,
            AscentModel ascent)
        {
            AscentGraphRenderModel model =
                new AscentGraphRenderModel();

            if (ascent == null ||
                ascent.History == null ||
                ascent.Profile == null)
            {
                model.MaximumDownrangeMeters =
                    120000.0;

                model.MaximumAltitudeMeters =
                    DefaultTargetApoapsisMeters *
                    1.15;

                model.TargetPoints =
                    new AscentGraphPoint[0];

                model.ActualPoints =
                    new AscentGraphPoint[0];

                _ascentGraphRenderer.Draw(
                    context,
                    bounds,
                    model);

                return;
            }

            double targetApoapsis =
                IsFinite(
                    ascent.Profile.TargetApoapsisMeters) &&
                ascent.Profile.TargetApoapsisMeters >
                    0.0
                    ? ascent.Profile.TargetApoapsisMeters
                    : DefaultTargetApoapsisMeters;

            double profileScale =
                IsFinite(
                    ascent.Profile.ProfileScaleMeters) &&
                ascent.Profile.ProfileScaleMeters >
                    1.0
                    ? ascent.Profile.ProfileScaleMeters
                    : 52000.0 /
                      Math.Sqrt(
                          1.5);

            double currentDownrange =
                Math.Max(
                    1.0,
                    ascent.History.DownrangeMeters);

            double maxDownrange =
                Math.Max(
                    120000.0,
                    Math.Max(
                        currentDownrange *
                        1.20,
                        profileScale *
                        4.5));

            double maxActualAltitude =
                0.0;

            for (int index = 0;
                 index <
                 ascent.History.Samples.Count;
                 index++)
            {
                maxActualAltitude =
                    Math.Max(
                        maxActualAltitude,
                        ascent.History
                            .Samples[index]
                            .AltitudeMeters);
            }

            model.MaximumDownrangeMeters =
                maxDownrange;

            model.MaximumAltitudeMeters =
                Math.Max(
                    targetApoapsis *
                    1.15,
                    maxActualAltitude *
                    1.10);

            const int targetPointCount =
                120;

            model.TargetPoints =
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

                double targetAltitude =
                    targetApoapsis *
                    (1.0 -
                     Math.Exp(
                         -Math.Max(
                             0.0,
                             downrange) /
                         profileScale));

                targetAltitude =
                    Math.Max(
                        0.0,
                        Math.Min(
                            targetApoapsis,
                            targetAltitude));

                model.TargetPoints[index] =
                    new AscentGraphPoint
                    {
                        DownrangeMeters =
                            downrange,

                        AltitudeMeters =
                            targetAltitude
                    };
            }

            model.ActualPoints =
                new AscentGraphPoint[
                    ascent.History
                        .Samples.Count];

            for (int index = 0;
                 index <
                 ascent.History.Samples.Count;
                 index++)
            {
                AscentHistorySample sample =
                    ascent.History
                        .Samples[index];

                model.ActualPoints[index] =
                    new AscentGraphPoint
                    {
                        DownrangeMeters =
                            sample.DownrangeMeters,

                        AltitudeMeters =
                            sample.AltitudeMeters
                    };
            }

            _ascentGraphRenderer.Draw(
                context,
                bounds,
                model);
        }

        private void DrawNavball(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry,
            AscentModel ascent)
        {
            double pitch =
                telemetry.Pitch;

            double heading =
                telemetry.Heading;

            double roll =
                telemetry.Roll;

            bool flightPathAvailable =
                false;

            double flightPathAngle =
                double.NaN;

            if (ascent != null &&
                ascent.Current != null &&
                ascent.Current.Available)
            {
                pitch =
                    ascent.Current.PitchDegrees;

                heading =
                    ascent.Current.HeadingDegrees;

                roll =
                    ascent.Current.RollDegrees;

                flightPathAvailable =
                    ascent.FlightPathAngleAvailable;

                flightPathAngle =
                    ascent.FlightPathAngleDegrees;
            }
            else
            {
                double horizontal =
                    telemetry.HorizontalSpeed;

                double vertical =
                    telemetry.VerticalSpeed;

                double speed =
                    Math.Sqrt(
                        horizontal *
                        horizontal +
                        vertical *
                        vertical);

                flightPathAvailable =
                    IsFinite(
                        speed) &&
                    speed >=
                        1.0;

                if (flightPathAvailable)
                {
                    flightPathAngle =
                        Math.Atan2(
                            vertical,
                            Math.Max(
                                0.0,
                                horizontal)) *
                        180.0 /
                        Math.PI;
                }
            }

            NavballRenderModel model =
                new NavballRenderModel
                {
                    PitchDegrees =
                        pitch,

                    HeadingDegrees =
                        heading,

                    RollDegrees =
                        roll,

                    FlightPathAvailable =
                        flightPathAvailable,

                    FlightPathAngleDegrees =
                        flightPathAngle
                };

            if (ascent != null &&
                ascent.Current != null &&
                ascent.Current.Available &&
                ascent.FlightDirector != null &&
                ascent.FlightDirector.Available)
            {
                model.GuidanceAvailable =
                    true;

                model.CommandedPitchDegrees =
                    ascent.FlightDirector
                        .RecommendedPitchDegrees;

                model.PitchErrorDegrees =
                    ascent.FlightDirector
                        .RecommendedPitchDegrees -
                    ascent.Current
                        .PitchDegrees;

                model.FlightPhase =
                    ascent.FlightDirector
                        .FlightPhase;

                model.CutoffRequired =
                    ascent.FlightDirector
                        .CutoffRequired;

                model.CoastLockoutActive =
                    ascent.FlightDirector
                        .CoastLockoutActive;

                model.OrbitHandoffRequired =
                    ascent.FlightDirector
                        .OrbitHandoffRequired;

                model.FlashAlert =
                    ascent.FlightDirector
                        .FlashAlert;
            }

            _navballRenderer.Draw(
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
            AscentModel ascent)
        {
            FlightDirectorRenderModel model =
                new FlightDirectorRenderModel();

            if (ascent != null &&
                ascent.Current != null &&
                ascent.Profile != null &&
                ascent.FlightDirector != null)
            {
                AscentFlightDirectorModel source =
                    ascent.FlightDirector;

                model.Available =
                    source.Available;

                model.MissionTimeSeconds =
                    ascent.Current
                        .MissionTimeSeconds;

                model.FlightPhase =
                    source.FlightPhase;

                model.TargetApoapsisMeters =
                    ascent.Profile
                        .TargetApoapsisMeters;

                model.DownrangeMeters =
                    ascent.History != null
                        ? ascent.History
                            .DownrangeMeters
                        : 0.0;

                model.TargetAltitudeMeters =
                    ascent.Profile
                        .TargetAltitudeMeters;

                model.ActualAltitudeMeters =
                    ascent.Current
                        .AltitudeMeters;

                model.ActualPitchDegrees =
                    ascent.Current
                        .PitchDegrees;

                model.DynamicPressureKpa =
                    ascent.Current
                        .DynamicPressureKpa;

                model.NominalPitchDegrees =
                    source.NominalPitchDegrees;

                model.RecommendedPitchDegrees =
                    source.RecommendedPitchDegrees;

                model.PitchCorrectionDegrees =
                    source.PitchCorrectionDegrees;

                model.AltitudeErrorMeters =
                    source.AltitudeErrorMeters;

                model.ApoapsisErrorMeters =
                    source.ApoapsisErrorMeters;

                model.RecoveryAuthorityPercent =
                    source.RecoveryAuthorityPercent;

                model.IsTargetAchievable =
                    source.IsTargetAchievable;

                model.Command =
                    source.Command;

                model.ThrottleCommand =
                    source.ThrottleCommand;

                model.Status =
                    source.Status;

                model.NextEvent =
                    source.NextEvent;

                model.MecoCountdownSeconds =
                    source.MecoCountdownSeconds;

                model.CutoffRequired =
                    source.CutoffRequired;

                model.CoastLockoutActive =
                    source.CoastLockoutActive;

                model.OrbitHandoffRequired =
                    source.OrbitHandoffRequired;

                model.FlashAlert =
                    source.FlashAlert;

                model.PredictiveGuidanceBlended =
                    source.PredictiveGuidanceBlended;

                model.PredictiveBlendFraction =
                    source.PredictiveBlendFraction;
            }

            _flightDirectorRenderer.Draw(
                context,
                bounds,
                model);
        }

        private void DrawPredictivePanel(
            MissionRenderContext context,
            Rectangle bounds,
            AscentModel ascent)
        {
            PredictionRenderModel model =
                new PredictionRenderModel();

            if (ascent != null)
            {
                if (ascent.Prediction != null)
                {
                    AscentPredictionModel burnout =
                        ascent.Prediction;

                    model.BurnoutAvailable =
                        burnout.Available;

                    model.BurnTimeRemainingSeconds =
                        burnout.TimeRemainingSeconds;

                    model.BurnoutVelocityMetersPerSecond =
                        burnout
                            .BurnoutVelocityMetersPerSecond;

                    model.BurnoutPredictedApoapsisMeters =
                        burnout
                            .PredictedApoapsisMeters;

                    model.BurnoutTargetErrorMeters =
                        burnout.TargetErrorMeters;

                    model.BurnoutConfidencePercent =
                        burnout.ConfidencePercent;

                    model.BurnoutStatus =
                        burnout.Status;

                    model.BurnoutEvidence =
                        burnout.FuelEvidence
                            .ToString();
                }

                if (ascent.PoweredGuidance != null)
                {
                    PoweredAscentModel powered =
                        ascent.PoweredGuidance;

                    model.PoweredAvailable =
                        powered.Available;

                    model.PoweredMode =
                        powered.Mode;

                    model.PoweredInactiveReason =
                        powered.InactiveReason;

                    model.PoweredPredictedApoapsisMeters =
                        powered
                            .PredictedApoapsisMeters;

                    model.PoweredPredictedPeriapsisMeters =
                        powered
                            .PredictedPeriapsisMeters;

                    model.PoweredOrbitErrorMeters =
                        powered.OrbitErrorMeters;

                    model.PoweredRecommendedPitchDegrees =
                        powered
                            .RecommendedPitchDegrees;

                    model.PoweredConfidencePercent =
                        powered.ConfidencePercent;

                    model.PoweredFlightSeconds =
                        powered.PoweredFlightSeconds;

                    model.CoastFlightSeconds =
                        powered.CoastFlightSeconds;

                    model.ConvergenceKnown =
                        powered
                            .PredictionConvergenceKnown;

                    model.ConvergenceMeters =
                        powered
                            .PredictionConvergenceMeters;

                    model.ThrustEvidence =
                        powered.ThrustEvidence
                            .ToString();

                    model.TargetCutoffReached =
                        powered.TargetCutoffReached;
                }
            }

            _predictionRenderer.Draw(
                context,
                bounds,
                model);
        }

        private void DrawFooter(
            MissionRenderContext context,
            Rectangle bounds,
            MissionTelemetry telemetry,
            AscentModel ascent)
        {
            FooterRenderModel model =
                new FooterRenderModel();

            if (ascent != null &&
                ascent.Current != null &&
                ascent.Current.Available)
            {
                AscentTelemetryState current =
                    ascent.Current;

                model.MissionTimeSeconds =
                    current.MissionTimeSeconds;

                model.CurrentStage =
                    current.CurrentStage;

                model.AltitudeMeters =
                    current.AltitudeMeters;

                model.DownrangeMeters =
                    ascent.History != null
                        ? ascent.History
                            .DownrangeMeters
                        : 0.0;

                model.VerticalSpeedMetersPerSecond =
                    current
                        .VerticalSpeedMetersPerSecond;

                model.HorizontalSpeedMetersPerSecond =
                    current
                        .HorizontalSpeedMetersPerSecond;

                model.ThrustToWeightRatio =
                    current.ThrustToWeightRatio;

                model.GForce =
                    current.GForce;

                model.ApoapsisMeters =
                    current.ApoapsisMeters;

                model.FuelPercent =
                    CalculateStageFuelPercent(
                        current);

                model.Status =
                    ascent.Phase != null &&
                    ascent.Phase.Available
                        ? ascent.Phase
                            .PhaseName
                        : "ASCENT";
            }
            else
            {
                model.MissionTimeSeconds =
                    telemetry.MissionTime;

                model.CurrentStage =
                    telemetry.CurrentStage;

                model.AltitudeMeters =
                    telemetry.Altitude;

                model.DownrangeMeters =
                    0.0;

                model.VerticalSpeedMetersPerSecond =
                    telemetry.VerticalSpeed;

                model.HorizontalSpeedMetersPerSecond =
                    telemetry.HorizontalSpeed;

                model.ThrustToWeightRatio =
                    telemetry.ThrustToWeightRatio;

                model.GForce =
                    telemetry.GForce;

                model.ApoapsisMeters =
                    telemetry.Apoapsis;

                model.FuelPercent =
                    CalculateStageFuelPercent(
                        telemetry);

                model.Status =
                    "ENGINE WAIT";
            }

            _footerRenderer.Draw(
                context,
                bounds,
                model);
        }

        private static double CalculateStageFuelPercent(
            AscentTelemetryState telemetry)
        {
            double amount =
                Math.Max(
                    0.0,
                    telemetry
                        .StageLiquidFuelAmount) +
                Math.Max(
                    0.0,
                    telemetry
                        .StageOxidizerAmount);

            double capacity =
                Math.Max(
                    0.0,
                    telemetry
                        .StageLiquidFuelCapacity) +
                Math.Max(
                    0.0,
                    telemetry
                        .StageOxidizerCapacity);

            if (capacity <=
                0.0)
            {
                return
                    double.NaN;
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

        private static double CalculateStageFuelPercent(
            MissionTelemetry telemetry)
        {
            double amount =
                Math.Max(
                    0.0,
                    telemetry
                        .StageLiquidFuelAmount) +
                Math.Max(
                    0.0,
                    telemetry
                        .StageOxidizerAmount);

            double capacity =
                Math.Max(
                    0.0,
                    telemetry
                        .StageLiquidFuelCapacity) +
                Math.Max(
                    0.0,
                    telemetry
                        .StageOxidizerCapacity);

            if (capacity <=
                0.0)
            {
                return
                    double.NaN;
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

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
