using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Phase 6C recovery-aware ascent and circularization flight director.
    ///
    /// This planner is advisory only. It does not control the vehicle.
    ///
    /// Phase sequence:
    /// PRELAUNCH
    /// ASCENT
    /// TARGET APPROACH
    /// MECO
    /// COAST TO APOAPSIS
    /// CIRCULARIZATION READY
    /// CIRCULARIZATION BURN
    /// ORBIT ACHIEVED
    ///
    /// The first implementation uses Kerbin's radius and gravitational
    /// parameter for the circularization estimate.
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

        private const double CutoffToleranceMeters =
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

        private const double CircularizationCutoffToleranceMeters =
            250.0;

        private const double OrbitToleranceMeters =
            2000.0;

        private string _flightPhase =
            "PRELAUNCH";

        private double _lastMissionTime =
            double.NaN;

        private double _lastRecommendedPitch =
            double.NaN;

        private bool _ascentMecoLatched;

        private bool _circularizationStarted;

        private bool _orbitAchieved;

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

            CircularizationSolution circularization =
                CalculateCircularizationSolution(
                    telemetry,
                    targetApoapsisMeters);

            UpdateFlightPhase(
                telemetry,
                targetApoapsisMeters,
                circularization);

            PopulateCircularizationResult(
                result,
                circularization,
                telemetry,
                targetApoapsisMeters);

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

            if (_flightPhase == "PRELAUNCH")
            {
                ConfigurePrelaunch(
                    result);

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
                    circularization);

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
                    circularization);

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
                    targetApoapsisMeters);

                SavePlannerState(
                    telemetry,
                    result);

                return result;
            }

            if (_flightPhase == "ORBIT ACHIEVED")
            {
                ConfigureOrbitAchieved(
                    result,
                    telemetry);

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
                    "PLANNER WAITING",

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

            _ascentMecoLatched =
                false;

            _circularizationStarted =
                false;

            _orbitAchieved =
                false;
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

        private void UpdateFlightPhase(
            MissionTelemetry telemetry,
            double targetApoapsisMeters,
            CircularizationSolution solution)
        {
            if (_orbitAchieved)
            {
                _flightPhase =
                    "ORBIT ACHIEVED";

                return;
            }

            if (_circularizationStarted)
            {
                if (telemetry.Periapsis >=
                    targetApoapsisMeters -
                    CircularizationCutoffToleranceMeters)
                {
                    _orbitAchieved =
                        true;

                    _flightPhase =
                        "ORBIT ACHIEVED";
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
                bool plannedIgnitionWindow =
                    solution.IsAvailable &&
                    solution.IgnitionInSeconds <= 0.0;

                bool engineProducingThrust =
                    telemetry.CurrentThrust > 0.1 ||
                    telemetry.ProducingThrustEngineCount > 0;

                if (plannedIgnitionWindow &&
                    engineProducingThrust)
                {
                    _circularizationStarted =
                        true;

                    _flightPhase =
                        "CIRCULARIZATION BURN";

                    return;
                }

                if (solution.IsAvailable &&
                    solution.IgnitionInSeconds <=
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
                targetApoapsisMeters -
                CutoffToleranceMeters)
            {
                _ascentMecoLatched =
                    true;

                _flightPhase =
                    "MECO";

                return;
            }

            if (telemetry.Apoapsis >=
                targetApoapsisMeters -
                TargetApproachBandMeters)
            {
                _flightPhase =
                    "TARGET APPROACH";

                return;
            }

            _flightPhase =
                "ASCENT";
        }

        private static CircularizationSolution
            CalculateCircularizationSolution(
                MissionTelemetry telemetry,
                double targetOrbitMeters)
        {
            CircularizationSolution result =
                new CircularizationSolution();

            if (telemetry == null ||
                telemetry.Apoapsis <= 0.0 ||
                !IsFinite(
                    telemetry.TimeToApoapsis))
            {
                return result;
            }

            double apoapsisRadius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    telemetry.Apoapsis);

            double periapsisRadius =
                KerbinRadiusMeters +
                telemetry.Periapsis;

            /*
             * Keep the osculating orbit calculation numerically safe when
             * periapsis is still below the surface.
             */
            periapsisRadius =
                Math.Max(
                    KerbinRadiusMeters *
                    0.10,
                    periapsisRadius);

            double semiMajorAxis =
                (apoapsisRadius +
                 periapsisRadius) /
                2.0;

            if (semiMajorAxis <= 0.0)
            {
                return result;
            }

            double currentApoapsisVelocityTerm =
                KerbinGravitationalParameter *
                (2.0 /
                 apoapsisRadius -
                 1.0 /
                 semiMajorAxis);

            if (currentApoapsisVelocityTerm <= 0.0)
            {
                return result;
            }

            double currentApoapsisVelocity =
                Math.Sqrt(
                    currentApoapsisVelocityTerm);

            double targetRadius =
                KerbinRadiusMeters +
                Math.Max(
                    targetOrbitMeters,
                    telemetry.Apoapsis);

            double circularVelocity =
                Math.Sqrt(
                    KerbinGravitationalParameter /
                    targetRadius);

            double deltaV =
                Math.Max(
                    0.0,
                    circularVelocity -
                    currentApoapsisVelocity);

            double burnTime =
                EstimateBurnTime(
                    telemetry,
                    deltaV);

            if (!IsFinite(burnTime) ||
                burnTime < 0.0)
            {
                return result;
            }

            result.IsAvailable =
                true;

            result.DeltaV =
                deltaV;

            result.BurnTimeSeconds =
                burnTime;

            result.IgnitionInSeconds =
                telemetry.TimeToApoapsis -
                burnTime /
                2.0;

            return result;
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

        private static void PopulateCircularizationResult(
            MissionPlannerResult result,
            CircularizationSolution solution,
            MissionTelemetry telemetry,
            double targetOrbitMeters)
        {
            result.CircularizationAvailable =
                solution.IsAvailable;

            result.CircularizationDeltaV =
                solution.DeltaV;

            result.CircularizationBurnTimeSeconds =
                solution.BurnTimeSeconds;

            result.CircularizationIgnitionInSeconds =
                solution.IgnitionInSeconds;

            result.CircularizationPeriapsisErrorMeters =
                targetOrbitMeters -
                telemetry.Periapsis;
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

        private static void ConfigureAscentCutoff(
            MissionPlannerResult result,
            MissionTelemetry telemetry)
        {
            result.RecommendedPitchDegrees =
                Clamp(
                    telemetry.Pitch,
                    MinimumPitchDegrees,
                    MaximumPitchDegrees);

            result.PitchCorrectionDegrees =
                0.0;

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CutoffRequired =
                true;

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            if (telemetry.CurrentThrust > 0.1 ||
                telemetry.Throttle > 0.01)
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

        private static void ConfigureCoast(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            CircularizationSolution solution)
        {
            result.RecommendedPitchDegrees =
                0.0;

            result.Command =
                "POINT PROGRADE";

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            if (telemetry.CurrentThrust > 0.1)
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
                solution.IsAvailable
                    ? "IGNITION T-" +
                      FormatCountdown(
                          solution.IgnitionInSeconds)
                    : "BURN SOLUTION WAIT";
        }

        private static void ConfigureCircularizationReady(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            CircularizationSolution solution)
        {
            result.RecommendedPitchDegrees =
                0.0;

            result.Command =
                "HOLD PROGRADE";

            result.CoastLockoutActive =
                false;

            result.IsTargetAchievable =
                true;

            if (solution.IgnitionInSeconds > 0.0)
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
                        solution.IgnitionInSeconds);
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
                    "CIRC BURN " +
                    FormatCountdown(
                        solution.BurnTimeSeconds);
            }
        }

        private static void ConfigureCircularizationBurn(
            MissionPlannerResult result,
            MissionTelemetry telemetry,
            double targetOrbitMeters)
        {
            double periapsisError =
                targetOrbitMeters -
                telemetry.Periapsis;

            result.RecommendedPitchDegrees =
                0.0;

            result.Command =
                "HOLD PROGRADE";

            result.CoastLockoutActive =
                false;

            result.IsTargetAchievable =
                true;

            double throttlePercent;

            if (periapsisError <=
                CircularizationCutoffToleranceMeters)
            {
                throttlePercent =
                    0.0;
            }
            else if (periapsisError <
                3000.0)
            {
                throttlePercent =
                    25.0;
            }
            else if (periapsisError <
                10000.0)
            {
                throttlePercent =
                    55.0;
            }
            else
            {
                throttlePercent =
                    100.0;
            }

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
                    "ORBIT TARGET REACHED";
            }
            else if (telemetry.CurrentThrust <= 0.1)
            {
                result.Command =
                    "IGNITE NOW";

                result.Status =
                    "CIRC BURN REQUIRED";
            }
            else
            {
                result.Status =
                    "RAISE PERIAPSIS";
            }

            result.NextEvent =
                "PE ERR " +
                FormatSignedDistance(
                    periapsisError);
        }

        private static void ConfigureOrbitAchieved(
            MissionPlannerResult result,
            MissionTelemetry telemetry)
        {
            result.RecommendedPitchDegrees =
                0.0;

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CutoffRequired =
                telemetry.CurrentThrust > 0.1 ||
                telemetry.Throttle > 0.01;

            result.CoastLockoutActive =
                true;

            result.IsTargetAchievable =
                true;

            result.Command =
                result.CutoffRequired
                    ? "CUTOFF NOW"
                    : "HOLD PROGRADE";

            result.Status =
                result.CutoffRequired
                    ? "ORBIT CUTOFF"
                    : "ORBIT NOMINAL";

            result.NextEvent =
                "MISSION ORBIT";
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

            if (telemetry.CurrentThrust <=
                    0.1 &&
                telemetry.VerticalSpeed <=
                    0.0 &&
                apoapsisError >
                    5000.0)
            {
                return false;
            }

            return recoveryAuthorityPercent >=
                28.0;
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

        private static string FormatSignedDistance(
            double meters)
        {
            if (!IsFinite(meters))
            {
                return "---";
            }

            return
                (meters / 1000.0)
                .ToString(
                    "+0.0;-0.0;0.0") +
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

        private sealed class CircularizationSolution
        {
            public bool IsAvailable { get; set; }

            public double DeltaV { get; set; }

            public double BurnTimeSeconds { get; set; }

            public double IgnitionInSeconds { get; set; }
        }
    }
}
