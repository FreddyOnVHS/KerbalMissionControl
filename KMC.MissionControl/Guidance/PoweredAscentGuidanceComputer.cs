using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Bounded predictive ascent guidance computer.
    ///
    /// Phase 13B adds target-cutoff projection, coast-to-apex propagation,
    /// mass depletion, and convergence-based confidence.
    /// </summary>
    public sealed class PoweredAscentGuidanceComputer
    {
        private readonly GuidanceOptimizer _optimizer =
            new GuidanceOptimizer();

        private readonly AscentEnergyManager _energyManager =
            new AscentEnergyManager();

        private double _previousPredictionApoapsis =
            double.NaN;

        private double _previousPredictionTime =
            double.NaN;

        public void Reset()
        {
            _previousPredictionApoapsis =
                double.NaN;

            _previousPredictionTime =
                double.NaN;
        }

        public PoweredAscentGuidanceSolution Calculate(
            MissionTelemetry telemetry,
            double referencePitchDegrees,
            double targetApoapsisMeters)
        {
            PoweredAscentGuidanceSolution result =
                new PoweredAscentGuidanceSolution
                {
                    Mode =
                        "INACTIVE"
                };

            if (telemetry == null ||
                telemetry.Altitude <
                    12000.0 ||
                telemetry.CurrentThrust <=
                    0.1 ||
                telemetry.Apoapsis >=
                    targetApoapsisMeters +
                    3000.0)
            {
                return result;
            }

            AscentTrajectoryPrediction best =
                _optimizer.FindBestPitch(
                    telemetry,
                    referencePitchDegrees,
                    targetApoapsisMeters);

            if (best == null ||
                !best.IsValid)
            {
                result.Mode =
                    "NO SOLUTION";

                return result;
            }

            double energyError =
                _energyManager
                    .CalculateTargetEnergyError(
                        telemetry,
                        targetApoapsisMeters);

            double convergence =
                CalculateConvergence(
                    telemetry.MissionTime,
                    best.ApoapsisMeters);

            double confidence =
                CalculateConfidence(
                    telemetry,
                    best,
                    energyError,
                    convergence);

            result.IsAvailable =
                true;

            result.RecommendedPitchDegrees =
                best.PitchDegrees;

            result.PredictedApoapsisMeters =
                best.ApoapsisMeters;

            result.PredictedPeriapsisMeters =
                best.PeriapsisMeters;

            result.OrbitErrorMeters =
                best.ApoapsisMeters -
                targetApoapsisMeters;

            result.ConfidencePercent =
                confidence;

            result.PoweredFlightSeconds =
                best.PoweredFlightSeconds;

            result.CoastFlightSeconds =
                best.CoastFlightSeconds;

            result.PredictionConvergenceMeters =
                convergence;

            result.TargetCutoffReached =
                best.TargetCutoffReached;

            result.Mode =
                best.TargetCutoffReached
                    ? "TARGET CUTOFF"
                    : "ADAPTIVE HORIZON";

            _previousPredictionApoapsis =
                best.ApoapsisMeters;

            _previousPredictionTime =
                telemetry.MissionTime;

            return result;
        }

        private double CalculateConvergence(
            double missionTime,
            double predictedApoapsis)
        {
            if (!IsFinite(
                    _previousPredictionApoapsis) ||
                !IsFinite(
                    _previousPredictionTime) ||
                missionTime <=
                    _previousPredictionTime)
            {
                return double.NaN;
            }

            return Math.Abs(
                predictedApoapsis -
                _previousPredictionApoapsis);
        }

        private static double CalculateConfidence(
            MissionTelemetry telemetry,
            AscentTrajectoryPrediction prediction,
            double energyError,
            double convergence)
        {
            double confidence =
                88.0;

            if (!prediction.TargetCutoffReached)
            {
                confidence -=
                    25.0;
            }

            if (telemetry.DynamicPressureKpa >
                35.0)
            {
                confidence -=
                    18.0;
            }
            else if (telemetry.DynamicPressureKpa >
                     20.0)
            {
                confidence -=
                    8.0;
            }

            if (IsFinite(
                    convergence))
            {
                if (convergence >
                    10000.0)
                {
                    confidence -=
                        30.0;
                }
                else if (convergence >
                         5000.0)
                {
                    confidence -=
                        20.0;
                }
                else if (convergence >
                         2000.0)
                {
                    confidence -=
                        10.0;
                }
                else if (convergence <
                         500.0)
                {
                    confidence +=
                        5.0;
                }
            }

            if (Math.Abs(
                    energyError) >
                1000000.0)
            {
                confidence -=
                    8.0;
            }

            if (prediction.PoweredFlightSeconds >
                50.0)
            {
                confidence -=
                    8.0;
            }

            return Clamp(
                confidence,
                0.0,
                100.0);
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

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
