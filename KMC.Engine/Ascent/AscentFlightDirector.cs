using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Engine-owned powered-ascent flight director.
    ///
    /// Build 9.4 preserves the ascent-side steering/throttle/recoverability
    /// behavior from MissionControl MissionPlanner while consuming Engine-owned
    /// reference profile, powered trajectory guidance, and phase state.
    ///
    /// Circularization guidance is intentionally excluded.
    /// </summary>
    internal sealed class AscentFlightDirector
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

        private double _lastMissionTime =
            double.NaN;

        private double _lastRecommendedPitch =
            double.NaN;

        public void Reset()
        {
            _lastMissionTime =
                double.NaN;

            _lastRecommendedPitch =
                double.NaN;
        }

        public AscentFlightDirectorModel Calculate(
            AscentTelemetryState telemetry,
            AscentProfileModel profile,
            PoweredAscentModel poweredGuidance,
            AscentPhaseModel phase,
            double targetApoapsisMeters)
        {
            AscentFlightDirectorModel result =
                CreateDefaultResult(
                    profile,
                    phase);

            if (telemetry == null ||
                !telemetry.Available ||
                profile == null ||
                phase == null ||
                !phase.Available)
            {
                return result;
            }

            result.Available =
                true;

            result.FlightPhase =
                phase.PhaseName;

            result.MecoCountdownSeconds =
                phase.MecoCountdownSeconds;

            result.FlashAlert =
                phase.FlashAlert;

            result.OrbitHandoffRequired =
                phase.OrbitHandoffRequired;

            /*
             * Legacy MissionPlanner sign convention:
             *
             * positive altitude error = reference altitude is ABOVE vehicle.
             * positive apoapsis error = target apoapsis is ABOVE actual.
             */
            double altitudeError =
                profile.TargetAltitudeMeters -
                telemetry.AltitudeMeters;

            double apoapsisError =
                targetApoapsisMeters -
                telemetry.ApoapsisMeters;

            result.AltitudeErrorMeters =
                altitudeError;

            result.ApoapsisErrorMeters =
                apoapsisError;

            double deltaTime =
                CalculateDeltaTime(
                    telemetry.MissionTimeSeconds);

            switch (phase.Phase)
            {
                case AscentFlightPhase.Prelaunch:
                    ConfigurePrelaunch(
                        result);
                    break;

                case AscentFlightPhase.MecoCountdown:
                    ConfigurePoweredAscent(
                        result,
                        telemetry,
                        profile.TargetPitchDegrees,
                        altitudeError,
                        apoapsisError,
                        deltaTime,
                        poweredGuidance);

                    ConfigureMecoCountdown(
                        result,
                        phase.MecoCountdownSeconds);
                    break;

                case AscentFlightPhase.Meco:
                    ConfigureAscentCutoff(
                        result,
                        telemetry,
                        poweredGuidance);
                    break;

                case AscentFlightPhase.CoastHandoff:
                    ConfigureCoastHandoff(
                        result,
                        telemetry,
                        poweredGuidance);
                    break;

                default:
                    ConfigurePoweredAscent(
                        result,
                        telemetry,
                        profile.TargetPitchDegrees,
                        altitudeError,
                        apoapsisError,
                        deltaTime,
                        poweredGuidance);
                    break;
            }

            SaveState(
                telemetry,
                result);

            return result;
        }

        private static AscentFlightDirectorModel CreateDefaultResult(
            AscentProfileModel profile,
            AscentPhaseModel phase)
        {
            double nominalPitch =
                profile != null
                    ? Clamp(
                        profile.TargetPitchDegrees,
                        MinimumPitchDegrees,
                        MaximumPitchDegrees)
                    : 90.0;

            return
                new AscentFlightDirectorModel
                {
                    NominalPitchDegrees =
                        nominalPitch,

                    RecommendedPitchDegrees =
                        nominalPitch,

                    FlightPhase =
                        phase != null
                            ? phase.PhaseName
                            : "UNKNOWN",

                    Command =
                        "HOLD ATTITUDE",

                    ThrottleCommand =
                        "THROTTLE HOLD",

                    Status =
                        "GUIDANCE WAITING",

                    NextEvent =
                        "---"
                };
        }

        private static void ConfigurePrelaunch(
            AscentFlightDirectorModel result)
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
            AscentFlightDirectorModel result,
            int countdownSeconds)
        {
            int countdown =
                Math.Max(
                    1,
                    Math.Min(
                        5,
                        countdownSeconds));

            result.MecoCountdownSeconds =
                countdown;

            result.Status =
                "PREPARE FOR MECO " +
                countdown;

            result.NextEvent =
                "MECO T-" +
                countdown;
        }

        private static void ConfigureAscentCutoff(
            AscentFlightDirectorModel result,
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance)
        {
            result.RecommendedPitchDegrees =
                Clamp(
                    telemetry.PitchDegrees,
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
                    telemetry,
                    poweredGuidance))
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
                "COAST / ORBIT HANDOFF";
        }

        private static void ConfigureCoastHandoff(
            AscentFlightDirectorModel result,
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance)
        {
            double progradePitch =
                CalculateProgradePitchDegrees(
                    telemetry);

            result.RecommendedPitchDegrees =
                progradePitch;

            result.ThrottleCommandPercent =
                0.0;

            result.ThrottleCommand =
                "THROTTLE 0%";

            result.CoastLockoutActive =
                true;

            result.OrbitHandoffRequired =
                true;

            result.IsTargetAchievable =
                true;

            if (IsProducingThrust(
                    telemetry,
                    poweredGuidance))
            {
                result.CutoffRequired =
                    true;

                result.Command =
                    "MECO - THRUST STILL ACTIVE";

                result.Status =
                    "CUTOFF REQUIRED";
            }
            else
            {
                result.Command =
                    "HOLD PROGRADE";

                result.Status =
                    "ASCENT COMPLETE";
            }

            result.NextEvent =
                "ORBIT GUIDANCE";
        }

        private void ConfigurePoweredAscent(
            AscentFlightDirectorModel result,
            AscentTelemetryState telemetry,
            double nominalPitchDegrees,
            double altitudeError,
            double apoapsisError,
            double deltaTime,
            PoweredAscentModel poweredGuidance)
        {
            double rawPitch =
                CalculateRawRecommendedPitch(
                    telemetry,
                    nominalPitchDegrees,
                    altitudeError,
                    apoapsisError);

            if (result.FlightPhase ==
                "TARGET APPROACH")
            {
                rawPitch =
                    CalculateTargetApproachPitch(
                        telemetry,
                        rawPitch,
                        apoapsisError);
            }

            if (poweredGuidance != null &&
                poweredGuidance.Available)
            {
                double blend =
                    telemetry.AltitudeMeters >= 30000.0
                        ? 0.55
                        : 0.35;

                if (result.FlightPhase ==
                    "TARGET APPROACH")
                {
                    blend =
                        0.70;
                }

                rawPitch =
                    rawPitch *
                    (1.0 - blend) +
                    poweredGuidance
                        .RecommendedPitchDegrees *
                    blend;

                result.PredictiveGuidanceBlended =
                    true;

                result.PredictiveBlendFraction =
                    blend;
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
                    telemetry.PitchDegrees,
                    recommendedPitch);

            result.RecoveryAuthorityPercent =
                CalculateRecoveryAuthority(
                    telemetry,
                    poweredGuidance,
                    apoapsisError,
                    result.PitchCorrectionDegrees);

            result.IsTargetAchievable =
                DetermineAchievability(
                    telemetry,
                    poweredGuidance,
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

            if (poweredGuidance != null &&
                poweredGuidance.Available &&
                result.IsTargetAchievable &&
                apoapsisError >
                    AscentCutoffToleranceMeters)
            {
                result.Status =
                    "PREDICTIVE GUIDANCE";
            }

            result.NextEvent =
                result.FlightPhase ==
                    "TARGET APPROACH"
                    ? "MECO AT TARGET AP"
                    : "TARGET APPROACH";
        }

        private static double CalculateRawRecommendedPitch(
            AscentTelemetryState telemetry,
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
            AscentTelemetryState telemetry,
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

            if (telemetry.VerticalSpeedMetersPerSecond >
                450.0)
            {
                result -=
                    2.0;
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

            return
                "HOLD ATTITUDE";
        }

        private static void ConfigurePoweredThrottle(
            AscentFlightDirectorModel result,
            AscentTelemetryState telemetry,
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
                return
                    "TARGET NOT RECOVERABLE";
            }

            if (apoapsisError <=
                TargetApproachBandMeters)
            {
                return
                    "TARGET APPROACH";
            }

            if (apoapsisError >
                20000.0)
            {
                return
                    "ENERGY LOW - RECOVER";
            }

            if (altitudeError >
                6000.0)
            {
                return
                    "PROFILE LOW - CLIMB";
            }

            if (altitudeError <
                -6000.0)
            {
                return
                    "PROFILE HIGH - FLATTEN";
            }

            return
                "REPLAN NOMINAL";
        }

        private static double CalculateVelocityCorrection(
            AscentTelemetryState telemetry)
        {
            double vertical =
                Math.Max(
                    0.0,
                    telemetry.VerticalSpeedMetersPerSecond);

            double horizontal =
                Math.Max(
                    0.0,
                    telemetry.HorizontalSpeedMetersPerSecond);

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
                telemetry.AltitudeMeters < 10000.0
                    ? 0.62
                    : telemetry.AltitudeMeters < 30000.0
                        ? 0.40
                        : telemetry.AltitudeMeters < 55000.0
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
            AscentTelemetryState telemetry)
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
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance,
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
                    telemetry,
                    poweredGuidance);

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
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance)
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
                return
                    IsProducingThrust(
                        telemetry,
                        poweredGuidance)
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
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance,
            double apoapsisError,
            double recoveryAuthorityPercent)
        {
            if (telemetry.ApoapsisMeters >=
                70000.0)
            {
                return true;
            }

            if (apoapsisError <= 0.0)
            {
                return true;
            }

            if (!IsProducingThrust(
                    telemetry,
                    poweredGuidance) &&
                telemetry.VerticalSpeedMetersPerSecond <=
                    0.0 &&
                apoapsisError >
                    5000.0)
            {
                return false;
            }

            return
                recoveryAuthorityPercent >=
                28.0;
        }

        private static bool IsProducingThrust(
            AscentTelemetryState telemetry,
            PoweredAscentModel poweredGuidance)
        {
            if (poweredGuidance != null &&
                poweredGuidance.CurrentThrustKnown)
            {
                return
                    poweredGuidance
                        .CurrentThrustKilonewtons >
                    0.1;
            }

            return
                telemetry != null &&
                telemetry.CurrentThrustKilonewtons >
                    0.1;
        }

        private static double CalculateProgradePitchDegrees(
            AscentTelemetryState telemetry)
        {
            double speed =
                Math.Max(
                    0.0,
                    telemetry.OrbitalSpeedMetersPerSecond);

            if (speed < 1.0)
            {
                return 0.0;
            }

            double ratio =
                Clamp(
                    telemetry.VerticalSpeedMetersPerSecond /
                    speed,
                    -1.0,
                    1.0);

            return
                Math.Asin(
                    ratio) *
                180.0 /
                Math.PI;
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
                delta <
                    MinimumPlanningDeltaSeconds ||
                delta > 2.0)
            {
                return 0.20;
            }

            return delta;
        }

        private void SaveState(
            AscentTelemetryState telemetry,
            AscentFlightDirectorModel result)
        {
            _lastMissionTime =
                telemetry.MissionTimeSeconds;

            _lastRecommendedPitch =
                result.RecommendedPitchDegrees;
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
            return
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value));
        }
    }
}
