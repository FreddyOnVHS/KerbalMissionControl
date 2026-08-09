using System;

namespace KMC.Engine.Ascent
{
    /// <summary>
    /// Lightweight two-dimensional powered-flight and coast predictor.
    ///
    /// Build 9.3 preserves the current MissionControl propagation equations,
    /// time steps, penalties, target-cutoff logic, and Kerbin constants.
    /// Only data ownership changes.
    /// </summary>
    internal sealed class PoweredTrajectoryPredictor
    {
        private const double KerbinRadiusMeters =
            600000.0;

        private const double Mu =
            3.5316e12;

        private const double StandardGravity =
            9.80665;

        private const double PoweredTimeStepSeconds =
            0.20;

        private const double CoastTimeStepSeconds =
            0.50;

        private const double MinimumPoweredSeconds =
            2.0;

        private const double MaximumCoastSeconds =
            600.0;

        public AscentTrajectoryPrediction Predict(
            AscentTelemetryState telemetry,
            PoweredAscentThrustInput thrustInput,
            double pitchDegrees,
            double targetApoapsisMeters)
        {
            AscentTrajectoryPrediction output =
                new AscentTrajectoryPrediction
                {
                    PitchDegrees =
                        pitchDegrees
                };

            if (!CanPredict(
                    telemetry,
                    thrustInput,
                    targetApoapsisMeters))
            {
                return output;
            }

            double radius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    telemetry.AltitudeMeters);

            double radialVelocity =
                telemetry.VerticalSpeedMetersPerSecond;

            double tangentialVelocity =
                Math.Max(
                    0.0,
                    telemetry.HorizontalSpeedMetersPerSecond);

            /*
             * Preserve the legacy thrust-selection equation:
             *
             * max(CurrentThrust, Maximum/AvailableThrust * Throttle)
             *
             * The values now come from an explicitly labeled Engine-owned
             * thrust input.
             */
            double thrustKilonewtons =
                Math.Max(
                    thrustInput.CurrentThrustKilonewtons,
                    thrustInput.AvailableThrustKilonewtons *
                    Math.Max(
                        0.0,
                        thrustInput.ThrottleCommand));

            double massTonnes =
                Math.Max(
                    0.001,
                    telemetry.VesselMassTonnes);

            double specificImpulseSeconds =
                IsFinite(
                    telemetry.AverageSpecificImpulseSeconds) &&
                telemetry.AverageSpecificImpulseSeconds >
                    1.0
                    ? telemetry.AverageSpecificImpulseSeconds
                    : 300.0;

            double massFlowTonnesPerSecond =
                thrustKilonewtons /
                (specificImpulseSeconds *
                 StandardGravity) /
                1000.0;

            double pitchRadians =
                pitchDegrees *
                Math.PI /
                180.0;

            double poweredHorizonSeconds =
                CalculatePoweredHorizon(
                    telemetry,
                    targetApoapsisMeters);

            double poweredTime =
                0.0;

            bool targetCutoffReached =
                false;

            while (poweredTime <
                   poweredHorizonSeconds)
            {
                double thrustAcceleration =
                    thrustKilonewtons /
                    Math.Max(
                        0.001,
                        massTonnes);

                double radialThrust =
                    thrustAcceleration *
                    Math.Sin(
                        pitchRadians);

                double tangentialThrust =
                    thrustAcceleration *
                    Math.Cos(
                        pitchRadians);

                IntegrateStep(
                    ref radius,
                    ref radialVelocity,
                    ref tangentialVelocity,
                    radialThrust,
                    tangentialThrust,
                    PoweredTimeStepSeconds);

                massTonnes =
                    Math.Max(
                        0.001,
                        massTonnes -
                        massFlowTonnesPerSecond *
                        PoweredTimeStepSeconds);

                poweredTime +=
                    PoweredTimeStepSeconds;

                if (radius <=
                    KerbinRadiusMeters)
                {
                    return output;
                }

                OrbitElements poweredOrbit =
                    CalculateOrbitElements(
                        radius,
                        radialVelocity,
                        tangentialVelocity);

                if (poweredOrbit.IsValid &&
                    poweredTime >=
                        MinimumPoweredSeconds &&
                    poweredOrbit.ApoapsisMeters >=
                        targetApoapsisMeters)
                {
                    targetCutoffReached =
                        true;

                    break;
                }
            }

            OrbitElements cutoffOrbit =
                CalculateOrbitElements(
                    radius,
                    radialVelocity,
                    tangentialVelocity);

            if (!cutoffOrbit.IsValid)
            {
                return output;
            }

            double coastTime =
                PropagateCoastToApex(
                    ref radius,
                    ref radialVelocity,
                    ref tangentialVelocity);

            OrbitElements finalOrbit =
                CalculateOrbitElements(
                    radius,
                    radialVelocity,
                    tangentialVelocity);

            if (!finalOrbit.IsValid)
            {
                finalOrbit =
                    cutoffOrbit;
            }

            double propagatedApoapsis =
                Math.Max(
                    finalOrbit.ApoapsisMeters,
                    radius -
                    KerbinRadiusMeters);

            double apoapsisError =
                Math.Abs(
                    propagatedApoapsis -
                    targetApoapsisMeters);

            double cutoffVerticalPenalty =
                Math.Max(
                    0.0,
                    Math.Abs(
                        radialVelocity) -
                    35.0) *
                22.0;

            double horizonPenalty =
                targetCutoffReached
                    ? 0.0
                    : 25000.0;

            double unsafePenalty =
                finalOrbit.PeriapsisMeters <
                    -150000.0
                    ? 150000.0
                    : 0.0;

            output.IsValid =
                true;

            output.ApoapsisMeters =
                propagatedApoapsis;

            output.PeriapsisMeters =
                finalOrbit.PeriapsisMeters;

            output.FinalVerticalSpeedMetersPerSecond =
                radialVelocity;

            output.PoweredFlightSeconds =
                poweredTime;

            output.CoastFlightSeconds =
                coastTime;

            output.TargetCutoffReached =
                targetCutoffReached;

            output.Score =
                apoapsisError +
                cutoffVerticalPenalty +
                horizonPenalty +
                unsafePenalty;

            return output;
        }

        private static bool CanPredict(
            AscentTelemetryState telemetry,
            PoweredAscentThrustInput thrustInput,
            double targetApoapsisMeters)
        {
            return
                telemetry != null &&
                thrustInput != null &&
                thrustInput.CurrentThrustKnown &&
                thrustInput.AvailableThrustKnown &&
                IsFinite(
                    telemetry.AltitudeMeters) &&
                IsFinite(
                    telemetry.VerticalSpeedMetersPerSecond) &&
                IsFinite(
                    telemetry.HorizontalSpeedMetersPerSecond) &&
                IsFinite(
                    targetApoapsisMeters) &&
                targetApoapsisMeters >
                    0.0;
        }

        private static double CalculatePoweredHorizon(
            AscentTelemetryState telemetry,
            double targetApoapsisMeters)
        {
            double apoapsisGap =
                Math.Max(
                    0.0,
                    targetApoapsisMeters -
                    telemetry.ApoapsisMeters);

            double horizon =
                18.0 +
                apoapsisGap /
                2500.0;

            if (telemetry.AltitudeMeters >
                30000.0)
            {
                horizon +=
                    8.0;
            }

            if (telemetry.AltitudeMeters >
                55000.0)
            {
                horizon +=
                    8.0;
            }

            return Clamp(
                horizon,
                18.0,
                60.0);
        }

        private static void IntegrateStep(
            ref double radius,
            ref double radialVelocity,
            ref double tangentialVelocity,
            double radialThrust,
            double tangentialThrust,
            double timeStep)
        {
            double gravity =
                Mu /
                (radius *
                 radius);

            double radialAcceleration =
                radialThrust -
                gravity +
                tangentialVelocity *
                tangentialVelocity /
                radius;

            double tangentialAcceleration =
                tangentialThrust -
                radialVelocity *
                tangentialVelocity /
                radius;

            radialVelocity +=
                radialAcceleration *
                timeStep;

            tangentialVelocity +=
                tangentialAcceleration *
                timeStep;

            radius +=
                radialVelocity *
                timeStep;
        }

        private static double PropagateCoastToApex(
            ref double radius,
            ref double radialVelocity,
            ref double tangentialVelocity)
        {
            double coastTime =
                0.0;

            if (radialVelocity <=
                0.0)
            {
                return coastTime;
            }

            while (coastTime <
                   MaximumCoastSeconds &&
                   radialVelocity >
                       0.0)
            {
                IntegrateStep(
                    ref radius,
                    ref radialVelocity,
                    ref tangentialVelocity,
                    0.0,
                    0.0,
                    CoastTimeStepSeconds);

                coastTime +=
                    CoastTimeStepSeconds;

                if (radius <=
                    KerbinRadiusMeters)
                {
                    break;
                }
            }

            return coastTime;
        }

        private static OrbitElements CalculateOrbitElements(
            double radius,
            double radialVelocity,
            double tangentialVelocity)
        {
            OrbitElements output =
                new OrbitElements();

            double speedSquared =
                radialVelocity *
                radialVelocity +
                tangentialVelocity *
                tangentialVelocity;

            double specificEnergy =
                speedSquared /
                2.0 -
                Mu /
                radius;

            double angularMomentum =
                radius *
                tangentialVelocity;

            if (!IsFinite(
                    specificEnergy) ||
                specificEnergy >=
                    0.0 ||
                !IsFinite(
                    angularMomentum))
            {
                return output;
            }

            double semiMajorAxis =
                -Mu /
                (2.0 *
                 specificEnergy);

            double eccentricityTerm =
                1.0 +
                2.0 *
                specificEnergy *
                angularMomentum *
                angularMomentum /
                (Mu *
                 Mu);

            double eccentricity =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        eccentricityTerm));

            double apoapsis =
                semiMajorAxis *
                (1.0 +
                 eccentricity) -
                KerbinRadiusMeters;

            double periapsis =
                semiMajorAxis *
                (1.0 -
                 eccentricity) -
                KerbinRadiusMeters;

            if (!IsFinite(
                    apoapsis) ||
                !IsFinite(
                    periapsis))
            {
                return output;
            }

            output.IsValid =
                true;

            output.ApoapsisMeters =
                apoapsis;

            output.PeriapsisMeters =
                periapsis;

            return output;
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

        private sealed class OrbitElements
        {
            public bool IsValid { get; set; }

            public double ApoapsisMeters { get; set; }

            public double PeriapsisMeters { get; set; }
        }
    }
}
