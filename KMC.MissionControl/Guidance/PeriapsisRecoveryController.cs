using System;

namespace KMC.MissionControl.Guidance
{
    public sealed class PeriapsisRecoveryController
    {
        private const double MinimumSafePeriapsisMeters =
            70000.0;

        private const double PredictedCutoffPeriapsisMeters =
            71000.0;

        public void Reset()
        {
        }

        public PeriapsisRecoverySolution Calculate(
            PeriapsisRecoveryInput input)
        {
            PeriapsisRecoverySolution result =
                new PeriapsisRecoverySolution
                {
                    Reason =
                        "RECOVERY WAITING"
                };

            if (input == null)
            {
                return result;
            }

            double actualPeriapsis =
                IsFinite(input.ActualPeriapsisMeters)
                    ? input.ActualPeriapsisMeters
                    : double.NegativeInfinity;

            double predictedPeriapsis =
                input.GuidanceAvailable &&
                IsFinite(input.PredictedPeriapsisMeters)
                    ? input.PredictedPeriapsisMeters
                    : actualPeriapsis;

            double error =
                Math.Max(
                    0.0,
                    MinimumSafePeriapsisMeters -
                    actualPeriapsis);

            bool actualSafe =
                actualPeriapsis >=
                    MinimumSafePeriapsisMeters;

            bool predictedSafe =
                predictedPeriapsis >=
                    PredictedCutoffPeriapsisMeters;

            result.PeriapsisErrorMeters =
                error;

            result.ActualPeriapsisSafe =
                actualSafe;

            result.PredictedPeriapsisSafe =
                predictedSafe;

            result.ProducingThrust =
                input.ProducingThrust ||
                input.Throttle > 0.01;

            if (actualSafe ||
                predictedSafe)
            {
                result.ThrottlePercent =
                    0.0;

                result.CutoffRequired =
                    result.ProducingThrust;

                result.Reason =
                    actualSafe
                        ? "LIVE PERIAPSIS SAFE"
                        : "PREDICTED PERIAPSIS SAFE";

                return result;
            }

            result.ThrottlePercent =
                CalculateThrottlePercent(
                    error);

            result.CutoffRequired =
                false;

            result.Reason =
                "RAISE PERIAPSIS";

            return result;
        }

        private static double CalculateThrottlePercent(
            double errorMeters)
        {
            if (errorMeters > 40000.0)
            {
                return 55.0;
            }

            if (errorMeters > 20000.0)
            {
                return 30.0;
            }

            if (errorMeters > 8000.0)
            {
                return 15.0;
            }

            if (errorMeters > 2000.0)
            {
                return 8.0;
            }

            return 5.0;
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
