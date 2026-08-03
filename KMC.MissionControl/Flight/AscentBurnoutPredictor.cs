using System;
using System.Collections.Generic;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Flight
{
    /// <summary>
    /// Estimates powered-stage burnout time, velocity, apoapsis, and
    /// confidence from recent stage telemetry.
    ///
    /// The equations and thresholds are intentionally preserved from the
    /// former AscentPage implementation.
    /// </summary>
    public sealed class AscentBurnoutPredictor
    {
        private int _predictionStage =
            -1;

        private double _predictionStageStartTime =
            double.NaN;

        public void Reset()
        {
            _predictionStage =
                -1;

            _predictionStageStartTime =
                double.NaN;
        }

        public BurnoutPrediction Calculate(
            MissionTelemetry telemetry,
            IList<AscentHistorySample> samples,
            double targetApoapsisMeters)
        {
            BurnoutPrediction result =
                new BurnoutPrediction
                {
                    Status =
                        "COLLECTING DATA"
                };

            if (telemetry == null)
            {
                return result;
            }

            if (_predictionStage !=
                telemetry.CurrentStage)
            {
                _predictionStage =
                    telemetry.CurrentStage;

                _predictionStageStartTime =
                    telemetry.MissionTime;

                result.Status =
                    "STAGE TREND RESET";

                return result;
            }

            if (!IsFinite(
                    _predictionStageStartTime))
            {
                _predictionStageStartTime =
                    telemetry.MissionTime;
            }

            double stageAge =
                telemetry.MissionTime -
                _predictionStageStartTime;

            if (stageAge < 2.5)
            {
                result.Status =
                    "COLLECTING STAGE DATA";

                return result;
            }

            List<AscentHistorySample> window =
                GetPredictionWindow(
                    samples,
                    telemetry.CurrentStage,
                    telemetry.MissionTime,
                    6.0);

            if (window.Count < 8)
            {
                result.Status =
                    "COLLECTING DATA";

                return result;
            }

            AscentHistorySample newest =
                window[
                    window.Count - 1];

            double elapsed =
                newest.MissionTime -
                window[0].MissionTime;

            if (elapsed < 1.5)
            {
                return result;
            }

            double liquidFuelRate =
                CalculateConsumptionRate(
                    window,
                    sample =>
                        sample.StageLiquidFuelAmount);

            double oxidizerRate =
                CalculateConsumptionRate(
                    window,
                    sample =>
                        sample.StageOxidizerAmount);

            double liquidFuelTime =
                liquidFuelRate > 0.0001
                    ? newest.StageLiquidFuelAmount /
                      liquidFuelRate
                    : double.PositiveInfinity;

            double oxidizerTime =
                oxidizerRate > 0.0001
                    ? newest.StageOxidizerAmount /
                      oxidizerRate
                    : double.PositiveInfinity;

            double timeRemaining =
                Math.Min(
                    liquidFuelTime,
                    oxidizerTime);

            if (!IsFinite(
                    timeRemaining) ||
                timeRemaining <= 0.0 ||
                timeRemaining > 1800.0)
            {
                result.Status =
                    telemetry.CurrentThrust > 0.1
                        ? "FUEL TREND UNAVAILABLE"
                        : "ENGINE OFF";

                return result;
            }

            RegressionResult apoapsisTrend =
                CalculateRegression(
                    window,
                    sample =>
                        sample.ApoapsisMeters);

            RegressionResult velocityTrend =
                CalculateRegression(
                    window,
                    sample =>
                        sample
                            .OrbitalSpeedMetersPerSecond);

            if (!apoapsisTrend.IsValid ||
                !velocityTrend.IsValid)
            {
                result.Status =
                    "TREND UNSTABLE";

                return result;
            }

            double predictedApoapsis =
                newest.ApoapsisMeters +
                apoapsisTrend.SlopePerSecond *
                timeRemaining;

            double predictedVelocity =
                newest.OrbitalSpeedMetersPerSecond +
                velocityTrend.SlopePerSecond *
                timeRemaining;

            double fuelConsistency =
                CalculateFuelConsistency(
                    window);

            double trendQuality =
                Math.Min(
                    apoapsisTrend.RSquared,
                    velocityTrend.RSquared);

            double sampleQuality =
                Math.Min(
                    1.0,
                    window.Count /
                    24.0);

            double confidence =
                100.0 *
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        trendQuality *
                        0.55 +
                        fuelConsistency *
                        0.25 +
                        sampleQuality *
                        0.20));

            result.IsAvailable =
                true;

            result.HasFuelTrend =
                true;

            result.TimeRemainingSeconds =
                timeRemaining;

            result.PredictedApoapsisMeters =
                Math.Max(
                    newest.ApoapsisMeters,
                    predictedApoapsis);

            result.BurnoutVelocityMetersPerSecond =
                Math.Max(
                    0.0,
                    predictedVelocity);

            result.ConfidencePercent =
                confidence;

            double targetError =
                result.PredictedApoapsisMeters -
                targetApoapsisMeters;

            if (confidence < 35.0)
            {
                result.Status =
                    "LOW CONFIDENCE";
            }
            else if (targetError < -5000.0)
            {
                result.Status =
                    "TARGET AT RISK";
            }
            else if (targetError > 8000.0)
            {
                result.Status =
                    "OVERSHOOT LIKELY";
            }
            else
            {
                result.Status =
                    "TARGET ACHIEVABLE";
            }

            return result;
        }

        private static List<AscentHistorySample> GetPredictionWindow(
            IList<AscentHistorySample> samples,
            int stage,
            double currentMissionTime,
            double windowSeconds)
        {
            List<AscentHistorySample> result =
                new List<AscentHistorySample>();

            if (samples == null)
            {
                return result;
            }

            double earliestTime =
                currentMissionTime -
                windowSeconds;

            for (int index =
                    samples.Count - 1;
                 index >= 0;
                 index--)
            {
                AscentHistorySample sample =
                    samples[index];

                if (sample.MissionTime <
                    earliestTime)
                {
                    break;
                }

                if (sample.StageNumber ==
                    stage)
                {
                    result.Add(
                        sample);
                }
            }

            result.Reverse();

            return result;
        }

        private static double CalculateConsumptionRate(
            IList<AscentHistorySample> samples,
            Func<AscentHistorySample, double> selector)
        {
            RegressionResult trend =
                CalculateRegression(
                    samples,
                    selector);

            if (!trend.IsValid)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                -trend.SlopePerSecond);
        }

        private static double CalculateFuelConsistency(
            IList<AscentHistorySample> samples)
        {
            RegressionResult liquidFuel =
                CalculateRegression(
                    samples,
                    sample =>
                        sample.StageLiquidFuelAmount);

            RegressionResult oxidizer =
                CalculateRegression(
                    samples,
                    sample =>
                        sample.StageOxidizerAmount);

            double best =
                Math.Max(
                    liquidFuel.RSquared,
                    oxidizer.RSquared);

            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    best));
        }

        private static RegressionResult CalculateRegression(
            IList<AscentHistorySample> samples,
            Func<AscentHistorySample, double> selector)
        {
            RegressionResult result =
                new RegressionResult();

            if (samples == null ||
                selector == null ||
                samples.Count < 3)
            {
                return result;
            }

            double origin =
                samples[0].MissionTime;

            double sumX = 0.0;
            double sumY = 0.0;
            double sumXX = 0.0;
            double sumXY = 0.0;

            int count = 0;

            for (int index = 0;
                 index < samples.Count;
                 index++)
            {
                double x =
                    samples[index].MissionTime -
                    origin;

                double y =
                    selector(
                        samples[index]);

                if (!IsFinite(x) ||
                    !IsFinite(y))
                {
                    continue;
                }

                sumX += x;
                sumY += y;
                sumXX += x * x;
                sumXY += x * y;
                count++;
            }

            if (count < 3)
            {
                return result;
            }

            double denominator =
                count *
                sumXX -
                sumX *
                sumX;

            if (Math.Abs(
                    denominator) <
                0.000001)
            {
                return result;
            }

            double slope =
                (count *
                 sumXY -
                 sumX *
                 sumY) /
                denominator;

            double intercept =
                (sumY -
                 slope *
                 sumX) /
                count;

            double meanY =
                sumY /
                count;

            double totalVariation = 0.0;
            double residualVariation = 0.0;

            for (int index = 0;
                 index < samples.Count;
                 index++)
            {
                double x =
                    samples[index].MissionTime -
                    origin;

                double y =
                    selector(
                        samples[index]);

                if (!IsFinite(x) ||
                    !IsFinite(y))
                {
                    continue;
                }

                double fitted =
                    intercept +
                    slope *
                    x;

                double totalError =
                    y -
                    meanY;

                double residualError =
                    y -
                    fitted;

                totalVariation +=
                    totalError *
                    totalError;

                residualVariation +=
                    residualError *
                    residualError;
            }

            double rSquared =
                totalVariation > 0.000001
                    ? 1.0 -
                      residualVariation /
                      totalVariation
                    : 1.0;

            result.IsValid =
                true;

            result.SlopePerSecond =
                slope;

            result.RSquared =
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        rSquared));

            return result;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private sealed class RegressionResult
        {
            public bool IsValid { get; set; }

            public double SlopePerSecond { get; set; }

            public double RSquared { get; set; }
        }
    }
}
