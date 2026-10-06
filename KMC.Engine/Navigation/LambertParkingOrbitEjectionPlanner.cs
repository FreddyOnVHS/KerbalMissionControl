using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Converts a parent-frame Lambert departure excess vector into a preview
    /// of the local impulsive burn from an elliptic parking orbit.
    ///
    /// The method is two-body and KSP-independent. It assumes the parking-orbit
    /// element axes and the Lambert parent-frame axes share the canonical inertial
    /// orientation supplied by the adapter. Runtime KSP validation is therefore
    /// required before this result can become maneuver authority.
    /// </summary>
    public static class LambertParkingOrbitEjectionPlanner
    {
        private const double MaximumParkingEccentricity = 0.05;
        private const double DirectionTolerance = 1e-10;
        private const int RadiusIterations = 8;

        public static bool TryCalculate(
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            CelestialBodyState originBody,
            Vector3d departureExcessVelocity,
            double departureUniversalTimeSeconds,
            out LambertParkingOrbitEjectionSolution solution)
        {
            solution = null;

            if (parkingOrbit == null || originBody == null ||
                !parkingOrbit.HasFiniteGeometry ||
                !departureExcessVelocity.IsFinite ||
                !Vector3d.Finite(departureUniversalTimeSeconds))
                return false;

            if (!IsFinitePositive(originBody.GravParameter) ||
                !IsFinitePositive(referenceBodyRadiusMeters) ||
                !IsFinitePositive(parkingOrbit.SemiMajorAxisMeters))
                return false;

            if (parkingOrbit.Eccentricity < 0.0 ||
                parkingOrbit.Eccentricity >= 1.0 ||
                parkingOrbit.Eccentricity > MaximumParkingEccentricity)
                return false;

            if (!MatchesOptionalFrame(
                    parkingOrbit.ReferenceBodyName,
                    originBody.Name))
                return false;

            double vinf =
                departureExcessVelocity.Magnitude;

            if (!IsFinitePositive(vinf))
                return false;

            double mu = originBody.GravParameter;
            double a = parkingOrbit.SemiMajorAxisMeters;
            double minimumParkingRadius =
                a * (1.0 - parkingOrbit.Eccentricity);

            if (!IsFinitePositive(minimumParkingRadius) ||
                minimumParkingRadius <= referenceBodyRadiusMeters)
                return false;

            StateVector departureParkingState;
            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    mu,
                    departureUniversalTimeSeconds,
                    out departureParkingState))
                return false;

            Vector3d parkingNormal;
            if (!TryNormalize(
                    Vector3d.Cross(
                        departureParkingState.Position,
                        departureParkingState.Velocity),
                    out parkingNormal))
                return false;

            Vector3d vinfDirection;
            if (!TryNormalize(
                    departureExcessVelocity,
                    out vinfDirection))
                return false;

            Vector3d projectedVinf =
                vinfDirection -
                parkingNormal *
                Vector3d.Dot(
                    vinfDirection,
                    parkingNormal);

            double projectionMagnitude =
                projectedVinf.Magnitude;

            if (!IsFinitePositive(projectionMagnitude))
                return false;

            Vector3d projectedDirection;
            if (!TryNormalize(
                    projectedVinf,
                    out projectedDirection))
                return false;

            Vector3d inPlanePerpendicular;
            if (!TryNormalize(
                    Vector3d.Cross(
                        parkingNormal,
                        projectedDirection),
                    out inPlanePerpendicular))
                return false;

            Vector3d periapsisBasis;
            Vector3d transverseBasis;
            if (!TryOrbitPlaneBasis(
                    parkingOrbit,
                    out periapsisBasis,
                    out transverseBasis))
                return false;

            Candidate first;
            Candidate second;

            bool hasFirst =
                TryCandidate(
                    parkingOrbit,
                    referenceBodyRadiusMeters,
                    mu,
                    departureUniversalTimeSeconds,
                    departureExcessVelocity,
                    vinfDirection,
                    vinf,
                    projectedDirection,
                    inPlanePerpendicular,
                    projectionMagnitude,
                    periapsisBasis,
                    transverseBasis,
                    1.0,
                    out first);

            bool hasSecond =
                TryCandidate(
                    parkingOrbit,
                    referenceBodyRadiusMeters,
                    mu,
                    departureUniversalTimeSeconds,
                    departureExcessVelocity,
                    vinfDirection,
                    vinf,
                    projectedDirection,
                    inPlanePerpendicular,
                    projectionMagnitude,
                    periapsisBasis,
                    transverseBasis,
                    -1.0,
                    out second);

            if (!hasFirst && !hasSecond)
                return false;

            Candidate best =
                hasFirst && hasSecond
                    ? Better(first, second)
                    : hasFirst
                        ? first
                        : second;

            solution =
                new LambertParkingOrbitEjectionSolution
                {
                    BurnUniversalTimeSeconds =
                        best.BurnUniversalTimeSeconds,
                    WindowOffsetSeconds =
                        best.BurnUniversalTimeSeconds -
                        departureUniversalTimeSeconds,
                    ParkingRadiusMeters =
                        best.ParkingRadiusMeters,
                    ParkingAltitudeMeters =
                        best.ParkingRadiusMeters -
                        referenceBodyRadiusMeters,
                    ParkingSpeedMetersPerSecond =
                        best.ParkingSpeedMetersPerSecond,
                    HyperbolicExcessSpeedMetersPerSecond =
                        vinf,
                    HyperbolicPeriapsisSpeedMetersPerSecond =
                        best.HyperbolicPeriapsisSpeedMetersPerSecond,
                    HyperbolicEccentricity =
                        best.HyperbolicEccentricity,
                    AsymptoteAngleDegrees =
                        best.AsymptoteAngleRadians *
                        180.0 / Math.PI,
                    ProgradeDeltaVMetersPerSecond =
                        best.ProgradeDeltaVMetersPerSecond,
                    NormalDeltaVMetersPerSecond =
                        best.NormalDeltaVMetersPerSecond,
                    RadialDeltaVMetersPerSecond =
                        best.RadialDeltaVMetersPerSecond,
                    TotalDeltaVMetersPerSecond =
                        best.TotalDeltaVMetersPerSecond,
                    GeometryResidualDegrees =
                        best.GeometryResidualRadians *
                        180.0 / Math.PI
                };

            return true;
        }

        private static bool TryCandidate(
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            double mu,
            double departureUniversalTimeSeconds,
            Vector3d departureExcessVelocity,
            Vector3d vinfDirection,
            double vinf,
            Vector3d projectedDirection,
            Vector3d inPlanePerpendicular,
            double projectionMagnitude,
            Vector3d periapsisBasis,
            Vector3d transverseBasis,
            double branchSign,
            out Candidate candidate)
        {
            candidate = null;

            double radius =
                parkingOrbit.SemiMajorAxisMeters;

            Vector3d burnDirection =
                new Vector3d();

            double asymptoteAngle =
                double.NaN;

            double hyperbolicEccentricity =
                double.NaN;

            for (int iteration = 0;
                iteration < RadiusIterations;
                iteration++)
            {
                hyperbolicEccentricity =
                    1.0 +
                    (radius * vinf * vinf / mu);

                if (!IsFinitePositive(hyperbolicEccentricity) ||
                    hyperbolicEccentricity <= 1.0)
                    return false;

                double cosine =
                    -1.0 / hyperbolicEccentricity;

                double alpha =
                    cosine / projectionMagnitude;

                if (!Vector3d.Finite(alpha) ||
                    Math.Abs(alpha) > 1.0 + DirectionTolerance)
                    return false;

                alpha =
                    Clamp(alpha, -1.0, 1.0);

                double beta =
                    Math.Sqrt(
                        Math.Max(
                            0.0,
                            1.0 - alpha * alpha));

                burnDirection =
                    projectedDirection * alpha +
                    inPlanePerpendicular *
                    (branchSign * beta);

                Vector3d normalizedBurnDirection;
                if (!TryNormalize(
                        burnDirection,
                        out normalizedBurnDirection))
                    return false;

                burnDirection =
                    normalizedBurnDirection;

                double trueAnomaly =
                    Math.Atan2(
                        Vector3d.Dot(
                            burnDirection,
                            transverseBasis),
                        Vector3d.Dot(
                            burnDirection,
                            periapsisBasis));

                double denominator =
                    1.0 +
                    parkingOrbit.Eccentricity *
                    Math.Cos(trueAnomaly);

                if (denominator <= 0.0 ||
                    !Vector3d.Finite(denominator))
                    return false;

                double nextRadius =
                    parkingOrbit.SemiMajorAxisMeters *
                    (1.0 -
                        parkingOrbit.Eccentricity *
                        parkingOrbit.Eccentricity) /
                    denominator;

                if (!IsFinitePositive(nextRadius) ||
                    nextRadius <= referenceBodyRadiusMeters)
                    return false;

                if (Math.Abs(nextRadius - radius) <=
                    Math.Max(1e-6, nextRadius * 1e-12))
                {
                    radius = nextRadius;
                    break;
                }

                radius = nextRadius;
            }

            hyperbolicEccentricity =
                1.0 +
                (radius * vinf * vinf / mu);

            if (hyperbolicEccentricity <= 1.0)
                return false;

            double asymptoteCosine =
                -1.0 / hyperbolicEccentricity;

            asymptoteAngle =
                Math.Acos(
                    Clamp(
                        asymptoteCosine,
                        -1.0,
                        1.0));

            double finalAlpha =
                asymptoteCosine /
                projectionMagnitude;

            if (!Vector3d.Finite(finalAlpha) ||
                Math.Abs(finalAlpha) >
                    1.0 + DirectionTolerance)
                return false;

            finalAlpha =
                Clamp(finalAlpha, -1.0, 1.0);

            double finalBeta =
                Math.Sqrt(
                    Math.Max(
                        0.0,
                        1.0 - finalAlpha * finalAlpha));

            burnDirection =
                projectedDirection * finalAlpha +
                inPlanePerpendicular *
                (branchSign * finalBeta);

            if (!TryNormalize(
                    burnDirection,
                    out burnDirection))
                return false;

            double targetTrueAnomaly =
                Math.Atan2(
                    Vector3d.Dot(
                        burnDirection,
                        transverseBasis),
                    Vector3d.Dot(
                        burnDirection,
                        periapsisBasis));

            double targetMeanAnomaly;
            if (!TryMeanAnomalyFromTrueAnomaly(
                    targetTrueAnomaly,
                    parkingOrbit.Eccentricity,
                    out targetMeanAnomaly))
                return false;

            double meanMotion =
                Math.Sqrt(
                    mu /
                    (parkingOrbit.SemiMajorAxisMeters *
                     parkingOrbit.SemiMajorAxisMeters *
                     parkingOrbit.SemiMajorAxisMeters));

            if (!IsFinitePositive(meanMotion))
                return false;

            double meanAtDeparture =
                parkingOrbit.MeanAnomalyAtEpochRadians +
                meanMotion *
                (departureUniversalTimeSeconds -
                 parkingOrbit.EpochUniversalTimeSeconds);

            double meanDelta =
                NormalizeSignedRadians(
                    targetMeanAnomaly -
                    meanAtDeparture);

            double burnUt =
                departureUniversalTimeSeconds +
                meanDelta / meanMotion;

            if (!Vector3d.Finite(burnUt))
                return false;

            StateVector burnState;
            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    mu,
                    burnUt,
                    out burnState))
                return false;

            double actualRadius =
                burnState.Position.Magnitude;

            if (!IsFinitePositive(actualRadius) ||
                actualRadius <= referenceBodyRadiusMeters)
                return false;

            Vector3d radialDirection;
            if (!TryNormalize(
                    burnState.Position,
                    out radialDirection))
                return false;

            double actualHyperbolicEccentricity =
                1.0 +
                (actualRadius * vinf * vinf / mu);

            if (actualHyperbolicEccentricity <= 1.0)
                return false;

            double actualAsymptoteAngle =
                Math.Acos(
                    Clamp(
                        -1.0 /
                        actualHyperbolicEccentricity,
                        -1.0,
                        1.0));

            double achievedRadiusAsymptoteAngle =
                Math.Acos(
                    Clamp(
                        Vector3d.Dot(
                            radialDirection,
                            vinfDirection),
                        -1.0,
                        1.0));

            double residual =
                Math.Abs(
                    achievedRadiusAsymptoteAngle -
                    actualAsymptoteAngle);

            Vector3d hyperbolicTangent =
                vinfDirection -
                radialDirection *
                Vector3d.Dot(
                    vinfDirection,
                    radialDirection);

            if (!TryNormalize(
                    hyperbolicTangent,
                    out hyperbolicTangent))
                return false;

            double hyperbolicPeriapsisSpeed =
                Math.Sqrt(
                    vinf * vinf +
                    2.0 * mu / actualRadius);

            if (!IsFinitePositive(
                    hyperbolicPeriapsisSpeed))
                return false;

            Vector3d desiredVelocity =
                hyperbolicTangent *
                hyperbolicPeriapsisSpeed;

            Vector3d deltaV =
                desiredVelocity -
                burnState.Velocity;

            double totalDeltaV =
                deltaV.Magnitude;

            if (!IsFinitePositive(totalDeltaV))
                return false;

            Vector3d progradeDirection;
            Vector3d normalDirection;
            Vector3d nodeRadialDirection;

            if (!TryNormalize(
                    burnState.Velocity,
                    out progradeDirection) ||
                !TryNormalize(
                    Vector3d.Cross(
                        burnState.Position,
                        burnState.Velocity),
                    out normalDirection) ||
                !TryNormalize(
                    Vector3d.Cross(
                        progradeDirection,
                        normalDirection),
                    out nodeRadialDirection))
                return false;

            candidate =
                new Candidate
                {
                    BurnUniversalTimeSeconds =
                        burnUt,
                    ParkingRadiusMeters =
                        actualRadius,
                    ParkingSpeedMetersPerSecond =
                        burnState.Velocity.Magnitude,
                    HyperbolicPeriapsisSpeedMetersPerSecond =
                        hyperbolicPeriapsisSpeed,
                    HyperbolicEccentricity =
                        actualHyperbolicEccentricity,
                    AsymptoteAngleRadians =
                        actualAsymptoteAngle,
                    ProgradeDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            progradeDirection),
                    NormalDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            normalDirection),
                    RadialDeltaVMetersPerSecond =
                        Vector3d.Dot(
                            deltaV,
                            nodeRadialDirection),
                    TotalDeltaVMetersPerSecond =
                        totalDeltaV,
                    GeometryResidualRadians =
                        residual
                };

            return true;
        }

        private static Candidate Better(
            Candidate first,
            Candidate second)
        {
            double tolerance =
                1e-10 *
                Math.Max(
                    1.0,
                    Math.Max(
                        first.TotalDeltaVMetersPerSecond,
                        second.TotalDeltaVMetersPerSecond));

            if (first.TotalDeltaVMetersPerSecond <
                second.TotalDeltaVMetersPerSecond -
                tolerance)
                return first;

            if (second.TotalDeltaVMetersPerSecond <
                first.TotalDeltaVMetersPerSecond -
                tolerance)
                return second;

            return
                Math.Abs(first.BurnUniversalTimeSeconds) <=
                Math.Abs(second.BurnUniversalTimeSeconds)
                    ? first
                    : second;
        }

        private static bool TryOrbitPlaneBasis(
            OrbitalElements orbit,
            out Vector3d periapsisBasis,
            out Vector3d transverseBasis)
        {
            periapsisBasis =
                RotatePlaneVector(
                    orbit,
                    1.0,
                    0.0);

            transverseBasis =
                RotatePlaneVector(
                    orbit,
                    0.0,
                    1.0);

            return
                TryNormalize(
                    periapsisBasis,
                    out periapsisBasis) &&
                TryNormalize(
                    transverseBasis,
                    out transverseBasis);
        }

        private static Vector3d RotatePlaneVector(
            OrbitalElements orbit,
            double x,
            double y)
        {
            double argument =
                (orbit.ArgumentOfPeriapsisDegrees % 360.0) *
                Math.PI / 180.0;

            double inclination =
                (orbit.InclinationDegrees % 360.0) *
                Math.PI / 180.0;

            double node =
                (orbit.LongitudeOfAscendingNodeDegrees % 360.0) *
                Math.PI / 180.0;

            double x1 =
                Math.Cos(argument) * x -
                Math.Sin(argument) * y;

            double y1 =
                Math.Sin(argument) * x +
                Math.Cos(argument) * y;

            double y2 =
                Math.Cos(inclination) *
                y1;

            double z2 =
                Math.Sin(inclination) *
                y1;

            return
                new Vector3d(
                    Math.Cos(node) * x1 -
                    Math.Sin(node) * y2,
                    Math.Sin(node) * x1 +
                    Math.Cos(node) * y2,
                    z2);
        }

        private static bool TryMeanAnomalyFromTrueAnomaly(
            double trueAnomaly,
            double eccentricity,
            out double meanAnomaly)
        {
            meanAnomaly = double.NaN;

            if (!Vector3d.Finite(trueAnomaly) ||
                !Vector3d.Finite(eccentricity) ||
                eccentricity < 0.0 ||
                eccentricity >= 1.0)
                return false;

            double eccentricAnomaly =
                2.0 *
                Math.Atan2(
                    Math.Sqrt(1.0 - eccentricity) *
                    Math.Sin(trueAnomaly * 0.5),
                    Math.Sqrt(1.0 + eccentricity) *
                    Math.Cos(trueAnomaly * 0.5));

            meanAnomaly =
                eccentricAnomaly -
                eccentricity *
                Math.Sin(eccentricAnomaly);

            return
                Vector3d.Finite(meanAnomaly);
        }

        private static double NormalizeSignedRadians(
            double radians)
        {
            double twoPi =
                2.0 * Math.PI;

            radians %= twoPi;

            if (radians > Math.PI)
                radians -= twoPi;

            if (radians < -Math.PI)
                radians += twoPi;

            return radians;
        }

        private static bool TryNormalize(
            Vector3d value,
            out Vector3d normalized)
        {
            normalized =
                new Vector3d();

            double magnitude =
                value.Magnitude;

            if (!IsFinitePositive(magnitude))
                return false;

            normalized =
                value *
                (1.0 / magnitude);

            return
                normalized.IsFinite;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                Vector3d.Finite(value) &&
                value > 0.0;
        }

        private static bool MatchesOptionalFrame(
            string referenceName,
            string expectedName)
        {
            return
                string.IsNullOrWhiteSpace(referenceName) ||
                string.Equals(
                    referenceName,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase);
        }

        private sealed class Candidate
        {
            public double BurnUniversalTimeSeconds;
            public double ParkingRadiusMeters;
            public double ParkingSpeedMetersPerSecond;
            public double HyperbolicPeriapsisSpeedMetersPerSecond;
            public double HyperbolicEccentricity;
            public double AsymptoteAngleRadians;
            public double ProgradeDeltaVMetersPerSecond;
            public double NormalDeltaVMetersPerSecond;
            public double RadialDeltaVMetersPerSecond;
            public double TotalDeltaVMetersPerSecond;
            public double GeometryResidualRadians;
        }
    }
}
