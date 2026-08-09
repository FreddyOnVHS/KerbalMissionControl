using System;

namespace KMC.Engine.Ascent
{
    internal sealed class GuidanceOptimizer
    {
        private readonly PoweredTrajectoryPredictor _predictor =
            new PoweredTrajectoryPredictor();

        public AscentTrajectoryPrediction FindBestPitch(
            AscentTelemetryState telemetry,
            PoweredAscentThrustInput thrustInput,
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

            AscentTrajectoryPrediction best =
                null;

            for (int index = 0;
                 index < offsets.Length;
                 index++)
            {
                double candidatePitch =
                    Clamp(
                        referencePitchDegrees +
                        offsets[index],
                        0.0,
                        90.0);

                AscentTrajectoryPrediction candidate =
                    _predictor.Predict(
                        telemetry,
                        thrustInput,
                        candidatePitch,
                        targetApoapsisMeters);

                if (!candidate.IsValid)
                {
                    continue;
                }

                candidate.Score +=
                    Math.Abs(
                        offsets[index]) *
                    125.0;

                if (best == null ||
                    candidate.Score <
                    best.Score)
                {
                    best =
                        candidate;
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
