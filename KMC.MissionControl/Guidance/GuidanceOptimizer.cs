using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    public sealed class GuidanceOptimizer
    {
        private readonly PoweredTrajectoryPredictor _predictor =
            new PoweredTrajectoryPredictor();

        internal AscentTrajectoryPrediction FindBestPitch(
            MissionTelemetry telemetry,
            double referencePitchDegrees,
            double targetApoapsisMeters)
        {
            double[] offsets =
            {
                -6.0,
                -3.0,
                0.0,
                3.0,
                6.0
            };

            AscentTrajectoryPrediction best = null;

            for (int i = 0; i < offsets.Length; i++)
            {
                double candidatePitch =
                    Clamp(
                        referencePitchDegrees +
                        offsets[i],
                        0.0,
                        90.0);

                AscentTrajectoryPrediction candidate =
                    _predictor.Predict(
                        telemetry,
                        candidatePitch,
                        targetApoapsisMeters);

                if (!candidate.IsValid)
                {
                    continue;
                }

                candidate.Score +=
                    Math.Abs(offsets[i]) *
                    125.0;

                if (best == null ||
                    candidate.Score < best.Score)
                {
                    best = candidate;
                }
            }

            return best;
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
