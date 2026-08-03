using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Phase 7 orbital guidance computer.
    ///
    /// The ascent and circularization sequence is still advisory only.
    /// This class does not control the vehicle.
    ///
    /// The circularization burn is no longer terminated from periapsis
    /// alone. Guidance now evaluates:
    ///
    /// - current specific orbital energy
    /// - target circular-orbit energy
    /// - remaining prograde delta-v
    /// - predicted shutdown apoapsis and periapsis
    /// - estimated engine shutdown response
    ///
    /// Kerbin constants are used in this build.
    /// </summary>
    public sealed class MissionPlanner
    {
        private const double MaximumCorrectionDegrees =
            14.0;

        private const double MinimumPitchDegrees =
            0.0;

        private const double MaximumPitchDegrees =
            90.0;

        private const double TargetApproachBandMeters =
            12000.0;

        private const double AscentCutoffToleranceMeters =
            250.0;

        private const double SteeringDeadbandDegrees =
            2.0;

        private const double MaximumPitchRateDegreesPerSecond =
            1.5;

        private const double MinimumPlanningDeltaSeconds =
            0.02;

        private const double KerbinRadiusMeters =
            600000.0;

        private const double KerbinGravitationalParameter =
            3.5316e12;

        private const double StandardGravity =
            9.80665;

        private const double CircularizationReadyLeadSeconds =
            8.0;

        /*
         * The engine is assumed to keep producing meaningful impulse
         * briefly after the pilot receives a cutoff command.
         */
        private const double ShutdownResponseSeconds =
            0.55;

        private const double OrbitNominalToleranceMeters =
            3000.0;

        private const double MaximumAllowedOrbitErrorMeters =
            7500.0;

        private const double RemainingDeltaVCutoffMetersPerSecond =
            1.25;

        private const double EnergyCutoffToleranceJoulesPerKilogram =
            3000.0;

        private const double MecoCountdownWindowSeconds =
            5.0;

        private const double MecoFlashDurationSeconds =
            1.25;

        private const double MaximumCircularizationPitchCorrectionDegrees =
            2.0;

        private string _flightPhase =
            "PRELAUNCH";

        private double _lastMissionTime =
            double.NaN;

        private double _lastRecommendedPitch =
            double.NaN;

        private double _initialCircularizationDeltaV =
            double.NaN;

        private double _lastApoapsisMeters =
            double.NaN;

        private double _lastApoapsisSampleTime =
            double.NaN;

        private double _smoothedApoapsisRateMetersPerSecond =
            double.NaN;

        private double _mecoCommandTime =
            double.NaN;

        private bool _ascentMecoLatched;

        private bool _circularizationStarted;

        private bool _orbitCutoffLatched;

        private string _lastOrbitSafetyReason =
            string.Empty;

        private bool _lastOrbitSafetyAchieved;

        private bool _lastOrbitSafetyPauseBurn;

        private bool _lastActualPeriapsisSafe;

        private bool _lastPredictedPeriapsisSafe;

        private bool _lastOrbitEnergySatisfied;

        private bool _lastOrbitDeltaVSatisfied;

        private double _lastOrbitSafetyDecisionTime =
            double.NaN;

        private readonly OrbitalGuidanceController _orbitalController =
            new OrbitalGuidanceController();

        public MissionPlannerResult CreatePlan(
            MissionTelemetry telemetry,
            double nominalAltitudeMeters,
            double nominalPitchDegrees,
            double targetApoapsisMeters)
        {
            MissionPlannerResult result =
                CreateDefaultResult(
                    nominalPitchDegrees);

            if (telemetry == null)
            {
                return result;
            }

            ResetIfMissionRestarted(
                telemetry);

            double deltaTime =
                CalculateDeltaTime(
                    telemetry.MissionTime);

            UpdateApoapsisTrend(
                telemetry);

            double estimatedMecoSeconds =
                CalculateEstimatedMecoSeconds(
                    telemetry,
                    targetApoapsisMeters);

            OrbitalGuidanceSolution guidance =
                CalculateOrbitalGuidance(
                    telemetry,
                    targetApoapsisMeters);

            UpdateFlightPhase(
                telemetry,
                targetApoapsisMeters,
                guidance,
                estimatedMecoSeconds);

            PopulateOrbitalGuidanceResult(
                result,
                guidance,
                telemetry,
                targetApoapsisMeters);

            result.OrbitSafetyReason =
                _lastOrbitSafetyReason;

            result.OrbitSafetyAchieved =
                _lastOrbitSafetyAchieved;

            result.OrbitSafetyPauseBurn =
                _lastOrbitSafetyPauseBurn;

            result.ActualPeriapsisSafe =
                _lastActualPeriapsisSafe;

            result.PredictedPeriapsisSafe =
                _lastPredictedPeriapsisSafe;

            result.OrbitEnergySatisfied =
                _lastOrbitEnergySatisfied;

            result.OrbitDeltaVSatisfied =
                _lastOrbitDeltaVSatisfied;

            result.OrbitSafetyDecisionTime =
                _lastOrbitSafetyDecisionTime;

            double altitudeError =
                nominalAltitudeMeters -
                telemetry.Altitude;

            double apoapsisError =
                targetApoapsisMeters -
                telemetry.Apoapsis;

            result.AltitudeErrorMeters =
                altitudeError;

            result.ApoapsisErrorMeters =
                apoapsisError;

            result.FlightPhase =
                _flightPhase;

            result.FlashAlert =
                string.Equals(
                    _flightPhase,
                    "MECO",
                    StringComparison.Ordinal) &&
                IsFinite(
                    _mecoCommandTime) &&
                telemetry.MissionTime -
                    _mecoCommandTime <=
                    MecoFlashDurationSeconds;

            if (_flightPhase == "PRELAUNCH")
            {
                ConfigurePrelaunch(
                    result);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "MECO COUNTDOWN")
            {
                ConfigurePoweredAscent(
                    result,
                    telemetry,
                    nominalPitchDegrees,
                    altitudeError,
                    apoapsisError,
                    deltaTime);

                ConfigureMecoCountdown(
                    result,
                    estimatedMecoSeconds);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "MECO")
            {
                ConfigureAscentCutoff(
                    result,
                    telemetry);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "COAST TO APOAPSIS")
            {
                ConfigureCoast(
                    result,
                    telemetry,
                    guidance);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "CIRCULARIZATION READY")
            {
                ConfigureCircularizationReady(
                    result,
                    telemetry,
                    guidance);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "CIRCULARIZATION BURN")
            {
                ConfigureCircularizationBurn(
                    result,
                    telemetry,
                    guidance);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "ORBIT SHAPE HOLD")
            {
                ConfigureOrbitShapeHold(
                    result,
                    telemetry,
                    guidance);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "ORBIT ACHIEVED")
            {
                ConfigureOrbitAchieved(
                    result,
                    telemetry,
                    guidance);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            ConfigurePoweredAscent(
                result,
                telemetry,
                nominalPitchDegrees,
                altitudeError,
                apoapsisError,
                deltaTime);

            SavePlannerState(
                telemetry,
                result);

            return result;
        }

        private MissionPlannerResult CreateDefaultResult(
            double nominalPitchDegrees)
        {
            double safePitch =
                Clamp(
                    nominalPitchDegrees,
                    MinimumPitchDegrees,
                    MaximumPitchDegrees);

            return new MissionPlannerResult
            {
                NominalPitchDegrees =
                    safePitch,

                RecommendedPitchDegrees =
                    safePitch,

                Command =
                    "HOLD ATTITUDE",

                ThrottleCommand =
                    "THROTTLE HOLD",

                Status =
                    "GUIDANCE WAITING",

                NextEvent =
                    "---",

                FlightPhase =
                    _flightPhase
            };
        }

        private void ResetIfMissionRestarted(
            MissionTelemetry telemetry)
        {
            bool missionTimeReset =
                IsFinite(
                    _lastMissionTime) &&
                telemetry.MissionTime + 0.5 <
                _lastMissionTime;

            if (!missionTimeReset)
            {
                return;
            }

            _flightPhase =
                "PRELAUNCH";

            _lastMissionTime =
                double.NaN;

            _lastRecommendedPitch =
                double.NaN;

            _initialCircularizationDeltaV =
                double.NaN;

            _lastApoapsisMeters =
                double.NaN;

            _lastApoapsisSampleTime =
                double.NaN;

            _smoothedApoapsisRateMetersPerSecond =
                double.NaN;

            _mecoCommandTime =
                double.NaN;

            _ascentMecoLatched =
                false;

            _circularizationStarted =
                false;

            _orbitCutoffLatched =
                false;

            _lastOrbitSafetyReason =
                string.Empty;

            _lastOrbitSafetyAchieved =
                false;

            _lastOrbitSafetyPauseBurn =
                false;

            _lastActualPeriapsisSafe =
                false;

            _lastPredictedPeriapsisSafe =
                false;

            _lastOrbitEnergySatisfied =
                false;

            _lastOrbitDeltaVSatisfied =
                false;

            _lastOrbitSafetyDecisionTime =
                double.NaN;

            _orbitalController.Reset();
        }

        private double CalculateDeltaTime(
            double missionTime)
        {
            if (!IsFinite(
                    _lastMissionTime))
            {
                return 0.20;
            }

            double delta =
                missionTime -
                _lastMissionTime;

            if (!IsFinite(delta) ||
                delta < MinimumPlanningDeltaSeconds ||
                delta > 2.0)
            {
                return 0.20;
            }

            return delta;
        }

        private void UpdateApoapsisTrend(
            MissionTelemetry telemetry)
        {
            if (!IsFinite(
                    telemetry.MissionTime) ||
                !IsFinite(
                    telemetry.Apoapsis))
            {
                return;
            }

            if (IsFinite(
                    _lastApoapsisSampleTime))
            {
                double elapsed =
                    telemetry.MissionTime -
                    _lastApoapsisSampleTime;

                if (elapsed >= 0.10 &&
                    elapsed <= 2.0)
                {
                    double instantaneousRate =
                        (telemetry.Apoapsis -
                         _lastApoapsisMeters) /
                        elapsed;

                    if (instantaneousRate > 0.0 &&
                        instantaneousRate < 50000.0)
                    {
                        if (!IsFinite(
                                _smoothedApoapsisRateMetersPerSecond))
                        {
                            _smoothedApoapsisRateMetersPerSecond =
                                instantaneousRate;
                        }
                        else
                        {
                            _smoothedApoapsisRateMetersPerSecond =
                                _smoothedApoapsisRateMetersPerSecond *
                                0.72 +
                                instantaneousRate *
                                0.28;
                        }
                    }
                }
            }

            _lastApoapsisMeters =
                telemetry.Apoapsis;

            _lastApoapsisSampleTime =
                telemetry.MissionTime;
        }

        private double CalculateEstimatedMecoSeconds(
            MissionTelemetry telemetry,
            double targetOrbitMeters)
        {
            if (!IsFinite(
                    _smoothedApoapsisRateMetersPerSecond) ||
                _smoothedApoapsisRateMetersPerSecond < 1.0)
            {
                return double.PositiveInfinity;
            }

            double remainingMeters =
                targetOrbitMeters -
                AscentCutoffToleranceMeters -
                telemetry.Apoapsis;

            if (remainingMeters <= 0.0)
            {
                return 0.0;
            }

            return
                remainingMeters /
                _smoothedApoapsisRateMetersPerSecond;
        }

        private void UpdateFlightPhase(
            MissionTelemetry telemetry,
            double targetOrbitMeters,
            OrbitalGuidanceSolution guidance,
            double estimatedMecoSeconds)
        {
            if (_orbitCutoffLatched)
            {
                _flightPhase =
                    "ORBIT ACHIEVED";

                return;
            }

            if (_circularizationStarted)
            {
                OrbitSafetyDecision decision =
                    _orbitalController.Evaluate(
                        new OrbitSafetyInput
                        {
                            TargetOrbitMeters =
                                targetOrbitMeters,

                            ActualApoapsisMeters =
                                telemetry.Apoapsis,

                            ActualPeriapsisMeters =
                                telemetry.Periapsis,

                            GuidanceAvailable =
                                guidance.IsAvailable,

                            PredictedApoapsisMeters =
                                guidance.PredictedApoapsis,

                            PredictedPeriapsisMeters =
                                guidance.PredictedPeriapsis,

                            PredictedOrbitErrorMeters =
                                guidance.PredictedOrbitError,

                            PredictedEnergyError =
                                guidance.PredictedEnergyError,

                            RemainingDeltaVMetersPerSecond =
                                guidance.RemainingDeltaV
                        });

                _lastOrbitSafetyReason =
                    decision.Reason;

                _lastOrbitSafetyAchieved =
                    decision.OrbitAchieved;

                _lastOrbitSafetyPauseBurn =
                    decision.PauseBurn;

                _lastActualPeriapsisSafe =
                    decision.ActualPeriapsisSafe;

                _lastPredictedPeriapsisSafe =
                    decision.PredictedPeriapsisSafe;

                _lastOrbitEnergySatisfied =
                    decision.EnergySatisfied;

                _lastOrbitDeltaVSatisfied =
                    decision.DeltaVSatisfied;

                _lastOrbitSafetyDecisionTime =
                    telemetry.MissionTime;

                if (decision.OrbitAchieved)
                {
                    _orbitCutoffLatched =
                        true;

                    _flightPhase =
                        "ORBIT ACHIEVED";
                }
                else if (decision.PauseBurn)
                {
                    _flightPhase =
                        "ORBIT SHAPE HOLD";
                }
                else
                {
                    _flightPhase =
                        "CIRCULARIZATION BURN";
                }

                return;
            }

            if (_ascentMecoLatched)
            {
                bool ignitionDue =
                    guidance.IsAvailable &&
                    guidance.IgnitionInSeconds <= 0.0;

                bool producingThrust =
                    IsProducingThrust(
                        telemetry);

                if (ignitionDue &&
                    producingThrust)
                {
                    _circularizationStarted =
                        true;

                    if (!IsFinite(
                            _initialCircularizationDeltaV))
                    {
                        _initialCircularizationDeltaV =
                            Math.Max(
                                guidance.RemainingDeltaV,
                                0.1);
                    }

                    _flightPhase =
                        "CIRCULARIZATION BURN";

                    return;
                }

                if (guidance.IsAvailable &&
                    guidance.IgnitionInSeconds <=
                    CircularizationReadyLeadSeconds)
                {
                    _flightPhase =
                        "CIRCULARIZATION READY";
                }
                else
                {
                    _flightPhase =
                        "COAST TO APOAPSIS";
                }

                return;
            }

            bool launchStarted =
                telemetry.MissionTime >= 1.0 ||
                telemetry.Altitude >= 15.0 ||
                telemetry.VerticalSpeed >= 3.0;

            if (!launchStarted)
            {
                _flightPhase =
                    "PRELAUNCH";

                return;
            }

            if (telemetry.Apoapsis >=
                targetOrbitMeters -
                AscentCutoffToleranceMeters)
            {
                _ascentMecoLatched =
                    true;

                _mecoCommandTime =
                    telemetry.MissionTime;

                _flightPhase =
                    "MECO";

                return;
            }

            if (IsFinite(
                    estimatedMecoSeconds) &&
                estimatedMecoSeconds <=
                    MecoCountdownWindowSeconds)
            {
                _flightPhase =
                    "MECO COUNTDOWN";

                return;
            }

            if (telemetry.Apoapsis >=
                targetOrbitMeters -
                TargetApproachBandMeters)
            {
                _flightPhase =
                    "TARGET APPROACH";

                return;
            }

            _flightPhase =
                "ASCENT";
        }

        private OrbitalGuidanceSolution
            CalculateOrbitalGuidance(
                MissionTelemetry telemetry,
                double targetOrbitMeters)
        {
            OrbitalGuidanceSolution result =
                new OrbitalGuidanceSolution();

            double radius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    telemetry.Altitude);

            double targetRadius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    targetOrbitMeters);

            if (radius <= 0.0 ||
                targetRadius <= 0.0)
            {
                return result;
            }

            double currentSpeed =
                Math.Max(
                    0.0,
                    telemetry.OrbitalSpeed);

            double radialSpeed =
                telemetry.VerticalSpeed;

            if (!IsFinite(currentSpeed) ||
                !IsFinite(radialSpeed))
            {
                return result;
            }

            radialSpeed =
                Clamp(
                    radialSpeed,
                    -currentSpeed,
                    currentSpeed);

            double tangentialSpeedSquared =
                Math.Max(
                    0.0,
                    currentSpeed *
                    currentSpeed -
                    radialSpeed *
                    radialSpeed);

            double tangentialSpeed =
                Math.Sqrt(
                    tangentialSpeedSquared);

            double currentEnergy =
                0.5 *
                currentSpeed *
                currentSpeed -
                KerbinGravitationalParameter /
                radius;

            double targetEnergy =
                -KerbinGravitationalParameter /
                (2.0 *
                 targetRadius);

            double targetSpeedSquared =
                2.0 *
                (targetEnergy +
                 KerbinGravitationalParameter /
                 radius);

            if (targetSpeedSquared < 0.0)
            {
                return result;
            }

            double targetSpeed =
                Math.Sqrt(
                    targetSpeedSquared);

            double remainingDeltaV =
                Math.Max(
                    0.0,
                    targetSpeed -
                    currentSpeed);

            double burnTime =
                EstimateBurnTime(
                    telemetry,
                    remainingDeltaV);

            if (!IsFinite(burnTime))
            {
                burnTime =
                    0.0;
            }

            double ignitionIn =
                IsFinite(
                    telemetry.TimeToApoapsis)
                    ? telemetry.TimeToApoapsis -
                      burnTime /
                      2.0
                    : double.NaN;

            double throttleFraction =
                DetermineCircularizationThrottleFraction(
                    remainingDeltaV);

            double shutdownDeltaV =
                EstimateShutdownDeltaV(
                    telemetry,
                    throttleFraction);

            OrbitalState predicted =
                PredictOrbitAfterProgradeDeltaV(
                    radius,
                    radialSpeed,
                    tangentialSpeed,
                    shutdownDeltaV);

            result.IsAvailable =
                predicted.IsValid;

            result.CurrentEnergy =
                currentEnergy;

            result.TargetEnergy =
                targetEnergy;

            result.EnergyError =
                targetEnergy -
                currentEnergy;

            result.PredictedEnergyError =
                targetEnergy -
                predicted.SpecificEnergy;

            result.RemainingDeltaV =
                remainingDeltaV;

            result.BurnTimeSeconds =
                burnTime;

            result.IgnitionInSeconds =
                ignitionIn;

            result.PredictedApoapsis =
                predicted.ApoapsisMeters;

            result.PredictedPeriapsis =
                predicted.PeriapsisMeters;

            result.PredictedOrbitError =
                Math.Max(
                    Math.Abs(
                        predicted.ApoapsisMeters -
                        targetOrbitMeters),
                    Math.Abs(
                        predicted.PeriapsisMeters -
                        targetOrbitMeters));

            if (!IsFinite(
                    _initialCircularizationDeltaV) &&
                _ascentMecoLatched &&
                remainingDeltaV > 0.1)
            {
                _initialCircularizationDeltaV =
                    remainingDeltaV;
            }

            result.InitialDeltaV =
                IsFinite(
                    _initialCircularizationDeltaV)
                    ? _initialCircularizationDeltaV
                    : remainingDeltaV;

            result.BurnCompletionPercent =
                result.InitialDeltaV > 0.1
                    ? Clamp(
                        100.0 *
                        (1.0 -
                         remainingDeltaV /
                         result.InitialDeltaV),
                        0.0,
                        100.0)
                    : 100.0;

            return result;
        }

        private static double EstimateShutdownDeltaV(
            MissionTelemetry telemetry,
            double throttleFraction)
        {
            double massKilograms =
                Math.Max(
                    0.0,
                    telemetry.VesselMass) *
                1000.0;

            double thrustKilonewtons =
                Math.Max(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust *
                    throttleFraction);

            double thrustNewtons =
                Math.Max(
                    0.0,
                    thrustKilonewtons) *
                1000.0;

            if (massKilograms <= 0.0 ||
                thrustNewtons <= 0.0)
            {
                return 0.0;
            }

            double acceleration =
                thrustNewtons /
                massKilograms;

            return
                acceleration *
                ShutdownResponseSeconds;
        }

        private static OrbitalState
            PredictOrbitAfterProgradeDeltaV(
                double radius,
                double radialSpeed,
                double tangentialSpeed,
                double progradeDeltaV)
        {
            OrbitalState result =
                new OrbitalState();

            double predictedTangentialSpeed =
                Math.Max(
                    0.0,
                    tangentialSpeed +
                    Math.Max(
                        0.0,
                        progradeDeltaV));

            double speedSquared =
                radialSpeed *
                radialSpeed +
                predictedTangentialSpeed *
                predictedTangentialSpeed;

            double energy =
                0.5 *
                speedSquared -
                KerbinGravitationalParameter /
                radius;

            if (energy >= 0.0)
            {
                return result;
            }

            double angularMomentum =
                radius *
                predictedTangentialSpeed;

            double semiMajorAxis =
                -KerbinGravitationalParameter /
                (2.0 *
                 energy);

            double eccentricityTerm =
                1.0 +
                2.0 *
                energy *
                angularMomentum *
                angularMomentum /
                (KerbinGravitationalParameter *
                 KerbinGravitationalParameter);

            double eccentricity =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        eccentricityTerm));

            double apoapsisRadius =
                semiMajorAxis *
                (1.0 +
                 eccentricity);

            double periapsisRadius =
                semiMajorAxis *
                (1.0 -
                 eccentricity);

            result.IsValid =
                IsFinite(apoapsisRadius) &&
                IsFinite(periapsisRadius);

            result.SpecificEnergy =
                energy;

            result.ApoapsisMeters =
                apoapsisRadius -
                KerbinRadiusMeters;

            result.PeriapsisMeters =
                periapsisRadius -
                KerbinRadiusMeters;

            return result;
        }

        private static double DetermineCircularizationThrottleFraction(
            double remainingDeltaV)
        {
            if (remainingDeltaV > 45.0)
            {
                return 1.00;
            }

            if (remainingDeltaV > 18.0)
            {
                return 0.60;
            }

            if (remainingDeltaV > 6.0)
            {
                return 0.30;
            }

            if (remainingDeltaV >
                RemainingDeltaVCutoffMetersPerSecond)
            {
                return 0.10;
            }

            return 0.0;
        }

        private static double EstimateBurnTime(
            MissionTelemetry telemetry,
            double deltaV)
        {
            if (deltaV <= 0.0)
            {
                return 0.0;
            }

            double massKilograms =
                Math.Max(
                    0.0,
                    telemetry.VesselMass) *
                1000.0;

            double thrustNewtons =
                Math.Max(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust) *
                1000.0;

            double specificImpulse =
                telemetry.AverageSpecificImpulse;

            if (massKilograms > 0.0 &&
                thrustNewtons > 0.0 &&
                specificImpulse > 1.0)
            {
                double exhaustVelocity =
                    specificImpulse *
                    StandardGravity;

                double finalMass =
                    massKilograms /
                    Math.Exp(
                        deltaV /
                        exhaustVelocity);

                double propellantMass =
                    Math.Max(
                        0.0,
                        massKilograms -
                        finalMass);

                double massFlow =
                    thrustNewtons /
                    exhaustVelocity;

                if (massFlow > 0.0)
                {
                    return
                        propellantMass /
                        massFlow;
                }
            }

            if (massKilograms > 0.0 &&
                thrustNewtons > 0.0)
            {
                double acceleration =
                    thrustNewtons /
                    massKilograms;

                if (acceleration > 0.0)
                {
                    return
                        deltaV /
                        acceleration;
                }
            }

            return double.NaN;
        }

        private static void PopulateOrbitalGuidanceResult(
            MissionPlannerResult result,
            OrbitalGuidanceSolution guidance,
            MissionTelemetry telemetry,
            double targetOrbitMeters)
        {
            result.CircularizationAvailable =
                guidance.IsAvailable;

            result.CircularizationDeltaV =
                guidance.RemainingDeltaV;

            result.CircularizationBurnTimeSeconds =
                guidance.BurnTimeSeconds;

            result.CircularizationIgnitionInSeconds =
                guidance.IgnitionInSeconds;

            result.CircularizationPeriapsisErrorMeters =
                targetOrbitMeters -
                telemetry.Periapsis;

            result.CurrentSpecificOrbitalEnergy =
                guidance.CurrentEnergy;

            result.TargetSpecificOrbitalEnergy =
                guidance.TargetEnergy;

            result.OrbitalEnergyError =
                guidance.EnergyError;

            result.InitialCircularizationDeltaV =
                guidance.InitialDeltaV;

            result.BurnCompletionPercent =
                guidance.BurnCompletionPercent;

            result.PredictedShutdownApoapsisMeters =
                guidance.PredictedApoapsis;

            result.PredictedShutdownPeriapsisMeters =
                guidance.PredictedPeriapsis;

            result.PredictedOrbitErrorMeters =
                guidance.PredictedOrbitError;
        }

        private static void ConfigurePrelaunch(
            MissionPlannerResult result)
        {
            result.RecommendedPitchDegrees =
                90.0;

            result.Command =
                "HOLD VERTICAL";

            result.ThrottleCommandPercent =
                100.0;

            result.ThrottleCommand =
                "THROTTLE 100%";

            result.Status =
                "AWAIT LIFTOFF";

            result.NextEvent =
                "LIFTOFF";
        }

        private static void ConfigureMecoCountdown(
            MissionPlannerResult result,
            double estimatedMecoSeconds)
        {
            int countdown =
                Math.Max(
                    1,
                    Math.Min(
                        5,
                        (int)Math.Ceiling(
                            estimatedMecoSeconds)));

            result.MecoCountdownSeconds =
                countdown;

            result.Status =
                "PREPARE FOR MECO " +
                countdown;

            result.NextEvent =
                "MECO T-" +
                countdown;

            /*
             * Keep the current target-approach steering and throttle
             * recommendations until the actual cutoff threshold is met.
             */
        }

        private static void ConfigureAscentCutoff(
            MissionPlannerResult result,
            MissionTelemetry telemetry)
        {
            result.RecommendedPitchDegrees =
                Clamp(
                    telemetry.Pitch,
                    MinimumPitchDegrees,
                    MaximumPitchDegrees);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CutoffRequired =
                true;

            result.FlashAlert =
                true;

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            if (IsProducingThrust(
                    telemetry))
            {
                result.Command =
                    "MECO NOW";

                result.Status =
                    "CUTOFF REQUIRED";
            }
            else
            {
                result.Command =
                    "POINT PROGRADE";

                result.Status =
                    "MECO CONFIRMED";
            }

            result.NextEvent =
                "COAST TO APOAPSIS";
        }

        private static double CalculateProgradePitchDegrees(
            MissionTelemetry telemetry)
        {
            double speed =
                Math.Max(
                    0.0,
                    telemetry.OrbitalSpeed);

            if (speed < 1.0)
            {
                return 0.0;
            }

            double ratio =
                Clamp(
                    telemetry.VerticalSpeed /
                    speed,
                    -1.0,
                    1.0);

            return
                Math.Asin(ratio) *
                180.0 /
                Math.PI;
        }

        private static double CalculateCircularizationPitchDegrees(
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            double progradePitch =
                CalculateProgradePitchDegrees(
                    telemetry);

            if (!guidance.IsAvailable)
            {
                return Clamp(
                    progradePitch,
                    -10.0,
                    10.0);
            }

            /*
             * A positive shape error means the predicted apoapsis is
             * higher than the predicted periapsis. Add a small downward
             * radial component so the burn raises periapsis without
             * continuing to inflate apoapsis as aggressively.
             */
            double shapeError =
                guidance.PredictedApoapsis -
                guidance.PredictedPeriapsis;

            double shapeCorrection =
                -Clamp(
                    shapeError /
                    18000.0,
                    0.0,
                    MaximumCircularizationPitchCorrectionDegrees);

            double targetOvershoot =
                Math.Max(
                    0.0,
                    guidance.PredictedApoapsis -
                    80000.0);

            double overshootCorrection =
                -Clamp(
                    targetOvershoot /
                    12000.0,
                    0.0,
                    2.5);

            return Clamp(
                progradePitch +
                shapeCorrection +
                overshootCorrection,
                -3.0,
                3.0);
        }

        private static string GetCircularizationSteeringCommand(
            MissionTelemetry telemetry,
            double recommendedPitch)
        {
            double error =
                recommendedPitch -
                telemetry.Pitch;

            if (error > 1.5)
            {
                return
                    "PITCH UP " +
                    Math.Min(
                        9.9,
                        Math.Abs(error))
                        .ToString("0.0") +
                    " DEG";
            }

            if (error < -1.5)
            {
                return
                    "PITCH DOWN " +
                    Math.Min(
                        9.9,
                        Math.Abs(error))
                        .ToString("0.0") +
                    " DEG";
            }

            return "HOLD VECTOR";
        }

        private static void ConfigureCoast(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            double coastPitch =
                CalculateProgradePitchDegrees(
                    telemetry);

            result.RecommendedPitchDegrees =
                coastPitch;

            result.CircularizationPitchDegrees =
                coastPitch;

            result.Command =
                GetCircularizationSteeringCommand(
                    telemetry,
                    coastPitch);

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            if (IsProducingThrust(
                    telemetry))
            {
                result.Command =
                    "MECO - EARLY IGNITION";

                result.Status =
                    "UNPLANNED IGNITION";
            }
            else
            {
                result.Status =
                    "PREPARE CIRCULARIZATION";
            }

            result.NextEvent =
                guidance.IsAvailable
                    ? "IGNITION T-" +
                      FormatCountdown(
                          guidance.IgnitionInSeconds)
                    : "BURN SOLUTION WAIT";
        }

        private static void ConfigureCircularizationReady(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            double readyPitch =
                CalculateCircularizationPitchDegrees(
                    telemetry,
                    guidance);

            result.RecommendedPitchDegrees =
                readyPitch;

            result.CircularizationPitchDegrees =
                readyPitch;

            result.Command =
                GetCircularizationSteeringCommand(
                    telemetry,
                    readyPitch);

            result.CoastLockoutActive =
                false;

            result.IsTargetAchievable =
                true;

            if (guidance.IgnitionInSeconds > 0.0)
            {
                result.ThrottleCommandPercent =
                    0.0;

                result.ThrottleCommand =
                    "STANDBY";

                result.Status =
                    "IGNITION APPROACHING";

                result.NextEvent =
                    "IGNITE T-" +
                    FormatCountdown(
                        guidance.IgnitionInSeconds);
            }
            else
            {
                result.ThrottleCommandPercent =
                    100.0;

                result.ThrottleCommand =
                    "THROTTLE 100%";

                result.Command =
                    "IGNITE NOW";

                result.Status =
                    "CIRCULARIZATION GO";

                result.NextEvent =
                    "DV " +
                    guidance.RemainingDeltaV
                        .ToString("0.0") +
                    " M/S";
            }
        }

        private static void ConfigureCircularizationBurn(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            double throttleFraction =
                DetermineCircularizationThrottleFraction(
                    guidance.RemainingDeltaV);

            double throttlePercent =
                throttleFraction *
                100.0;

            double burnPitch =
                CalculateCircularizationPitchDegrees(
                    telemetry,
                    guidance);

            result.RecommendedPitchDegrees =
                burnPitch;

            result.CircularizationPitchDegrees =
                burnPitch;

            result.Command =
                GetCircularizationSteeringCommand(
                    telemetry,
                    burnPitch);

            result.CoastLockoutActive =
                false;

            result.IsTargetAchievable =
                true;

            result.ThrottleCommandPercent =
                throttlePercent;

            result.ThrottleCommand =
                "THROTTLE " +
                throttlePercent
                    .ToString("0") +
                "%";

            if (throttlePercent <= 0.0)
            {
                result.Command =
                    "CUTOFF NOW";

                result.CutoffRequired =
                    true;

                result.Status =
                    "ENERGY TARGET REACHED";
            }
            else if (!IsProducingThrust(
                         telemetry))
            {
                result.Command =
                    "IGNITE NOW";

                result.Status =
                    "CIRC BURN REQUIRED";
            }
            else
            {
                result.Status =
                    "ORBITAL ENERGY BUILD";
            }

            result.NextEvent =
                "DV LEFT " +
                guidance.RemainingDeltaV
                    .ToString("0.0") +
                " M/S";
        }

        private static void ConfigureOrbitShapeHold(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            double progradePitch =
                CalculateProgradePitchDegrees(
                    telemetry);

            result.RecommendedPitchDegrees =
                progradePitch;

            result.CircularizationPitchDegrees =
                progradePitch;

            result.Command =
                IsProducingThrust(
                    telemetry)
                    ? "CUTOFF - HOLD PROGRADE"
                    : "HOLD PROGRADE";

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CutoffRequired =
                IsProducingThrust(
                    telemetry) ||
                telemetry.Throttle > 0.01;

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                false;

            result.Status =
                "ORBIT SHAPE OFF-NOMINAL";

            result.NextEvent =
                "PERIAPSIS " +
                telemetry.Periapsis
                    .ToString("0") +
                " M";
        }

        private static void ConfigureOrbitAchieved(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            OrbitalGuidanceSolution guidance)
        {
            bool thrustActive =
                IsProducingThrust(
                    telemetry) ||
                telemetry.Throttle > 0.01;

            double progradePitch =
                CalculateProgradePitchDegrees(
                    telemetry);

            /*
             * ORBIT ACHIEVED is a permanent post-cutoff lockout state.
             *
             * Guidance must never recommend another burn after the orbital
             * safety controller has accepted the orbit. Any continued or
             * resumed thrust is treated as an unplanned ignition.
             */
            result.RecommendedPitchDegrees =
                progradePitch;

            result.CircularizationPitchDegrees =
                progradePitch;

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CutoffRequired =
                thrustActive;

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            if (thrustActive)
            {
                result.Command =
                    "CUTOFF IMMEDIATELY";

                result.Status =
                    "UNPLANNED IGNITION";

                result.NextEvent =
                    "NO REIGNITION";
            }
            else
            {
                result.Command =
                    "HOLD PROGRADE";

                result.Status =
                    guidance.PredictedOrbitError <=
                        OrbitNominalToleranceMeters
                        ? "ORBIT NOMINAL"
                        : "SAFE ORBIT - OFF TARGET";

                result.NextEvent =
                    "NO REIGNITION";
            }
        }

        private void ConfigurePoweredAscent(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            double nominalPitchDegrees,
            double altitudeError,
            double apoapsisError,
            double deltaTime)
        {
            double rawPitch =
                CalculateRawRecommendedPitch(
                    telemetry,
                    nominalPitchDegrees,
                    altitudeError,
                    apoapsisError);

            if (_flightPhase ==
                "TARGET APPROACH")
            {
                rawPitch =
                    CalculateTargetApproachPitch(
                        telemetry,
                        rawPitch,
                        apoapsisError);
            }

            double recommendedPitch =
                ApplyPitchRateLimit(
                    rawPitch,
                    deltaTime);

            result.PitchCorrectionDegrees =
                recommendedPitch -
                nominalPitchDegrees;

            result.RecommendedPitchDegrees =
                recommendedPitch;

            result.Command =
                GetRateLimitedCommand(
                    telemetry.Pitch,
                    recommendedPitch);

            result.RecoveryAuthorityPercent =
                CalculateRecoveryAuthority(
                    telemetry,
                    apoapsisError,
                    result.PitchCorrectionDegrees);

            result.IsTargetAchievable =
                DetermineAchievability(
                    telemetry,
                    apoapsisError,
                    result.RecoveryAuthorityPercent);

            ConfigurePoweredThrottle(
                result,
                telemetry,
                apoapsisError);

            result.Status =
                DeterminePoweredStatus(
                    altitudeError,
                    apoapsisError,
                    result.IsTargetAchievable);

            result.NextEvent =
                _flightPhase ==
                "TARGET APPROACH"
                    ? "MECO AT TARGET AP"
                    : "TARGET APPROACH";
        }

        private static double CalculateRawRecommendedPitch(
            MissionTelemetry telemetry,
            double nominalPitchDegrees,
            double altitudeError,
            double apoapsisError)
        {
            double altitudeCorrection =
                Clamp(
                    altitudeError /
                    2200.0,
                    -6.0,
                    6.0);

            double apoapsisCorrection =
                Clamp(
                    apoapsisError /
                    9000.0,
                    -7.0,
                    7.0);

            double velocityCorrection =
                CalculateVelocityCorrection(
                    telemetry);

            double dynamicPressureLimiter =
                CalculateDynamicPressureLimiter(
                    telemetry);

            double totalCorrection =
                altitudeCorrection *
                0.42 +
                apoapsisCorrection *
                0.43 +
                velocityCorrection *
                0.15 +
                dynamicPressureLimiter;

            totalCorrection =
                Clamp(
                    totalCorrection,
                    -MaximumCorrectionDegrees,
                    MaximumCorrectionDegrees);

            return Clamp(
                nominalPitchDegrees +
                totalCorrection,
                MinimumPitchDegrees,
                MaximumPitchDegrees);
        }

        private static double CalculateTargetApproachPitch(
            MissionTelemetry telemetry,
            double rawPitchDegrees,
            double apoapsisErrorMeters)
        {
            double approachFraction =
                Clamp(
                    1.0 -
                    apoapsisErrorMeters /
                    TargetApproachBandMeters,
                    0.0,
                    1.0);

            double flattening =
                approachFraction *
                8.0;

            double result =
                rawPitchDegrees -
                flattening;

            if (telemetry.VerticalSpeed >
                450.0)
            {
                result -= 2.0;
            }

            return Clamp(
                result,
                0.0,
                45.0);
        }

        private double ApplyPitchRateLimit(
            double requestedPitchDegrees,
            double deltaTime)
        {
            if (!IsFinite(
                    _lastRecommendedPitch))
            {
                return requestedPitchDegrees;
            }

            double maximumStep =
                MaximumPitchRateDegreesPerSecond *
                Math.Max(
                    MinimumPlanningDeltaSeconds,
                    deltaTime);

            return Clamp(
                requestedPitchDegrees,
                _lastRecommendedPitch -
                maximumStep,
                _lastRecommendedPitch +
                maximumStep);
        }

        private static string GetRateLimitedCommand(
            double actualPitchDegrees,
            double recommendedPitchDegrees)
        {
            double error =
                recommendedPitchDegrees -
                actualPitchDegrees;

            if (error >
                SteeringDeadbandDegrees)
            {
                return
                    "PITCH UP SLOW " +
                    Math.Min(
                        9.9,
                        Math.Abs(error))
                        .ToString("0.0") +
                    " DEG";
            }

            if (error <
                -SteeringDeadbandDegrees)
            {
                return
                    "PITCH DOWN SLOW " +
                    Math.Min(
                        9.9,
                        Math.Abs(error))
                        .ToString("0.0") +
                    " DEG";
            }

            return "HOLD ATTITUDE";
        }

        private static void ConfigurePoweredThrottle(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            double apoapsisErrorMeters)
        {
            double throttlePercent =
                100.0;

            if (result.FlightPhase ==
                "TARGET APPROACH")
            {
                double remainingFraction =
                    Clamp(
                        apoapsisErrorMeters /
                        TargetApproachBandMeters,
                        0.0,
                        1.0);

                throttlePercent =
                    35.0 +
                    remainingFraction *
                    65.0;

                if (telemetry.DynamicPressureKpa >
                    35.0)
                {
                    throttlePercent =
                        Math.Min(
                            throttlePercent,
                            70.0);
                }
            }
            else if (telemetry.DynamicPressureKpa >
                40.0)
            {
                throttlePercent =
                    75.0;
            }

            result.ThrottleCommandPercent =
                Clamp(
                    throttlePercent,
                    0.0,
                    100.0);

            result.ThrottleCommand =
                "THROTTLE " +
                result.ThrottleCommandPercent
                    .ToString("0") +
                "%";
        }

        private static string DeterminePoweredStatus(
            double altitudeError,
            double apoapsisError,
            bool achievable)
        {
            if (!achievable)
            {
                return "TARGET NOT RECOVERABLE";
            }

            if (apoapsisError <=
                TargetApproachBandMeters)
            {
                return "TARGET APPROACH";
            }

            if (apoapsisError >
                20000.0)
            {
                return "ENERGY LOW - RECOVER";
            }

            if (altitudeError >
                6000.0)
            {
                return "PROFILE LOW - CLIMB";
            }

            if (altitudeError <
                -6000.0)
            {
                return "PROFILE HIGH - FLATTEN";
            }

            return "REPLAN NOMINAL";
        }

        private void SavePlannerState(
            MissionTelemetry telemetry,
            MissionPlannerResult result)
        {
            _lastMissionTime =
                telemetry.MissionTime;

            _lastRecommendedPitch =
                result.RecommendedPitchDegrees;
        }

        private static double CalculateVelocityCorrection(
            MissionTelemetry telemetry)
        {
            double vertical =
                Math.Max(
                    0.0,
                    telemetry.VerticalSpeed);

            double horizontal =
                Math.Max(
                    0.0,
                    telemetry.HorizontalSpeed);

            double total =
                vertical +
                horizontal;

            if (total < 1.0)
            {
                return 0.0;
            }

            double verticalFraction =
                vertical /
                total;

            double desiredVerticalFraction =
                telemetry.Altitude < 10000.0
                    ? 0.62
                    : telemetry.Altitude < 30000.0
                        ? 0.40
                        : telemetry.Altitude < 55000.0
                            ? 0.22
                            : 0.10;

            return Clamp(
                (desiredVerticalFraction -
                 verticalFraction) *
                18.0,
                -4.0,
                4.0);
        }

        private static double CalculateDynamicPressureLimiter(
            MissionTelemetry telemetry)
        {
            if (telemetry.DynamicPressureKpa <=
                25.0)
            {
                return 0.0;
            }

            return Clamp(
                (telemetry.DynamicPressureKpa -
                 25.0) /
                8.0,
                0.0,
                3.0);
        }

        private static double CalculateRecoveryAuthority(
            MissionTelemetry telemetry,
            double apoapsisError,
            double correctionDegrees)
        {
            double thrustFactor =
                Clamp(
                    telemetry.ThrustToWeightRatio /
                    1.5,
                    0.0,
                    1.0);

            double fuelFactor =
                CalculateFuelFraction(
                    telemetry);

            double correctionFactor =
                1.0 -
                Clamp(
                    Math.Abs(
                        correctionDegrees) /
                    MaximumCorrectionDegrees,
                    0.0,
                    1.0) *
                0.35;

            double errorFactor =
                1.0 -
                Clamp(
                    Math.Abs(
                        apoapsisError) /
                    100000.0,
                    0.0,
                    0.70);

            return Clamp(
                100.0 *
                (thrustFactor *
                 0.35 +
                 fuelFactor *
                 0.35 +
                 correctionFactor *
                 0.15 +
                 errorFactor *
                 0.15),
                0.0,
                100.0);
        }

        private static double CalculateFuelFraction(
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
                return telemetry.CurrentThrust > 0.1
                    ? 0.50
                    : 0.0;
            }

            return Clamp(
                amount /
                capacity,
                0.0,
                1.0);
        }

        private static bool DetermineAchievability(
            MissionTelemetry telemetry,
            double apoapsisError,
            double recoveryAuthorityPercent)
        {
            if (telemetry.Apoapsis >=
                70000.0)
            {
                return true;
            }

            if (apoapsisError <= 0.0)
            {
                return true;
            }

            if (!IsProducingThrust(
                    telemetry) &&
                telemetry.VerticalSpeed <= 0.0 &&
                apoapsisError > 5000.0)
            {
                return false;
            }

            return recoveryAuthorityPercent >=
                28.0;
        }

        private static bool IsProducingThrust(
            MissionTelemetry telemetry)
        {
            return
                telemetry.CurrentThrust > 0.1 ||
                telemetry.ProducingThrustEngineCount > 0;
        }

        private static string FormatCountdown(
            double seconds)
        {
            if (!IsFinite(seconds))
            {
                return "---";
            }

            seconds =
                Math.Max(
                    0.0,
                    seconds);

            int minutes =
                (int)(seconds / 60.0);

            int remainingSeconds =
                (int)Math.Round(
                    seconds % 60.0);

            if (remainingSeconds >= 60)
            {
                minutes++;
                remainingSeconds = 0;
            }

            return string.Format(
                "{0:00}:{1:00}",
                minutes,
                remainingSeconds);
        }

        private static string FormatOrbitPair(
            double apoapsis,
            double periapsis)
        {
            if (!IsFinite(apoapsis) ||
                !IsFinite(periapsis))
            {
                return "---";
            }

            return
                (apoapsis / 1000.0)
                    .ToString("0.0") +
                " X " +
                (periapsis / 1000.0)
                    .ToString("0.0") +
                " KM";
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
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

        private sealed class OrbitalGuidanceSolution
        {
            public bool IsAvailable { get; set; }

            public double CurrentEnergy { get; set; }

            public double TargetEnergy { get; set; }

            public double EnergyError { get; set; }

            public double PredictedEnergyError { get; set; }

            public double RemainingDeltaV { get; set; }

            public double InitialDeltaV { get; set; }

            public double BurnCompletionPercent { get; set; }

            public double BurnTimeSeconds { get; set; }

            public double IgnitionInSeconds { get; set; }

            public double PredictedApoapsis { get; set; }

            public double PredictedPeriapsis { get; set; }

            public double PredictedOrbitError { get; set; }
        }

        private sealed class OrbitalState
        {
            public bool IsValid { get; set; }

            public double SpecificEnergy { get; set; }

            public double ApoapsisMeters { get; set; }

            public double PeriapsisMeters { get; set; }
        }
    }
}
