using System;
using KMC.MissionControl.Models;

namespace KMC.MissionControl.Guidance
{
    /// <summary>
    /// Lightweight two-dimensional powered trajectory projection.
    ///
    /// It deliberately uses a short horizon and a small candidate set so it
    /// is suitable for real-time and multiplayer use. Only the final
    /// guidance result needs to be transmitted.
    /// </summary>
    public sealed class PoweredTrajectoryPredictor
    {
        private const double KerbinRadiusMeters = 600000.0;
        private const double Mu = 3.5316e12;
        private const double TimeStepSeconds = 0.25;
        private const double ProjectionSeconds = 14.0;

        internal AscentTrajectoryPrediction Predict(
            MissionTelemetry telemetry,
            double pitchDegrees,
            double targetApoapsisMeters)
        {
            AscentTrajectoryPrediction output =
                new AscentTrajectoryPrediction
                {
                    PitchDegrees = pitchDegrees
                };

            if (telemetry == null ||
                !IsFinite(telemetry.Altitude) ||
                !IsFinite(telemetry.VerticalSpeed) ||
                !IsFinite(telemetry.HorizontalSpeed))
            {
                return output;
            }

            double r =
                KerbinRadiusMeters +
                Math.Max(0.0, telemetry.Altitude);

            double radialVelocity =
                telemetry.VerticalSpeed;

            double tangentialVelocity =
                Math.Max(0.0, telemetry.HorizontalSpeed);

            double thrust =
                Math.Max(
                    telemetry.CurrentThrust,
                    telemetry.MaximumThrust *
                    Math.Max(0.0, telemetry.Throttle));

            double mass =
                Math.Max(0.001, telemetry.VesselMass);

            // kN / tonne is numerically m/s².
            double thrustAcceleration =
                Math.Max(0.0, thrust / mass);

            double pitchRadians =
                pitchDegrees *
                Math.PI /
                180.0;

            double radialThrust =
                thrustAcceleration *
                Math.Sin(pitchRadians);

            double tangentialThrust =
                thrustAcceleration *
                Math.Cos(pitchRadians);

            int steps =
                (int)Math.Ceiling(
                    ProjectionSeconds /
                    TimeStepSeconds);

            for (int i = 0; i < steps; i++)
            {
                double gravity =
                    Mu /
                    (r * r);

                double radialAcceleration =
                    radialThrust -
                    gravity +
                    tangentialVelocity *
                    tangentialVelocity /
                    r;

                double tangentialAcceleration =
                    tangentialThrust -
                    radialVelocity *
                    tangentialVelocity /
                    r;

                radialVelocity +=
                    radialAcceleration *
                    TimeStepSeconds;

                tangentialVelocity +=
                    tangentialAcceleration *
                    TimeStepSeconds;

                r +=
                    radialVelocity *
                    TimeStepSeconds;

                if (r <= KerbinRadiusMeters)
                {
                    return output;
                }
            }

            double speedSquared =
                radialVelocity * radialVelocity +
                tangentialVelocity * tangentialVelocity;

            double specificEnergy =
                speedSquared / 2.0 -
                Mu / r;

            double angularMomentum =
                r * tangentialVelocity;

            if (!IsFinite(specificEnergy) ||
                specificEnergy >= 0.0)
            {
                return output;
            }

            double semiMajorAxis =
                -Mu /
                (2.0 * specificEnergy);

            double eccentricityTerm =
                1.0 +
                2.0 *
                specificEnergy *
                angularMomentum *
                angularMomentum /
                (Mu * Mu);

            double eccentricity =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        eccentricityTerm));

            double apoapsis =
                semiMajorAxis *
                (1.0 + eccentricity) -
                KerbinRadiusMeters;

            double periapsis =
                semiMajorAxis *
                (1.0 - eccentricity) -
                KerbinRadiusMeters;

            if (!IsFinite(apoapsis) ||
                !IsFinite(periapsis))
            {
                return output;
            }

            double apoapsisError =
                Math.Abs(
                    apoapsis -
                    targetApoapsisMeters);

            double verticalPenalty =
                Math.Max(
                    0.0,
                    Math.Abs(radialVelocity) -
                    180.0) *
                15.0;

            double unsafePeriapsisPenalty =
                periapsis < -100000.0
                    ? 250000.0
                    : 0.0;

            output.IsValid = true;
            output.ApoapsisMeters = apoapsis;
            output.PeriapsisMeters = periapsis;
            output.FinalVerticalSpeedMetersPerSecond =
                radialVelocity;
            output.Score =
                apoapsisError +
                verticalPenalty +
                unsafePeriapsisPenalty;

            return output;
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
