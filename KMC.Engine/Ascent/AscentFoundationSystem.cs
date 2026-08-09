using System;
using System.Diagnostics;
using KMC.Engine.Models;
using KMC.Shared;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Stateful Engine-owned ascent foundation.
    ///
    /// Build 9.0 runs this component directly from EngineeringEngine so the
    /// new history can be validated in parallel with the legacy ASCENT page
    /// before any renderer ownership is changed.
    /// </summary>
    internal sealed class AscentFoundationSystem
    {
        private const double DefaultTargetApoapsisMeters =
            80000.0;

        private readonly AscentHistoryTracker _history =
            new AscentHistoryTracker();

        private readonly AscentProfilePlanner _profilePlanner =
            new AscentProfilePlanner();

        private readonly AscentBurnoutPredictor _burnoutPredictor =
            new AscentBurnoutPredictor();

        private readonly PoweredAscentGuidanceComputer _poweredGuidanceComputer =
            new PoweredAscentGuidanceComputer();

        private readonly AscentPhaseAnalyzer _phaseAnalyzer =
            new AscentPhaseAnalyzer();

        private readonly AscentFlightDirector _flightDirector =
            new AscentFlightDirector();

        private int _initialStage =
            -1;

        private AscentModel _latest =
            new AscentModel();

        private DateTime _lastDiagnosticUtc =
            DateTime.MinValue;

        public void Update(
            TelemetryPacket packet,
            DateTime receivedUtc,
            PropulsionModel propulsion)
        {
            if (packet == null)
            {
                _latest =
                    new AscentModel();

                return;
            }

            bool reset =
                _history.Update(
                    packet);

            if (reset)
            {
                _profilePlanner.Reset();

                _burnoutPredictor.Reset();

                _poweredGuidanceComputer.Reset();

                _phaseAnalyzer.Reset();

                _flightDirector.Reset();

                _initialStage =
                    -1;
            }

            bool capturedNow =
                _profilePlanner.CaptureLaunchPlan(
                    packet.ThrustToWeightRatio);

            if (capturedNow)
            {
                _initialStage =
                    packet.CurrentStage;
            }

            AscentTelemetryState current =
                CreateCurrentState(
                    packet);

            AscentModel model =
                new AscentModel
                {
                    Available =
                        true,

                    ReceivedUtc =
                        receivedUtc,

                    Current =
                        current,

                    History =
                        _history.CreateSnapshot(
                            reset)
                };

            model.Profile =
                _profilePlanner.CreateModel(
                    model.History.DownrangeMeters,
                    current.AltitudeMeters,
                    current.PitchDegrees,
                    current.ThrustToWeightRatio,
                    DefaultTargetApoapsisMeters,
                    _initialStage,
                    capturedNow);

            model.Prediction =
                _burnoutPredictor.Calculate(
                    current,
                    model.History.Samples,
                    DefaultTargetApoapsisMeters);

            model.PoweredGuidance =
                _poweredGuidanceComputer.Calculate(
                    current,
                    propulsion,
                    model.Profile.TargetPitchDegrees,
                    DefaultTargetApoapsisMeters);

            model.Phase =
                _phaseAnalyzer.Update(
                    current,
                    DefaultTargetApoapsisMeters);

            model.FlightDirector =
                _flightDirector.Calculate(
                    current,
                    model.Profile,
                    model.PoweredGuidance,
                    model.Phase,
                    DefaultTargetApoapsisMeters);

            double vertical =
                packet.VerticalSpeed;

            double horizontal =
                packet.HorizontalSpeed;

            if (IsFinite(vertical) &&
                IsFinite(horizontal))
            {
                model.FlightPathAngleAvailable =
                    true;

                model.FlightPathAngleDegrees =
                    Math.Atan2(
                        vertical,
                        Math.Max(
                            0.000001,
                            horizontal)) *
                    180.0 /
                    Math.PI;
            }

            _latest =
                model;

            WriteDiagnosticIfDue(
                receivedUtc,
                model);
        }

        public AscentModel GetLatest()
        {
            return
                Clone(
                    _latest);
        }

        private static AscentTelemetryState CreateCurrentState(
            TelemetryPacket packet)
        {
            return
                new AscentTelemetryState
                {
                    Available =
                        true,

                    VesselName =
                        packet.VesselName ??
                        string.Empty,

                    BodyName =
                        packet.BodyName ??
                        string.Empty,

                    MissionTimeSeconds =
                        packet.MissionTime,

                    CurrentStage =
                        packet.CurrentStage,

                    AltitudeMeters =
                        packet.Altitude,

                    RadarAltitudeMeters =
                        packet.RadarAltitude,

                    VerticalSpeedMetersPerSecond =
                        packet.VerticalSpeed,

                    HorizontalSpeedMetersPerSecond =
                        packet.HorizontalSpeed,

                    OrbitalSpeedMetersPerSecond =
                        packet.OrbitalSpeed,

                    PitchDegrees =
                        packet.Pitch,

                    HeadingDegrees =
                        packet.Heading,

                    RollDegrees =
                        packet.Roll,

                    DynamicPressureKpa =
                        packet.DynamicPressureKpa,

                    StaticPressureKpa =
                        packet.StaticPressureKpa,

                    Mach =
                        packet.Mach,

                    GForce =
                        packet.GForce,

                    ApoapsisMeters =
                        packet.Apoapsis,

                    PeriapsisMeters =
                        packet.Periapsis,

                    TimeToApoapsisSeconds =
                        packet.TimeToApoapsis,

                    VesselMassTonnes =
                        packet.VesselMass,

                    CurrentThrustKilonewtons =
                        packet.CurrentThrust,

                    MaximumThrustKilonewtons =
                        packet.MaximumThrust,

                    ThrustToWeightRatio =
                        packet.ThrustToWeightRatio,

                    ThrottleCommand =
                        packet.Throttle,

                    AverageSpecificImpulseSeconds =
                        packet.AverageSpecificImpulse,

                    StageLiquidFuelAmount =
                        packet.StageLiquidFuelAmount,

                    StageLiquidFuelCapacity =
                        packet.StageLiquidFuelCapacity,

                    StageOxidizerAmount =
                        packet.StageOxidizerAmount,

                    StageOxidizerCapacity =
                        packet.StageOxidizerCapacity
                };
        }

        private void WriteDiagnosticIfDue(
            DateTime receivedUtc,
            AscentModel model)
        {
            DateTime utc =
                receivedUtc.Kind ==
                    DateTimeKind.Utc
                        ? receivedUtc
                        : receivedUtc
                            .ToUniversalTime();

            if (_lastDiagnosticUtc !=
                    DateTime.MinValue &&
                (utc -
                 _lastDiagnosticUtc)
                    .TotalSeconds <
                1.0)
            {
                return;
            }

            _lastDiagnosticUtc =
                utc;

            Debug.WriteLine(
                "KMC.Engine ASCENT FOUNDATION" +
                " | Vessel=" +
                model.Current.VesselName +
                " | MET=" +
                model.Current.MissionTimeSeconds
                    .ToString("0.0") +
                " | Stage=" +
                model.Current.CurrentStage +
                " | Alt=" +
                model.Current.AltitudeMeters
                    .ToString("0.0") +
                "m | Downrange=" +
                model.History.DownrangeMeters
                    .ToString("0.0") +
                "m | Samples=" +
                model.History.SampleCount +
                " | VSpeed=" +
                model.Current.VerticalSpeedMetersPerSecond
                    .ToString("0.0") +
                "m/s | HSpeed=" +
                model.Current.HorizontalSpeedMetersPerSecond
                    .ToString("0.0") +
                "m/s | Pitch=" +
                model.Current.PitchDegrees
                    .ToString("0.0") +
                "deg | Heading=" +
                model.Current.HeadingDegrees
                    .ToString("0.0") +
                "deg | Roll=" +
                model.Current.RollDegrees
                    .ToString("0.0") +
                "deg | FPA=" +
                (model.FlightPathAngleAvailable
                    ? model.FlightPathAngleDegrees
                        .ToString("+0.0;-0.0;0.0") +
                      "deg"
                    : "UNKNOWN") +
                " | Q=" +
                model.Current.DynamicPressureKpa
                    .ToString("0.00") +
                "kPa | Ap=" +
                model.Current.ApoapsisMeters
                    .ToString("0.0") +
                "m | Reset=" +
                model.History.MissionResetDetected);

            AscentProfileModel profile =
                model.Profile;

            Debug.WriteLine(
                "KMC.Engine ASCENT PROFILE" +
                " | TargetAp=" +
                profile.TargetApoapsisMeters
                    .ToString("0.0") +
                "m | PlanCaptured=" +
                profile.LaunchPlanCaptured +
                " | CapturedNow=" +
                profile.CaptureOccurredThisUpdate +
                " | InitialStage=" +
                profile.InitialStage +
                " | PlanTWR=" +
                (profile.PlanningThrustToWeightRatioKnown
                    ? profile.PlanningThrustToWeightRatio
                        .ToString("0.000")
                    : "UNKNOWN") +
                " | LiveTWR=" +
                (IsFinite(
                    profile.LiveThrustToWeightRatio)
                    ? profile.LiveThrustToWeightRatio
                        .ToString("0.000")
                    : "UNKNOWN") +
                " | ScaleSource=" +
                profile.ScaleSource +
                " | Scale=" +
                profile.ProfileScaleMeters
                    .ToString("0.0") +
                "m | Downrange=" +
                profile.DownrangeMeters
                    .ToString("0.0") +
                "m | TargetAlt=" +
                profile.TargetAltitudeMeters
                    .ToString("0.0") +
                "m | ActualAlt=" +
                profile.ActualAltitudeMeters
                    .ToString("0.0") +
                "m | AltError=" +
                profile.AltitudeErrorMeters
                    .ToString("+0.0;-0.0;0.0") +
                "m | TargetPitch=" +
                profile.TargetPitchDegrees
                    .ToString("0.0") +
                "deg | ActualPitch=" +
                profile.ActualPitchDegrees
                    .ToString("0.0") +
                "deg | PitchError=" +
                profile.PitchErrorDegrees
                    .ToString("+0.0;-0.0;0.0") +
                "deg | Reset=" +
                model.History.MissionResetDetected);

            AscentPredictionModel prediction =
                model.Prediction;

            Debug.WriteLine(
                "KMC.Engine ASCENT PREDICTION" +
                " | Stage=" +
                prediction.PredictionStage +
                " | StageAge=" +
                prediction.StageAgeSeconds
                    .ToString("0.0") +
                "s | Available=" +
                prediction.Available +
                " | Status=" +
                prediction.Status +
                " | Evidence=" +
                prediction.FuelEvidence +
                " | WindowSamples=" +
                prediction.WindowSampleCount +
                " | Window=" +
                prediction.WindowDurationSeconds
                    .ToString("0.0") +
                "s | LFRate=" +
                prediction.LiquidFuelConsumptionRatePerSecond
                    .ToString("0.000") +
                "/s | OXRate=" +
                prediction.OxidizerConsumptionRatePerSecond
                    .ToString("0.000") +
                "/s | BurnRemain=" +
                (prediction.Available
                    ? prediction.TimeRemainingSeconds
                        .ToString("0.0") + "s"
                    : "--") +
                " | BurnoutV=" +
                (prediction.Available
                    ? prediction.BurnoutVelocityMetersPerSecond
                        .ToString("0.0") + "m/s"
                    : "--") +
                " | PredAp=" +
                (prediction.Available
                    ? prediction.PredictedApoapsisMeters
                        .ToString("0.0") + "m"
                    : "--") +
                " | TargetAp=" +
                prediction.TargetApoapsisMeters
                    .ToString("0.0") +
                "m | TargetError=" +
                (prediction.Available
                    ? prediction.TargetErrorMeters
                        .ToString("+0.0;-0.0;0.0") + "m"
                    : "--") +
                " | Confidence=" +
                (prediction.Available
                    ? prediction.ConfidencePercent
                        .ToString("0.0") + "%"
                    : "--") +
                " | Reset=" +
                model.History.MissionResetDetected);

            PoweredAscentModel powered =
                model.PoweredGuidance;

            Debug.WriteLine(
                "KMC.Engine ASCENT POWERED" +
                " | Available=" +
                powered.Available +
                " | Mode=" +
                powered.Mode +
                " | Reason=" +
                (string.IsNullOrEmpty(
                    powered.InactiveReason)
                    ? "--"
                    : powered.InactiveReason) +
                " | ThrustEvidence=" +
                powered.ThrustEvidence +
                " | PropFresh=" +
                powered.PropulsionTelemetryFresh +
                " | PropCoverage=" +
                powered.PropulsionCoverageComplete +
                " | CurrentThrust=" +
                (powered.CurrentThrustKnown
                    ? powered.CurrentThrustKilonewtons
                        .ToString("0.000") + "kN"
                    : "UNKNOWN") +
                " | AvailableThrust=" +
                (powered.AvailableThrustKnown
                    ? powered.AvailableThrustKilonewtons
                        .ToString("0.000") + "kN"
                    : "UNKNOWN") +
                " | Throttle=" +
                powered.ThrottleCommand
                    .ToString("0.000") +
                " | RefPitch=" +
                powered.ReferencePitchDegrees
                    .ToString("0.0") +
                "deg | RecPitch=" +
                (powered.Available
                    ? powered.RecommendedPitchDegrees
                        .ToString("0.0") + "deg"
                    : "--") +
                " | PitchError=" +
                (powered.Available
                    ? powered.PitchErrorDegrees
                        .ToString("+0.0;-0.0;0.0") + "deg"
                    : "--") +
                " | PredAp=" +
                (powered.Available
                    ? powered.PredictedApoapsisMeters
                        .ToString("0.0") + "m"
                    : "--") +
                " | PredPe=" +
                (powered.Available
                    ? powered.PredictedPeriapsisMeters
                        .ToString("0.0") + "m"
                    : "--") +
                " | OrbitError=" +
                (powered.Available
                    ? powered.OrbitErrorMeters
                        .ToString("+0.0;-0.0;0.0") + "m"
                    : "--") +
                " | Powered=" +
                (powered.Available
                    ? powered.PoweredFlightSeconds
                        .ToString("0.0") + "s"
                    : "--") +
                " | Coast=" +
                (powered.Available
                    ? powered.CoastFlightSeconds
                        .ToString("0.0") + "s"
                    : "--") +
                " | Convergence=" +
                (powered.PredictionConvergenceKnown
                    ? powered.PredictionConvergenceMeters
                        .ToString("0.0") + "m"
                    : "UNKNOWN") +
                " | Confidence=" +
                (powered.Available
                    ? powered.ConfidencePercent
                        .ToString("0.0") + "%"
                    : "--") +
                " | TargetCutoff=" +
                powered.TargetCutoffReached +
                " | Reset=" +
                model.History.MissionResetDetected);

            AscentPhaseModel phase =
                model.Phase;

            Debug.WriteLine(
                "KMC.Engine ASCENT PHASE" +
                " | Phase=" +
                phase.PhaseName +
                " | MissionStarted=" +
                phase.MissionStarted +
                " | MecoLatched=" +
                phase.MecoLatched +
                " | ApRate=" +
                (phase.Cutoff.ApoapsisRiseRateAvailable
                    ? phase.Cutoff.ApoapsisRiseRateMetersPerSecond
                        .ToString("0.0") + "m/s"
                    : "UNKNOWN") +
                " | MecoEstimate=" +
                (phase.Cutoff.EstimatedMecoAvailable
                    ? phase.Cutoff.EstimatedMecoSeconds
                        .ToString("0.0") + "s"
                    : "UNKNOWN") +
                " | Countdown=" +
                (phase.MecoCountdownSeconds > 0
                    ? phase.MecoCountdownSeconds.ToString()
                    : "--") +
                " | CutoffThreshold=" +
                phase.Cutoff.CutoffThresholdMeters
                    .ToString("0.0") +
                "m | CutoffReached=" +
                phase.Cutoff.CutoffReached +
                " | OrbitHandoff=" +
                phase.OrbitHandoffRequired +
                " | Flash=" +
                phase.FlashAlert +
                " | Reset=" +
                model.History.MissionResetDetected);

            AscentFlightDirectorModel director =
                model.FlightDirector;

            Debug.WriteLine(
                "KMC.Engine ASCENT FLIGHT DIRECTOR" +
                " | Phase=" +
                director.FlightPhase +
                " | Available=" +
                director.Available +
                " | NominalPitch=" +
                director.NominalPitchDegrees
                    .ToString("0.0") +
                "deg | RecPitch=" +
                director.RecommendedPitchDegrees
                    .ToString("0.0") +
                "deg | PitchCorrection=" +
                director.PitchCorrectionDegrees
                    .ToString("+0.0;-0.0;0.0") +
                "deg | AltError=" +
                director.AltitudeErrorMeters
                    .ToString("+0.0;-0.0;0.0") +
                "m | ApError=" +
                director.ApoapsisErrorMeters
                    .ToString("+0.0;-0.0;0.0") +
                "m | Recovery=" +
                director.RecoveryAuthorityPercent
                    .ToString("0.0") +
                "% | Achievable=" +
                director.IsTargetAchievable +
                " | PredictiveBlend=" +
                (director.PredictiveGuidanceBlended
                    ? director.PredictiveBlendFraction
                        .ToString("0.00")
                    : "--") +
                " | Command=" +
                director.Command +
                " | Throttle=" +
                director.ThrottleCommand +
                " | Status=" +
                director.Status +
                " | Next=" +
                director.NextEvent +
                " | CutoffRequired=" +
                director.CutoffRequired +
                " | CoastLockout=" +
                director.CoastLockoutActive +
                " | OrbitHandoff=" +
                director.OrbitHandoffRequired +
                " | Reset=" +
                model.History.MissionResetDetected);
        }

        private static AscentModel Clone(
            AscentModel source)
        {
            AscentModel clone =
                new AscentModel();

            if (source == null ||
                !source.Available)
            {
                return clone;
            }

            clone.Available =
                source.Available;

            clone.ReceivedUtc =
                source.ReceivedUtc;

            clone.FlightPathAngleAvailable =
                source.FlightPathAngleAvailable;

            clone.FlightPathAngleDegrees =
                source.FlightPathAngleDegrees;

            clone.Current =
                new AscentTelemetryState
                {
                    Available =
                        source.Current.Available,

                    VesselName =
                        source.Current.VesselName,

                    BodyName =
                        source.Current.BodyName,

                    MissionTimeSeconds =
                        source.Current.MissionTimeSeconds,

                    CurrentStage =
                        source.Current.CurrentStage,

                    AltitudeMeters =
                        source.Current.AltitudeMeters,

                    RadarAltitudeMeters =
                        source.Current.RadarAltitudeMeters,

                    VerticalSpeedMetersPerSecond =
                        source.Current.VerticalSpeedMetersPerSecond,

                    HorizontalSpeedMetersPerSecond =
                        source.Current.HorizontalSpeedMetersPerSecond,

                    OrbitalSpeedMetersPerSecond =
                        source.Current.OrbitalSpeedMetersPerSecond,

                    PitchDegrees =
                        source.Current.PitchDegrees,

                    HeadingDegrees =
                        source.Current.HeadingDegrees,

                    RollDegrees =
                        source.Current.RollDegrees,

                    DynamicPressureKpa =
                        source.Current.DynamicPressureKpa,

                    StaticPressureKpa =
                        source.Current.StaticPressureKpa,

                    Mach =
                        source.Current.Mach,

                    GForce =
                        source.Current.GForce,

                    ApoapsisMeters =
                        source.Current.ApoapsisMeters,

                    PeriapsisMeters =
                        source.Current.PeriapsisMeters,

                    TimeToApoapsisSeconds =
                        source.Current.TimeToApoapsisSeconds,

                    VesselMassTonnes =
                        source.Current.VesselMassTonnes,

                    CurrentThrustKilonewtons =
                        source.Current.CurrentThrustKilonewtons,

                    MaximumThrustKilonewtons =
                        source.Current.MaximumThrustKilonewtons,

                    ThrustToWeightRatio =
                        source.Current.ThrustToWeightRatio,

                    ThrottleCommand =
                        source.Current.ThrottleCommand,

                    AverageSpecificImpulseSeconds =
                        source.Current.AverageSpecificImpulseSeconds,

                    StageLiquidFuelAmount =
                        source.Current.StageLiquidFuelAmount,

                    StageLiquidFuelCapacity =
                        source.Current.StageLiquidFuelCapacity,

                    StageOxidizerAmount =
                        source.Current.StageOxidizerAmount,

                    StageOxidizerCapacity =
                        source.Current.StageOxidizerCapacity
                };

            AscentHistoryModel history =
                new AscentHistoryModel
                {
                    Available =
                        source.History.Available,

                    TrackedVesselName =
                        source.History.TrackedVesselName,

                    DownrangeMeters =
                        source.History.DownrangeMeters,

                    MissionResetDetected =
                        source.History.MissionResetDetected,

                    MissionResetCount =
                        source.History.MissionResetCount
                };

            for (int index = 0;
                 index < source.History.Samples.Count;
                 index++)
            {
                AscentHistorySample sample =
                    source.History.Samples[index];

                history.Samples.Add(
                    new AscentHistorySample
                    {
                        MissionTimeSeconds =
                            sample.MissionTimeSeconds,

                        StageNumber =
                            sample.StageNumber,

                        DownrangeMeters =
                            sample.DownrangeMeters,

                        AltitudeMeters =
                            sample.AltitudeMeters,

                        ApoapsisMeters =
                            sample.ApoapsisMeters,

                        PitchDegrees =
                            sample.PitchDegrees,

                        DynamicPressureKpa =
                            sample.DynamicPressureKpa,

                        VerticalSpeedMetersPerSecond =
                            sample.VerticalSpeedMetersPerSecond,

                        HorizontalSpeedMetersPerSecond =
                            sample.HorizontalSpeedMetersPerSecond,

                        OrbitalSpeedMetersPerSecond =
                            sample.OrbitalSpeedMetersPerSecond,

                        VesselMassTonnes =
                            sample.VesselMassTonnes,

                        CurrentThrustKilonewtons =
                            sample.CurrentThrustKilonewtons,

                        AverageSpecificImpulseSeconds =
                            sample.AverageSpecificImpulseSeconds,

                        StageLiquidFuelAmount =
                            sample.StageLiquidFuelAmount,

                        StageOxidizerAmount =
                            sample.StageOxidizerAmount
                    });
            }

            clone.History =
                history;

            clone.Profile =
                new AscentProfileModel
                {
                    Available =
                        source.Profile.Available,

                    TargetApoapsisMeters =
                        source.Profile.TargetApoapsisMeters,

                    LaunchPlanCaptured =
                        source.Profile.LaunchPlanCaptured,

                    CaptureOccurredThisUpdate =
                        source.Profile.CaptureOccurredThisUpdate,

                    InitialStage =
                        source.Profile.InitialStage,

                    PlanningThrustToWeightRatioKnown =
                        source.Profile.PlanningThrustToWeightRatioKnown,

                    PlanningThrustToWeightRatio =
                        source.Profile.PlanningThrustToWeightRatio,

                    LiveThrustToWeightRatio =
                        source.Profile.LiveThrustToWeightRatio,

                    ScaleSource =
                        source.Profile.ScaleSource,

                    ProfileScaleMeters =
                        source.Profile.ProfileScaleMeters,

                    DownrangeMeters =
                        source.Profile.DownrangeMeters,

                    TargetAltitudeMeters =
                        source.Profile.TargetAltitudeMeters,

                    ActualAltitudeMeters =
                        source.Profile.ActualAltitudeMeters,

                    AltitudeErrorMeters =
                        source.Profile.AltitudeErrorMeters,

                    TargetPitchDegrees =
                        source.Profile.TargetPitchDegrees,

                    ActualPitchDegrees =
                        source.Profile.ActualPitchDegrees,

                    PitchErrorDegrees =
                        source.Profile.PitchErrorDegrees
                };

            clone.Prediction =
                new AscentPredictionModel
                {
                    Available =
                        source.Prediction.Available,

                    HasFuelTrend =
                        source.Prediction.HasFuelTrend,

                    FuelEvidence =
                        source.Prediction.FuelEvidence,

                    PredictionStage =
                        source.Prediction.PredictionStage,

                    StageAgeSeconds =
                        source.Prediction.StageAgeSeconds,

                    WindowSampleCount =
                        source.Prediction.WindowSampleCount,

                    WindowDurationSeconds =
                        source.Prediction.WindowDurationSeconds,

                    LiquidFuelConsumptionRatePerSecond =
                        source.Prediction.LiquidFuelConsumptionRatePerSecond,

                    OxidizerConsumptionRatePerSecond =
                        source.Prediction.OxidizerConsumptionRatePerSecond,

                    TimeRemainingSeconds =
                        source.Prediction.TimeRemainingSeconds,

                    BurnoutVelocityMetersPerSecond =
                        source.Prediction.BurnoutVelocityMetersPerSecond,

                    PredictedApoapsisMeters =
                        source.Prediction.PredictedApoapsisMeters,

                    TargetApoapsisMeters =
                        source.Prediction.TargetApoapsisMeters,

                    TargetErrorMeters =
                        source.Prediction.TargetErrorMeters,

                    ConfidencePercent =
                        source.Prediction.ConfidencePercent,

                    Status =
                        source.Prediction.Status
                };

            clone.PoweredGuidance =
                new PoweredAscentModel
                {
                    Available =
                        source.PoweredGuidance.Available,

                    Mode =
                        source.PoweredGuidance.Mode,

                    InactiveReason =
                        source.PoweredGuidance.InactiveReason,

                    ReferencePitchDegrees =
                        source.PoweredGuidance.ReferencePitchDegrees,

                    RecommendedPitchDegrees =
                        source.PoweredGuidance.RecommendedPitchDegrees,

                    PitchErrorDegrees =
                        source.PoweredGuidance.PitchErrorDegrees,

                    PredictedApoapsisMeters =
                        source.PoweredGuidance.PredictedApoapsisMeters,

                    PredictedPeriapsisMeters =
                        source.PoweredGuidance.PredictedPeriapsisMeters,

                    OrbitErrorMeters =
                        source.PoweredGuidance.OrbitErrorMeters,

                    ConfidencePercent =
                        source.PoweredGuidance.ConfidencePercent,

                    PoweredFlightSeconds =
                        source.PoweredGuidance.PoweredFlightSeconds,

                    CoastFlightSeconds =
                        source.PoweredGuidance.CoastFlightSeconds,

                    PredictionConvergenceKnown =
                        source.PoweredGuidance.PredictionConvergenceKnown,

                    PredictionConvergenceMeters =
                        source.PoweredGuidance.PredictionConvergenceMeters,

                    TargetCutoffReached =
                        source.PoweredGuidance.TargetCutoffReached,

                    ThrustEvidence =
                        source.PoweredGuidance.ThrustEvidence,

                    PropulsionTelemetryFresh =
                        source.PoweredGuidance.PropulsionTelemetryFresh,

                    PropulsionCoverageComplete =
                        source.PoweredGuidance.PropulsionCoverageComplete,

                    CurrentThrustKnown =
                        source.PoweredGuidance.CurrentThrustKnown,

                    CurrentThrustKilonewtons =
                        source.PoweredGuidance.CurrentThrustKilonewtons,

                    AvailableThrustKnown =
                        source.PoweredGuidance.AvailableThrustKnown,

                    AvailableThrustKilonewtons =
                        source.PoweredGuidance.AvailableThrustKilonewtons,

                    ThrottleCommand =
                        source.PoweredGuidance.ThrottleCommand,

                    VesselMassTonnes =
                        source.PoweredGuidance.VesselMassTonnes,

                    SpecificImpulseSeconds =
                        source.PoweredGuidance.SpecificImpulseSeconds
                };

            clone.Phase =
                new AscentPhaseModel
                {
                    Available =
                        source.Phase.Available,

                    Phase =
                        source.Phase.Phase,

                    PhaseName =
                        source.Phase.PhaseName,

                    MissionStarted =
                        source.Phase.MissionStarted,

                    MecoLatched =
                        source.Phase.MecoLatched,

                    MecoCountdownSeconds =
                        source.Phase.MecoCountdownSeconds,

                    FlashAlert =
                        source.Phase.FlashAlert,

                    OrbitHandoffRequired =
                        source.Phase.OrbitHandoffRequired,

                    Cutoff =
                        new AscentCutoffModel
                        {
                            Available =
                                source.Phase.Cutoff.Available,

                            TargetApoapsisMeters =
                                source.Phase.Cutoff.TargetApoapsisMeters,

                            CutoffToleranceMeters =
                                source.Phase.Cutoff.CutoffToleranceMeters,

                            CutoffThresholdMeters =
                                source.Phase.Cutoff.CutoffThresholdMeters,

                            ApoapsisRiseRateAvailable =
                                source.Phase.Cutoff.ApoapsisRiseRateAvailable,

                            ApoapsisRiseRateMetersPerSecond =
                                source.Phase.Cutoff.ApoapsisRiseRateMetersPerSecond,

                            EstimatedMecoAvailable =
                                source.Phase.Cutoff.EstimatedMecoAvailable,

                            EstimatedMecoSeconds =
                                source.Phase.Cutoff.EstimatedMecoSeconds,

                            CutoffReached =
                                source.Phase.Cutoff.CutoffReached
                        }
                };

            clone.FlightDirector =
                new AscentFlightDirectorModel
                {
                    Available =
                        source.FlightDirector.Available,

                    FlightPhase =
                        source.FlightDirector.FlightPhase,

                    NominalPitchDegrees =
                        source.FlightDirector.NominalPitchDegrees,

                    RecommendedPitchDegrees =
                        source.FlightDirector.RecommendedPitchDegrees,

                    PitchCorrectionDegrees =
                        source.FlightDirector.PitchCorrectionDegrees,

                    AltitudeErrorMeters =
                        source.FlightDirector.AltitudeErrorMeters,

                    ApoapsisErrorMeters =
                        source.FlightDirector.ApoapsisErrorMeters,

                    RecoveryAuthorityPercent =
                        source.FlightDirector.RecoveryAuthorityPercent,

                    IsTargetAchievable =
                        source.FlightDirector.IsTargetAchievable,

                    ThrottleCommandPercent =
                        source.FlightDirector.ThrottleCommandPercent,

                    Command =
                        source.FlightDirector.Command,

                    ThrottleCommand =
                        source.FlightDirector.ThrottleCommand,

                    Status =
                        source.FlightDirector.Status,

                    NextEvent =
                        source.FlightDirector.NextEvent,

                    MecoCountdownSeconds =
                        source.FlightDirector.MecoCountdownSeconds,

                    CutoffRequired =
                        source.FlightDirector.CutoffRequired,

                    CoastLockoutActive =
                        source.FlightDirector.CoastLockoutActive,

                    OrbitHandoffRequired =
                        source.FlightDirector.OrbitHandoffRequired,

                    FlashAlert =
                        source.FlightDirector.FlashAlert,

                    PredictiveGuidanceBlended =
                        source.FlightDirector.PredictiveGuidanceBlended,

                    PredictiveBlendFraction =
                        source.FlightDirector.PredictiveBlendFraction
                };

            return clone;
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
