using System;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned migration of the legacy OrbitalGuidanceController.
    ///
    /// This class owns orbit-completion and protective-stop decisions only.
    /// It does not own ORBIT phase selection or periapsis-recovery steering.
    /// </summary>
    internal sealed class OrbitSafetyController
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
             * Stateless. Retained to preserve the legacy controller contract
             * and provide a stable extension point for later ORBIT guidance.
             */
        }

        public OrbitSafetyModel Evaluate(
            OrbitTelemetryState current,
            CircularizationPredictionModel prediction,
            double targetOrbitMeters,
            bool circularizationStarted,
            bool cutoffLatched)
        {
            OrbitSafetyModel result =
                new OrbitSafetyModel
                {
                    CircularizationStarted =
                        circularizationStarted,

                    CutoffLatched =
                        cutoffLatched,

                    TargetOrbitMeters =
                        targetOrbitMeters,

                    Reason =
                        circularizationStarted
                            ? "CONTINUE CIRCULARIZATION"
                            : "SAFETY WAITING"
                };

            if (current != null)
            {
                result.ActualApoapsisMeters =
                    current.ApoapsisMeters;

                result.ActualPeriapsisMeters =
                    current.PeriapsisMeters;
            }

            if (prediction != null)
            {
                result.PredictedApoapsisMeters =
                    prediction.PredictedApoapsisMeters;

                result.PredictedPeriapsisMeters =
                    prediction.PredictedPeriapsisMeters;

                result.PredictedOrbitErrorMeters =
                    prediction.PredictedOrbitErrorMeters;

                result.PredictedEnergyErrorJoulesPerKilogram =
                    prediction
                        .PredictedEnergyErrorJoulesPerKilogram;

                result.RemainingDeltaVMetersPerSecond =
                    prediction
                        .RemainingDeltaVMetersPerSecond;
            }

            bool evidenceAvailable =
                current != null &&
                current.Available &&
                prediction != null &&
                prediction.Available;

            if (!circularizationStarted ||
                !evidenceAvailable)
            {
                if (cutoffLatched)
                {
                    result.Available =
                        true;

                    result.OrbitAchieved =
                        true;

                    result.CutoffRequired =
                        true;

                    result.CutoffLatched =
                        true;

                    result.Reason =
                        "ORBIT CUTOFF LATCHED";
                }

                return result;
            }

            result.Available =
                true;

            bool actualPeriapsisSafe =
                IsFinite(
                    current.PeriapsisMeters) &&
                current.PeriapsisMeters >=
                    KerbinAtmosphereTopMeters;

            bool predictedPeriapsisSafe =
                IsFinite(
                    prediction.PredictedPeriapsisMeters) &&
                prediction.PredictedPeriapsisMeters >=
                    KerbinAtmosphereTopMeters;

            result.ActualPeriapsisSafe =
                actualPeriapsisSafe;

            result.PredictedPeriapsisSafe =
                predictedPeriapsisSafe;

            bool energySatisfied =
                prediction
                    .PredictedEnergyErrorJoulesPerKilogram <=
                EnergyCutoffToleranceJoulesPerKilogram;

            bool deltaVSatisfied =
                prediction
                    .RemainingDeltaVMetersPerSecond <=
                RemainingDeltaVCutoffMetersPerSecond;

            result.EnergySatisfied =
                energySatisfied;

            result.DeltaVSatisfied =
                deltaVSatisfied;

            bool predictedOrbitNominal =
                prediction
                    .PredictedOrbitErrorMeters <=
                OrbitNominalToleranceMeters;

            result.PredictedOrbitNominal =
                predictedOrbitNominal;

            bool predictedApoapsisTooHigh =
                prediction.PredictedApoapsisMeters >
                    targetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            bool actualApoapsisTooHigh =
                current.ApoapsisMeters >
                    targetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            result.PredictedApoapsisTooHigh =
                predictedApoapsisTooHigh;

            result.ActualApoapsisTooHigh =
                actualApoapsisTooHigh;

            bool predictedOrbitTooHigh =
                predictedApoapsisTooHigh ||
                prediction.PredictedPeriapsisMeters >
                    targetOrbitMeters +
                    MaximumAllowedOrbitErrorMeters;

            result.PredictedOrbitTooHigh =
                predictedOrbitTooHigh;

            /*
             * Once cutoff has latched, keep the terminal decision permanent
             * but continue evaluating the supporting evidence every cycle.
             * This prevents the diagnostic/UI evidence flags from falling
             * back to their default false values after cutoff.
             */
            if (cutoffLatched)
            {
                result.OrbitAchieved =
                    true;

                result.CutoffRequired =
                    true;

                result.CutoffLatched =
                    true;

                result.PauseBurn =
                    false;

                result.Reason =
                    "ORBIT CUTOFF LATCHED";

                return result;
            }

            if (actualPeriapsisSafe &&
                predictedPeriapsisSafe &&
                predictedOrbitNominal &&
                (energySatisfied ||
                 deltaVSatisfied))
            {
                result.OrbitAchieved =
                    true;

                result.CutoffRequired =
                    true;

                result.Reason =
                    "SAFE ORBIT CONFIRMED";

                return result;
            }

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

            if (actualPeriapsisSafe &&
                predictedPeriapsisSafe &&
                predictedOrbitTooHigh)
            {
                result.OrbitAchieved =
                    true;

                result.CutoffRequired =
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
