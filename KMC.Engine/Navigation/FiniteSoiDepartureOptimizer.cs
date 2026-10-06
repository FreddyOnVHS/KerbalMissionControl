using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Generic bounded pattern search that improves the finite-SOI handoff of an
    /// already-selected Lambert parking-orbit ejection.
    ///
    /// Variables:
    ///   burn UT, prograde DV, normal DV, radial DV.
    ///
    /// Objective:
    ///   FiniteSoiDepartureAssessment.NormalizedStateError.
    ///
    /// The search contains no body names or stock-system constants.
    /// </summary>
    public static class FiniteSoiDepartureOptimizer
    {
        private const int MaximumIterations = 36;
        private const double MinimumUtStepSeconds = 0.50;
        private const double MinimumDvStepMetersPerSecond = 0.02;
        private const double ScoreRelativeTolerance = 1e-12;

        public static bool TryOptimize(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            double parentGravParameter,
            out FiniteSoiDepartureCorrectionResult result)
        {
            result = null;

            if (transfer == null ||
                seedEjection == null ||
                parkingOrbit == null ||
                originBody == null ||
                !FinitePositive(originBody.GravParameter) ||
                !FinitePositive(originBody.SoiRadiusMeters) ||
                !FinitePositive(parentGravParameter))
                return false;

            FiniteSoiDepartureAssessment seedAssessment;

            if (!FiniteSoiDepartureEvaluator.TryEvaluate(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    out seedAssessment))
                return false;

            double parkingPeriod =
                ResolveParkingPeriodSeconds(
                    parkingOrbit,
                    originBody.GravParameter);

            double utStep =
                Clamp(
                    parkingPeriod / 32.0,
                    15.0,
                    180.0);

            double dvScale =
                Math.Max(
                    1.0,
                    seedEjection.TotalDeltaVMetersPerSecond);

            double dvStep =
                Clamp(
                    dvScale * 0.015,
                    2.0,
                    30.0);

            double utHalfRange =
                Math.Max(
                    120.0,
                    parkingPeriod * 0.45);

            double componentHalfRange =
                Math.Max(
                    60.0,
                    dvScale * 0.25);

            double minimumBurnUt =
                seedEjection.BurnUniversalTimeSeconds -
                utHalfRange;

            double maximumBurnUt =
                seedEjection.BurnUniversalTimeSeconds +
                utHalfRange;

            double minimumPrograde =
                seedEjection.ProgradeDeltaVMetersPerSecond -
                componentHalfRange;

            double maximumPrograde =
                seedEjection.ProgradeDeltaVMetersPerSecond +
                componentHalfRange;

            double minimumNormal =
                seedEjection.NormalDeltaVMetersPerSecond -
                componentHalfRange;

            double maximumNormal =
                seedEjection.NormalDeltaVMetersPerSecond +
                componentHalfRange;

            double minimumRadial =
                seedEjection.RadialDeltaVMetersPerSecond -
                componentHalfRange;

            double maximumRadial =
                seedEjection.RadialDeltaVMetersPerSecond +
                componentHalfRange;

            LambertParkingOrbitEjectionSolution bestEjection =
                CloneWith(
                    seedEjection,
                    seedEjection.BurnUniversalTimeSeconds,
                    seedEjection.ProgradeDeltaVMetersPerSecond,
                    seedEjection.NormalDeltaVMetersPerSecond,
                    seedEjection.RadialDeltaVMetersPerSecond);

            FiniteSoiDepartureAssessment bestAssessment =
                seedAssessment;

            int evaluations = 1;
            int iterations = 0;

            for (int iteration = 0;
                iteration < MaximumIterations;
                iteration++)
            {
                iterations = iteration + 1;
                bool improved = false;

                improved |= TryNeighbor(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    bestEjection.BurnUniversalTimeSeconds + utStep,
                    bestEjection.ProgradeDeltaVMetersPerSecond,
                    bestEjection.NormalDeltaVMetersPerSecond,
                    bestEjection.RadialDeltaVMetersPerSecond,
                    minimumBurnUt,
                    maximumBurnUt,
                    minimumPrograde,
                    maximumPrograde,
                    minimumNormal,
                    maximumNormal,
                    minimumRadial,
                    maximumRadial,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryNeighbor(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    bestEjection.BurnUniversalTimeSeconds - utStep,
                    bestEjection.ProgradeDeltaVMetersPerSecond,
                    bestEjection.NormalDeltaVMetersPerSecond,
                    bestEjection.RadialDeltaVMetersPerSecond,
                    minimumBurnUt,
                    maximumBurnUt,
                    minimumPrograde,
                    maximumPrograde,
                    minimumNormal,
                    maximumNormal,
                    minimumRadial,
                    maximumRadial,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryDvAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    0,
                    dvStep,
                    minimumBurnUt,
                    maximumBurnUt,
                    minimumPrograde,
                    maximumPrograde,
                    minimumNormal,
                    maximumNormal,
                    minimumRadial,
                    maximumRadial,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryDvAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    1,
                    dvStep,
                    minimumBurnUt,
                    maximumBurnUt,
                    minimumPrograde,
                    maximumPrograde,
                    minimumNormal,
                    maximumNormal,
                    minimumRadial,
                    maximumRadial,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryDvAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    2,
                    dvStep,
                    minimumBurnUt,
                    maximumBurnUt,
                    minimumPrograde,
                    maximumPrograde,
                    minimumNormal,
                    maximumNormal,
                    minimumRadial,
                    maximumRadial,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                if (!improved)
                {
                    utStep *= 0.5;
                    dvStep *= 0.5;
                }

                if (utStep < MinimumUtStepSeconds &&
                    dvStep < MinimumDvStepMetersPerSecond)
                    break;
            }

            result =
                new FiniteSoiDepartureCorrectionResult(
                    seedEjection,
                    seedAssessment,
                    bestEjection,
                    bestAssessment,
                    iterations,
                    evaluations);

            return true;
        }

        private static bool TryDvAxis(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seed,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            double parentMu,
            int axis,
            double step,
            double minimumBurnUt,
            double maximumBurnUt,
            double minimumPrograde,
            double maximumPrograde,
            double minimumNormal,
            double maximumNormal,
            double minimumRadial,
            double maximumRadial,
            ref LambertParkingOrbitEjectionSolution bestEjection,
            ref FiniteSoiDepartureAssessment bestAssessment,
            ref int evaluations)
        {
            bool improved = false;

            for (int signIndex = 0; signIndex < 2; signIndex++)
            {
                double signedStep =
                    signIndex == 0 ? step : -step;

                double prograde =
                    bestEjection.ProgradeDeltaVMetersPerSecond;

                double normal =
                    bestEjection.NormalDeltaVMetersPerSecond;

                double radial =
                    bestEjection.RadialDeltaVMetersPerSecond;

                if (axis == 0)
                    prograde += signedStep;
                else if (axis == 1)
                    normal += signedStep;
                else
                    radial += signedStep;

                if (TryNeighbor(
                        transfer,
                        seed,
                        parkingOrbit,
                        originBody,
                        parentMu,
                        bestEjection.BurnUniversalTimeSeconds,
                        prograde,
                        normal,
                        radial,
                        minimumBurnUt,
                        maximumBurnUt,
                        minimumPrograde,
                        maximumPrograde,
                        minimumNormal,
                        maximumNormal,
                        minimumRadial,
                        maximumRadial,
                        ref bestEjection,
                        ref bestAssessment,
                        ref evaluations))
                {
                    improved = true;
                }
            }

            return improved;
        }

        private static bool TryNeighbor(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seed,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            double parentMu,
            double burnUt,
            double prograde,
            double normal,
            double radial,
            double minimumBurnUt,
            double maximumBurnUt,
            double minimumPrograde,
            double maximumPrograde,
            double minimumNormal,
            double maximumNormal,
            double minimumRadial,
            double maximumRadial,
            ref LambertParkingOrbitEjectionSolution bestEjection,
            ref FiniteSoiDepartureAssessment bestAssessment,
            ref int evaluations)
        {
            if (burnUt < minimumBurnUt ||
                burnUt > maximumBurnUt ||
                prograde < minimumPrograde ||
                prograde > maximumPrograde ||
                normal < minimumNormal ||
                normal > maximumNormal ||
                radial < minimumRadial ||
                radial > maximumRadial)
                return false;

            LambertParkingOrbitEjectionSolution trial =
                CloneWith(
                    seed,
                    burnUt,
                    prograde,
                    normal,
                    radial);

            FiniteSoiDepartureAssessment assessment;

            if (!FiniteSoiDepartureEvaluator.TryEvaluate(
                    transfer,
                    trial,
                    parkingOrbit,
                    originBody,
                    parentMu,
                    out assessment))
                return false;

            evaluations++;

            if (!IsBetter(
                    trial,
                    assessment,
                    bestEjection,
                    bestAssessment))
                return false;

            bestEjection = trial;
            bestAssessment = assessment;
            return true;
        }

        private static bool IsBetter(
            LambertParkingOrbitEjectionSolution candidateEjection,
            FiniteSoiDepartureAssessment candidateAssessment,
            LambertParkingOrbitEjectionSolution bestEjection,
            FiniteSoiDepartureAssessment bestAssessment)
        {
            if (candidateAssessment == null)
                return false;

            if (bestAssessment == null)
                return true;

            double scoreScale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidateAssessment.NormalizedStateError,
                        bestAssessment.NormalizedStateError));

            double tolerance =
                ScoreRelativeTolerance *
                scoreScale;

            double difference =
                candidateAssessment.NormalizedStateError -
                bestAssessment.NormalizedStateError;

            if (difference < -tolerance)
                return true;

            if (Math.Abs(difference) > tolerance)
                return false;

            return
                candidateEjection.TotalDeltaVMetersPerSecond <
                bestEjection.TotalDeltaVMetersPerSecond;
        }

        private static LambertParkingOrbitEjectionSolution CloneWith(
            LambertParkingOrbitEjectionSolution seed,
            double burnUt,
            double prograde,
            double normal,
            double radial)
        {
            double total =
                Math.Sqrt(
                    prograde * prograde +
                    normal * normal +
                    radial * radial);

            return
                new LambertParkingOrbitEjectionSolution
                {
                    BurnUniversalTimeSeconds = burnUt,
                    WindowOffsetSeconds =
                        seed.WindowOffsetSeconds +
                        (burnUt -
                         seed.BurnUniversalTimeSeconds),
                    ParkingRadiusMeters =
                        seed.ParkingRadiusMeters,
                    ParkingAltitudeMeters =
                        seed.ParkingAltitudeMeters,
                    ParkingSpeedMetersPerSecond =
                        seed.ParkingSpeedMetersPerSecond,
                    HyperbolicExcessSpeedMetersPerSecond =
                        seed.HyperbolicExcessSpeedMetersPerSecond,
                    HyperbolicPeriapsisSpeedMetersPerSecond =
                        seed.HyperbolicPeriapsisSpeedMetersPerSecond,
                    HyperbolicEccentricity =
                        seed.HyperbolicEccentricity,
                    AsymptoteAngleDegrees =
                        seed.AsymptoteAngleDegrees,
                    ProgradeDeltaVMetersPerSecond =
                        prograde,
                    NormalDeltaVMetersPerSecond =
                        normal,
                    RadialDeltaVMetersPerSecond =
                        radial,
                    TotalDeltaVMetersPerSecond =
                        total,
                    GeometryResidualDegrees =
                        seed.GeometryResidualDegrees
                };
        }

        private static double ResolveParkingPeriodSeconds(
            OrbitalElements parkingOrbit,
            double bodyMu)
        {
            if (parkingOrbit != null &&
                FinitePositive(parkingOrbit.PeriodSeconds))
                return parkingOrbit.PeriodSeconds;

            if (parkingOrbit != null &&
                FinitePositive(parkingOrbit.SemiMajorAxisMeters) &&
                FinitePositive(bodyMu))
            {
                double a =
                    parkingOrbit.SemiMajorAxisMeters;

                return
                    2.0 * Math.PI *
                    Math.Sqrt(
                        a * a * a /
                        bodyMu);
            }

            return 3600.0;
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

        private static bool FinitePositive(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
