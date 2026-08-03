using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Bounded predictive ascent guidance inspired by the architectural
    /// separation used by mature KSP guidance systems:
    /// prediction, optimization, and mission coordination are independent.
    ///
    /// This is advisory and does not directly control the vessel.
    /// </summary>
    public sealed class PoweredAscentGuidanceComputer
    {
        private readonly GuidanceOptimizer _optimizer =
            new GuidanceOptimizer();

        private readonly AscentEnergyManager _energyManager =
            new AscentEnergyManager();

        public void Reset()
        {
        }

        public PoweredAscentGuidanceSolution Calculate(
            MissionTelemetry telemetry,
            double referencePitchDegrees,
            double targetApoapsisMeters)
        {
            PoweredAscentGuidanceSolution result =
                new PoweredAscentGuidanceSolution
                {
                    Mode = "INACTIVE"
                };

            if (telemetry == null ||
                telemetry.Altitude < 12000.0 ||
                telemetry.CurrentThrust <= 0.1 ||
                telemetry.Apoapsis >=
                    targetApoapsisMeters + 3000.0)
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
                _energyManager.CalculateTargetEnergyError(
                    telemetry,
                    targetApoapsisMeters);

            double confidence =
                80.0;

            if (telemetry.DynamicPressureKpa > 35.0)
            {
                confidence -= 20.0;
            }

            if (Math.Abs(energyError) >
                1000000.0)
            {
                confidence -= 10.0;
            }

            result.IsAvailable = true;
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
                Math.Max(
                    0.0,
                    Math.Min(
                        100.0,
                        confidence));
            result.Mode =
                "PREDICTIVE";

            return result;
        }
    }
}
