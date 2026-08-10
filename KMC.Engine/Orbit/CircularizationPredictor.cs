using System;

namespace KMC.Engine.Orbit
{
    /// <summary>
    /// Engine-owned migration of the legacy MissionPlanner circularization
    /// prediction math.
    ///
    /// Build 10.1 preserves legacy equations and thresholds first.
    /// Safety/cutoff decisions are NOT made here.
    /// </summary>
    internal sealed class CircularizationPredictor
    {
        private const double KerbinRadiusMeters =
            600000.0;

        private const double KerbinGravitationalParameter =
            3.5316e12;

        private const double StandardGravity =
            9.80665;

        private const double ShutdownResponseSeconds =
            0.55;

        private const double RemainingDeltaVCutoffMetersPerSecond =
            1.25;

        private double _initialCircularizationDeltaV =
            double.NaN;

        public void Reset()
        {
            _initialCircularizationDeltaV =
                double.NaN;
        }

        public CircularizationPredictionModel Calculate(
            OrbitTelemetryState telemetry,
            double targetOrbitMeters,
            bool ascentHandoffObserved)
        {
            CircularizationPredictionModel result =
                new CircularizationPredictionModel
                {
                    ThrustEvidence =
                        OrbitPredictionThrustEvidence
                            .FlightPacketVesselThrustSummary,

                    TargetOrbitMeters =
                        targetOrbitMeters,

                    Status =
                        "PREDICTION UNAVAILABLE"
                };

            if (telemetry == null ||
                !telemetry.Available)
            {
                return result;
            }

            double radius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    telemetry.AltitudeMeters);

            double targetRadius =
                KerbinRadiusMeters +
                Math.Max(
                    0.0,
                    targetOrbitMeters);

            result.CurrentRadiusMeters =
                radius;

            result.TargetRadiusMeters =
                targetRadius;

            if (radius <= 0.0 ||
                targetRadius <= 0.0)
            {
                result.Status =
                    "INVALID ORBIT RADIUS";

                return result;
            }

            double currentSpeed =
                Math.Max(
                    0.0,
                    telemetry
                        .OrbitalSpeedMetersPerSecond);

            double radialSpeed =
                telemetry
                    .VerticalSpeedMetersPerSecond;

            result.CurrentOrbitalSpeedMetersPerSecond =
                currentSpeed;

            if (!IsFinite(currentSpeed) ||
                !IsFinite(radialSpeed))
            {
                result.Status =
                    "INVALID ORBIT VELOCITY";

                return result;
            }

            radialSpeed =
                Clamp(
                    radialSpeed,
                    -currentSpeed,
                    currentSpeed);

            double tangentialSpeedSquared =
                Math.Max(
                    0.0,
                    currentSpeed *
                    currentSpeed -
                    radialSpeed *
                    radialSpeed);

            double tangentialSpeed =
                Math.Sqrt(
                    tangentialSpeedSquared);

            result.RadialSpeedMetersPerSecond =
                radialSpeed;

            result.TangentialSpeedMetersPerSecond =
                tangentialSpeed;

            double currentEnergy =
                0.5 *
                currentSpeed *
                currentSpeed -
                KerbinGravitationalParameter /
                radius;

            double targetEnergy =
                -KerbinGravitationalParameter /
                (2.0 *
                 targetRadius);

            double targetSpeedSquared =
                2.0 *
                (targetEnergy +
                 KerbinGravitationalParameter /
                 radius);

            result.CurrentSpecificEnergyJoulesPerKilogram =
                currentEnergy;

            result.TargetSpecificEnergyJoulesPerKilogram =
                targetEnergy;

            result.EnergyErrorJoulesPerKilogram =
                targetEnergy -
                currentEnergy;

            if (targetSpeedSquared < 0.0)
            {
                result.Status =
                    "TARGET SPEED INVALID";

                return result;
            }

            double targetSpeed =
                Math.Sqrt(
                    targetSpeedSquared);

            double remainingDeltaV =
                Math.Max(
                    0.0,
                    targetSpeed -
                    currentSpeed);

            double burnTime =
                EstimateBurnTime(
                    telemetry,
                    remainingDeltaV);

            if (!IsFinite(burnTime))
            {
                burnTime =
                    0.0;
            }

            double ignitionIn =
                IsFinite(
                    telemetry
                        .TimeToApoapsisSeconds)
                    ? telemetry
                        .TimeToApoapsisSeconds -
                      burnTime /
                      2.0
                    : double.NaN;

            double throttleFraction =
                DetermineCircularizationThrottleFraction(
                    remainingDeltaV);

            double shutdownDeltaV =
                EstimateShutdownDeltaV(
                    telemetry,
                    throttleFraction);

            OrbitalState predicted =
                PredictOrbitAfterProgradeDeltaV(
                    radius,
                    radialSpeed,
                    tangentialSpeed,
                    shutdownDeltaV);

            result.TargetSpeedMetersPerSecond =
                targetSpeed;

            result.RemainingDeltaVMetersPerSecond =
                remainingDeltaV;

            result.BurnTimeSeconds =
                burnTime;

            result.IgnitionInSeconds =
                ignitionIn;

            result.RecommendedThrottleFraction =
                throttleFraction;

            result.ShutdownResponseDeltaVMetersPerSecond =
                shutdownDeltaV;

            if (!predicted.IsValid)
            {
                result.Status =
                    "SHUTDOWN ORBIT INVALID";

                return result;
            }

            result.Available =
                true;

            result.PredictedEnergyErrorJoulesPerKilogram =
                targetEnergy -
                predicted.SpecificEnergy;

            result.PredictedApoapsisMeters =
                predicted.ApoapsisMeters;

            result.PredictedPeriapsisMeters =
                predicted.PeriapsisMeters;

            result.PredictedOrbitErrorMeters =
                Math.Max(
                    Math.Abs(
                        predicted.ApoapsisMeters -
                        targetOrbitMeters),
                    Math.Abs(
                        predicted.PeriapsisMeters -
                        targetOrbitMeters));

            if (!IsFinite(
                    _initialCircularizationDeltaV) &&
                ascentHandoffObserved &&
                remainingDeltaV >
                    0.1)
            {
                _initialCircularizationDeltaV =
                    remainingDeltaV;
            }

            result.InitialDeltaVMetersPerSecond =
                IsFinite(
                    _initialCircularizationDeltaV)
                    ? _initialCircularizationDeltaV
                    : remainingDeltaV;

            result.BurnCompletionPercent =
                result.InitialDeltaVMetersPerSecond >
                    0.1
                    ? Clamp(
                        100.0 *
                        (1.0 -
                         remainingDeltaV /
                         result.InitialDeltaVMetersPerSecond),
                        0.0,
                        100.0)
                    : 100.0;

            if (!ascentHandoffObserved)
            {
                result.Status =
                    "ASCENT ACTIVE";
            }
            else if (remainingDeltaV <=
                     RemainingDeltaVCutoffMetersPerSecond)
            {
                result.Status =
                    "DELTA V SATISFIED";
            }
            else if (IsFinite(ignitionIn) &&
                     ignitionIn <= 0.0)
            {
                result.Status =
                    "IGNITION DUE";
            }
            else
            {
                result.Status =
                    "COAST PREDICTION";
            }

            return result;
        }

        private static double EstimateShutdownDeltaV(
            OrbitTelemetryState telemetry,
            double throttleFraction)
        {
            double massKilograms =
                Math.Max(
                    0.0,
                    telemetry.VesselMassTonnes) *
                1000.0;

            double thrustKilonewtons =
                Math.Max(
                    telemetry
                        .CurrentThrustKilonewtons,
                    telemetry
                        .MaximumThrustKilonewtons *
                    throttleFraction);

            double thrustNewtons =
                Math.Max(
                    0.0,
                    thrustKilonewtons) *
                1000.0;

            if (massKilograms <= 0.0 ||
                thrustNewtons <= 0.0)
            {
                return 0.0;
            }

            double acceleration =
                thrustNewtons /
                massKilograms;

            return
                acceleration *
                ShutdownResponseSeconds;
        }

        private static OrbitalState
            PredictOrbitAfterProgradeDeltaV(
                double radius,
                double radialSpeed,
                double tangentialSpeed,
                double progradeDeltaV)
        {
            OrbitalState result =
                new OrbitalState();

            double predictedTangentialSpeed =
                Math.Max(
                    0.0,
                    tangentialSpeed +
                    Math.Max(
                        0.0,
                        progradeDeltaV));

            double speedSquared =
                radialSpeed *
                radialSpeed +
                predictedTangentialSpeed *
                predictedTangentialSpeed;

            double energy =
                0.5 *
                speedSquared -
                KerbinGravitationalParameter /
                radius;

            if (energy >= 0.0)
            {
                return result;
            }

            double angularMomentum =
                radius *
                predictedTangentialSpeed;

            double semiMajorAxis =
                -KerbinGravitationalParameter /
                (2.0 *
                 energy);

            double eccentricityTerm =
                1.0 +
                2.0 *
                energy *
                angularMomentum *
                angularMomentum /
                (KerbinGravitationalParameter *
                 KerbinGravitationalParameter);

            double eccentricity =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        eccentricityTerm));

            double apoapsisRadius =
                semiMajorAxis *
                (1.0 +
                 eccentricity);

            double periapsisRadius =
                semiMajorAxis *
                (1.0 -
                 eccentricity);

            result.IsValid =
                IsFinite(
                    apoapsisRadius) &&
                IsFinite(
                    periapsisRadius);

            result.SpecificEnergy =
                energy;

            result.ApoapsisMeters =
                apoapsisRadius -
                KerbinRadiusMeters;

            result.PeriapsisMeters =
                periapsisRadius -
                KerbinRadiusMeters;

            return result;
        }

        private static double
            DetermineCircularizationThrottleFraction(
                double remainingDeltaV)
        {
            if (remainingDeltaV > 45.0)
            {
                return 1.00;
            }

            if (remainingDeltaV > 18.0)
            {
                return 0.60;
            }

            if (remainingDeltaV > 6.0)
            {
                return 0.30;
            }

            if (remainingDeltaV >
                RemainingDeltaVCutoffMetersPerSecond)
            {
                return 0.10;
            }

            return 0.0;
        }

        private static double EstimateBurnTime(
            OrbitTelemetryState telemetry,
            double deltaV)
        {
            if (deltaV <= 0.0)
            {
                return 0.0;
            }

            double massKilograms =
                Math.Max(
                    0.0,
                    telemetry.VesselMassTonnes) *
                1000.0;

            double thrustNewtons =
                Math.Max(
                    telemetry
                        .CurrentThrustKilonewtons,
                    telemetry
                        .MaximumThrustKilonewtons) *
                1000.0;

            double specificImpulse =
                telemetry
                    .AverageSpecificImpulseSeconds;

            if (massKilograms > 0.0 &&
                thrustNewtons > 0.0 &&
                specificImpulse > 1.0)
            {
                double exhaustVelocity =
                    specificImpulse *
                    StandardGravity;

                double finalMass =
                    massKilograms /
                    Math.Exp(
                        deltaV /
                        exhaustVelocity);

                double propellantMass =
                    Math.Max(
                        0.0,
                        massKilograms -
                        finalMass);

                double massFlow =
                    thrustNewtons /
                    exhaustVelocity;

                if (massFlow > 0.0)
                {
                    return
                        propellantMass /
                        massFlow;
                }
            }

            if (massKilograms > 0.0 &&
                thrustNewtons > 0.0)
            {
                double acceleration =
                    thrustNewtons /
                    massKilograms;

                if (acceleration > 0.0)
                {
                    return
                        deltaV /
                        acceleration;
                }
            }

            return double.NaN;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return
                Math.Max(
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

        private sealed class OrbitalState
        {
            public bool IsValid { get; set; }

            public double SpecificEnergy { get; set; }

            public double ApoapsisMeters { get; set; }

            public double PeriapsisMeters { get; set; }
        }
    }
}
