using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Phase 6A state-based mission planner.
    ///
    /// This is the first recovery-aware guidance layer. It does not assume
    /// that the pilot has followed the nominal profile perfectly. Instead,
    /// it recomputes a recommended pitch from the current vehicle state.
    ///
    /// A later Phase 6 build can replace the correction model with a full
    /// forward trajectory integrator without changing the Ascent page API.
    /// </summary>
    public sealed class MissionPlanner
    {
        private const double MaximumCorrectionDegrees =
            14.0;

        private const double MinimumPitchDegrees =
            0.0;

        private const double MaximumPitchDegrees =
            90.0;

        public MissionPlannerResult CreatePlan(
            MissionTelemetry telemetry,
            double nominalAltitudeMeters,
            double nominalPitchDegrees,
            double targetApoapsisMeters)
        {
            MissionPlannerResult result =
                new MissionPlannerResult
                {
                    NominalPitchDegrees =
                        Clamp(
                            nominalPitchDegrees,
                            MinimumPitchDegrees,
                            MaximumPitchDegrees),

                    RecommendedPitchDegrees =
                        Clamp(
                            nominalPitchDegrees,
                            MinimumPitchDegrees,
                            MaximumPitchDegrees),

                    Command =
                        "HOLD",

                    Status =
                        "PLANNER WAITING"
                };

            if (telemetry == null)
            {
                return result;
            }

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

            /*
             * Positive correction means pitch upward, preserving more
             * vertical energy. Negative correction means pitch downward,
             * building horizontal velocity and reducing overshoot.
             */
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
                0.15;

            totalCorrection +=
                dynamicPressureLimiter;

            totalCorrection =
                Clamp(
                    totalCorrection,
                    -MaximumCorrectionDegrees,
                    MaximumCorrectionDegrees);

            double recommendedPitch =
                Clamp(
                    nominalPitchDegrees +
                    totalCorrection,
                    MinimumPitchDegrees,
                    MaximumPitchDegrees);

            result.PitchCorrectionDegrees =
                totalCorrection;

            result.RecommendedPitchDegrees =
                recommendedPitch;

            result.Command =
                GetCommand(
                    telemetry.Pitch,
                    recommendedPitch);

            result.RecoveryAuthorityPercent =
                CalculateRecoveryAuthority(
                    telemetry,
                    apoapsisError,
                    totalCorrection);

            result.IsTargetAchievable =
                DetermineAchievability(
                    telemetry,
                    apoapsisError,
                    result.RecoveryAuthorityPercent);

            result.Status =
                DetermineStatus(
                    telemetry,
                    altitudeError,
                    apoapsisError,
                    result.IsTargetAchievable);

            return result;
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

            /*
             * Early ascent benefits from a larger vertical fraction.
             * Later ascent should increasingly favor horizontal velocity.
             */
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
            if (telemetry.DynamicPressureKpa <= 25.0)
            {
                return 0.0;
            }

            /*
             * At high dynamic pressure, avoid aggressive pitch-down
             * corrections. A small upward bias reduces rapid steering.
             */
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
            if (telemetry.Apoapsis >= 70000.0)
            {
                return true;
            }

            if (apoapsisError <= 0.0)
            {
                return true;
            }

            if (telemetry.CurrentThrust <= 0.1 &&
                telemetry.VerticalSpeed <= 0.0 &&
                apoapsisError > 5000.0)
            {
                return false;
            }

            return recoveryAuthorityPercent >= 28.0;
        }

        private static string DetermineStatus(
            MissionTelemetry telemetry,
            double altitudeError,
            double apoapsisError,
            bool achievable)
        {
            if (telemetry.MissionTime < 1.0)
            {
                return "AWAIT ASCENT";
            }

            if (!achievable)
            {
                return "TARGET NOT RECOVERABLE";
            }

            if (telemetry.Apoapsis >= 80000.0)
            {
                return "TARGET ENERGY REACHED";
            }

            if (apoapsisError > 20000.0)
            {
                return "ENERGY LOW - RECOVER";
            }

            if (apoapsisError < -12000.0)
            {
                return "ENERGY HIGH - LIMIT AP";
            }

            if (altitudeError > 6000.0)
            {
                return "PROFILE LOW - CLIMB";
            }

            if (altitudeError < -6000.0)
            {
                return "PROFILE HIGH - FLATTEN";
            }

            return "REPLAN NOMINAL";
        }

        private static string GetCommand(
            double actualPitchDegrees,
            double recommendedPitchDegrees)
        {
            double error =
                recommendedPitchDegrees -
                actualPitchDegrees;

            if (error > 2.5)
            {
                return
                    "PITCH UP " +
                    Math.Abs(error)
                        .ToString("0.0") +
                    " DEG";
            }

            if (error < -2.5)
            {
                return
                    "PITCH DOWN " +
                    Math.Abs(error)
                        .ToString("0.0") +
                    " DEG";
            }

            return "HOLD ATTITUDE";
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
    }
}
