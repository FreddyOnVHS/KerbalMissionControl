using System;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Owns orbit-completion and protective-stop decisions.
    ///
    /// MissionPlanner remains the phase coordinator, while this controller
    /// guarantees that ORBIT ACHIEVED cannot be declared with an unsafe live
    /// periapsis.
    /// </summary>
    public sealed class OrbitalGuidanceController
    {
        private const double KerbinAtmosphereTopMeters =
            70000.0;

        private const double OrbitNominalToleranceMeters =
            3000.0;

        private const double MaximumAllowedOrbitErrorMeters =
            7500.0;

        private const double RemainingDeltaVCutoffMetersPerSecond =
            1.25;

        private const double EnergyCutoffToleranceJoulesPerKilogram =
            3000.0;

        public void Reset()
        {
            /*
             * Stateless in this build. Method retained because future
             * powered-vector guidance will keep convergence state here.
             */
        }

        public OrbitSafetyDecision Evaluate(
            OrbitSafetyInput input)
        {
            OrbitSafetyDecision result =
                new OrbitSafetyDecision
                {
                    Reason =
                        "CONTINUE CIRCULARIZATION"
                };

            if (input == null ||
                !input.GuidanceAvailable)
            {
                return result;
            }

            bool actualPeriapsisSafe =
                IsFinite(
                    input.ActualPeriapsisMeters) &&
                input.ActualPeriapsisMeters >=
                    KerbinAtmosphereTopMeters;

            bool predictedPeriapsisSafe =
                IsFinite(
                    input.PredictedPeriapsisMeters) &&
                input.PredictedPeriapsisMeters >=
                    KerbinAtmosphereTopMeters;

            result.ActualPeriapsisSafe =
                actualPeriapsisSafe;

            result.PredictedPeriapsisSafe =
                predictedPeriapsisSafe;

            bool energySatisfied =
                input.PredictedEnergyError <=
                    EnergyCutoffToleranceJoulesPerKilogram;

            bool deltaVSatisfied =
                input.RemainingDeltaVMetersPerSecond <=
                    RemainingDeltaVCutoffMetersPerSecond;

            result.EnergySatisfied =
                energySatisfied;

            result.DeltaVSatisfied =
                deltaVSatisfied;

            bool predictedOrbitNominal =
                input.PredictedOrbitErrorMeters <=
                    OrbitNominalToleranceMeters;

            /*
             * Orbit completion requires the LIVE orbit to be survivable.
             * A predicted safe periapsis alone is not enough to latch
             * ORBIT ACHIEVED.
             */
            if (actualPeriapsisSafe &&
                predictedPeriapsisSafe &&
                predictedOrbitNominal &&
                (energySatisfied ||
                 deltaVSatisfied))
            {
                result.OrbitAchieved =
                    true;

                result.Reason =
                    "SAFE ORBIT CONFIRMED";

                return result;
            }

            bool predictedApoapsisTooHigh =
                input.PredictedApoapsisMeters >
                    input.TargetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            bool actualApoapsisTooHigh =
                input.ActualApoapsisMeters >
                    input.TargetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            /*
             * If apoapsis is running away while periapsis remains unsafe,
             * pause instead of falsely declaring success. The pilot can
             * return to prograde and reassess the orbit shape.
             */
            if (!actualPeriapsisSafe &&
                (predictedApoapsisTooHigh ||
                 actualApoapsisTooHigh))
            {
                result.PauseBurn =
                    true;

                result.Reason =
                    "APOAPSIS HIGH / PERIAPSIS UNSAFE";

                return result;
            }

            /*
             * A high predicted orbit may be protectively stopped only after
             * the live periapsis is already above the atmosphere.
             */
            bool predictedOrbitTooHigh =
                predictedApoapsisTooHigh ||
                input.PredictedPeriapsisMeters >
                    input.TargetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            if (actualPeriapsisSafe &&
                predictedPeriapsisSafe &&
                predictedOrbitTooHigh)
            {
                result.OrbitAchieved =
                    true;

                result.Reason =
                    "SAFE PROTECTIVE CUTOFF";
            }

            return result;
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
