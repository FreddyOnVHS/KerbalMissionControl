using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// MechJeb-style shooting layer for the KMC transfer pipeline.
    ///
    /// Lambert supplies the seed. This solver then evaluates the complete
    /// parking-orbit -> source-SOI -> parent-frame trajectory against the live
    /// target orbit and searches burn UT, P/N/R, and arrival epoch.
    ///
    /// The primary objective is actual target miss distance. A secondary
    /// source-SOI Lambert velocity mismatch is used only as a tie breaker.
    /// </summary>
    public static class TargetSoiShootingSolver
    {
        private const int ExitBracketExpansions = 80;
        private const int ExitBisectionIterations = 64;
        private const int MaximumIterations = 72;

        private const double MinimumBurnUtStepSeconds = 0.25;
        private const double MinimumDvStepMetersPerSecond = 0.01;
        private const double MinimumArrivalStepSeconds = 1.0;

        public static bool TrySolve(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            out TargetSoiShootingResult result)
        {
            result = null;

            if (transfer == null ||
                seedEjection == null ||
                parkingOrbit == null ||
                originBody == null ||
                destinationBody == null ||
                originBody.Orbit == null ||
                destinationBody.Orbit == null ||
                !FinitePositive(originBody.GravParameter) ||
                !FinitePositive(originBody.SoiRadiusMeters) ||
                !FinitePositive(destinationBody.SoiRadiusMeters) ||
                !FinitePositive(parentGravParameter))
                return false;

            double seedArrivalUt =
                transfer.ArrivalUniversalTimeSeconds;

            TargetSoiShootingAssessment seedAssessment;

            if (!TryEvaluate(
                    transfer,
                    seedEjection,
                    seedArrivalUt,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    out seedAssessment))
                return false;

            double parkingPeriod =
                ResolveParkingPeriodSeconds(
                    parkingOrbit,
                    originBody.GravParameter);

            double transferTime =
                Math.Max(
                    3600.0,
                    transfer.TimeOfFlightSeconds);

            double burnUtStep =
                Clamp(
                    parkingPeriod / 24.0,
                    10.0,
                    240.0);

            double dvScale =
                Math.Max(
                    1.0,
                    seedEjection.TotalDeltaVMetersPerSecond);

            double dvStep =
                Clamp(
                    dvScale * 0.02,
                    2.0,
                    40.0);

            double arrivalStep =
                Clamp(
                    transferTime / 48.0,
                    1800.0,
                    172800.0);

            double burnHalfRange =
                Math.Max(
                    120.0,
                    parkingPeriod * 0.50);

            double componentHalfRange =
                Math.Max(
                    100.0,
                    dvScale * 0.40);

            double arrivalHalfRange =
                Math.Max(
                    21600.0,
                    transferTime * 0.18);

            double minBurnUt =
                seedEjection.BurnUniversalTimeSeconds -
                burnHalfRange;

            double maxBurnUt =
                seedEjection.BurnUniversalTimeSeconds +
                burnHalfRange;

            double minArrivalUt =
                seedArrivalUt -
                arrivalHalfRange;

            double maxArrivalUt =
                seedArrivalUt +
                arrivalHalfRange;

            double minP =
                seedEjection.ProgradeDeltaVMetersPerSecond -
                componentHalfRange;

            double maxP =
                seedEjection.ProgradeDeltaVMetersPerSecond +
                componentHalfRange;

            double minN =
                seedEjection.NormalDeltaVMetersPerSecond -
                componentHalfRange;

            double maxN =
                seedEjection.NormalDeltaVMetersPerSecond +
                componentHalfRange;

            double minR =
                seedEjection.RadialDeltaVMetersPerSecond -
                componentHalfRange;

            double maxR =
                seedEjection.RadialDeltaVMetersPerSecond +
                componentHalfRange;

            LambertParkingOrbitEjectionSolution bestEjection =
                CloneWith(
                    seedEjection,
                    seedEjection.BurnUniversalTimeSeconds,
                    seedEjection.ProgradeDeltaVMetersPerSecond,
                    seedEjection.NormalDeltaVMetersPerSecond,
                    seedEjection.RadialDeltaVMetersPerSecond);

            TargetSoiShootingAssessment bestAssessment =
                seedAssessment;

            int evaluations = 1;
            int iterations = 0;

            for (int iteration = 0;
                iteration < MaximumIterations;
                iteration++)
            {
                iterations = iteration + 1;
                bool improved = false;

                improved |= TryAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    0,
                    burnUtStep,
                    minBurnUt,
                    maxBurnUt,
                    minP,
                    maxP,
                    minN,
                    maxN,
                    minR,
                    maxR,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    1,
                    dvStep,
                    minBurnUt,
                    maxBurnUt,
                    minP,
                    maxP,
                    minN,
                    maxN,
                    minR,
                    maxR,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    2,
                    dvStep,
                    minBurnUt,
                    maxBurnUt,
                    minP,
                    maxP,
                    minN,
                    maxN,
                    minR,
                    maxR,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    3,
                    dvStep,
                    minBurnUt,
                    maxBurnUt,
                    minP,
                    maxP,
                    minN,
                    maxN,
                    minR,
                    maxR,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                improved |= TryAxis(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    4,
                    arrivalStep,
                    minBurnUt,
                    maxBurnUt,
                    minP,
                    maxP,
                    minN,
                    maxN,
                    minR,
                    maxR,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations);

                if (bestAssessment.PredictedEncounter &&
                    burnUtStep <= 2.0 &&
                    dvStep <= 0.10 &&
                    arrivalStep <= 30.0)
                    break;

                if (!improved)
                {
                    burnUtStep *= 0.5;
                    dvStep *= 0.5;
                    arrivalStep *= 0.5;
                }

                if (burnUtStep < MinimumBurnUtStepSeconds &&
                    dvStep < MinimumDvStepMetersPerSecond &&
                    arrivalStep < MinimumArrivalStepSeconds)
                    break;
            }

            result =
                new TargetSoiShootingResult(
                    seedEjection,
                    seedAssessment,
                    bestEjection,
                    bestAssessment,
                    iterations,
                    evaluations);

            return true;
        }

        private static bool TryAxis(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seed,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            int axis,
            double step,
            double minBurnUt,
            double maxBurnUt,
            double minP,
            double maxP,
            double minN,
            double maxN,
            double minR,
            double maxR,
            double minArrivalUt,
            double maxArrivalUt,
            ref LambertParkingOrbitEjectionSolution bestEjection,
            ref TargetSoiShootingAssessment bestAssessment,
            ref int evaluations)
        {
            bool improved = false;

            for (int sign = 1; sign >= -1; sign -= 2)
            {
                double burnUt =
                    bestEjection.BurnUniversalTimeSeconds;

                double p =
                    bestEjection.ProgradeDeltaVMetersPerSecond;

                double n =
                    bestEjection.NormalDeltaVMetersPerSecond;

                double r =
                    bestEjection.RadialDeltaVMetersPerSecond;

                double arrivalUt =
                    bestAssessment.ArrivalUniversalTimeSeconds;

                double delta =
                    sign * step;

                if (axis == 0)
                    burnUt += delta;
                else if (axis == 1)
                    p += delta;
                else if (axis == 2)
                    n += delta;
                else if (axis == 3)
                    r += delta;
                else
                    arrivalUt += delta;

                if (burnUt < minBurnUt ||
                    burnUt > maxBurnUt ||
                    p < minP ||
                    p > maxP ||
                    n < minN ||
                    n > maxN ||
                    r < minR ||
                    r > maxR ||
                    arrivalUt < minArrivalUt ||
                    arrivalUt > maxArrivalUt)
                    continue;

                LambertParkingOrbitEjectionSolution trial =
                    CloneWith(
                        seed,
                        burnUt,
                        p,
                        n,
                        r);

                TargetSoiShootingAssessment assessment;

                if (!TryEvaluate(
                        transfer,
                        trial,
                        arrivalUt,
                        parkingOrbit,
                        originBody,
                        destinationBody,
                        parentMu,
                        out assessment))
                    continue;

                evaluations++;

                if (!IsBetter(
                        trial,
                        assessment,
                        bestEjection,
                        bestAssessment))
                    continue;

                bestEjection = trial;
                bestAssessment = assessment;
                improved = true;
            }

            return improved;
        }

        private static bool TryEvaluate(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution ejection,
            double arrivalUt,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            out TargetSoiShootingAssessment assessment)
        {
            assessment = null;

            if (!FinitePositive(arrivalUt) ||
                arrivalUt <= ejection.BurnUniversalTimeSeconds)
                return false;

            StateVector parkingState;

            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    originBody.GravParameter,
                    ejection.BurnUniversalTimeSeconds,
                    out parkingState))
                return false;

            Vector3d prograde;
            Vector3d normal;
            Vector3d radial;

            if (!TryNormalize(
                    parkingState.Velocity,
                    out prograde) ||
                !TryNormalize(
                    Vector3d.Cross(
                        parkingState.Position,
                        parkingState.Velocity),
                    out normal) ||
                !TryNormalize(
                    Vector3d.Cross(
                        prograde,
                        normal),
                    out radial))
                return false;

            Vector3d postBurnVelocity =
                parkingState.Velocity +
                prograde *
                    ejection.ProgradeDeltaVMetersPerSecond +
                normal *
                    ejection.NormalDeltaVMetersPerSecond +
                radial *
                    ejection.RadialDeltaVMetersPerSecond;

            StateVector relativeBurnState =
                new StateVector(
                    parkingState.Position,
                    postBurnVelocity,
                    ejection.BurnUniversalTimeSeconds,
                    originBody.GravParameter,
                    originBody.Name);

            double exitUt;
            StateVector relativeExitState;

            if (!TryFindSoiExit(
                    relativeBurnState,
                    originBody.SoiRadiusMeters,
                    Math.Max(
                        1.0,
                        ejection.HyperbolicExcessSpeedMetersPerSecond),
                    out exitUt,
                    out relativeExitState))
                return false;

            if (arrivalUt <= exitUt + 1.0)
                return false;

            StateVector originExitState;

            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentMu,
                    exitUt,
                    out originExitState))
                return false;

            Vector3d actualExitPosition =
                originExitState.Position +
                relativeExitState.Position;

            Vector3d actualExitVelocity =
                originExitState.Velocity +
                relativeExitState.Velocity;

            StateVector actualExit =
                new StateVector(
                    actualExitPosition,
                    actualExitVelocity,
                    exitUt,
                    parentMu,
                    originBody.ParentName);

            StateVector actualArrival;

            if (!StateVectorPropagator.TryPropagate(
                    actualExit,
                    arrivalUt,
                    out actualArrival))
                return false;

            StateVector destinationArrival;

            if (!KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentMu,
                    arrivalUt,
                    out destinationArrival))
                return false;

            double missDistance =
                (actualArrival.Position -
                 destinationArrival.Position).Magnitude;

            double relativeSpeed =
                (actualArrival.Velocity -
                 destinationArrival.Velocity).Magnitude;

            if (!Vector3d.Finite(missDistance) ||
                !Vector3d.Finite(relativeSpeed))
                return false;

            double velocityMismatch =
                double.PositiveInfinity;

            LambertSolution boundaryLambert;

            if (LambertSolver.TrySolve(
                    actualExitPosition,
                    destinationArrival.Position,
                    arrivalUt - exitUt,
                    parentMu,
                    transfer.Path,
                    out boundaryLambert))
            {
                velocityMismatch =
                    (actualExitVelocity -
                     boundaryLambert.DepartureVelocity).Magnitude;
            }

            if (!Vector3d.Finite(velocityMismatch))
                velocityMismatch =
                    1e300;

            assessment =
                new TargetSoiShootingAssessment
                {
                    ArrivalUniversalTimeSeconds =
                        arrivalUt,
                    SourceSoiExitUniversalTimeSeconds =
                        exitUt,
                    MissDistanceMeters =
                        missDistance,
                    MissFractionOfTargetSoi =
                        missDistance /
                        destinationBody.SoiRadiusMeters,
                    RelativeSpeedAtArrivalMetersPerSecond =
                        relativeSpeed,
                    SourceLambertVelocityMismatchMetersPerSecond =
                        velocityMismatch,
                    PredictedEncounter =
                        missDistance <=
                        destinationBody.SoiRadiusMeters
                };

            return true;
        }

        private static bool IsBetter(
            LambertParkingOrbitEjectionSolution candidateEjection,
            TargetSoiShootingAssessment candidate,
            LambertParkingOrbitEjectionSolution bestEjection,
            TargetSoiShootingAssessment best)
        {
            if (candidate == null)
                return false;

            if (best == null)
                return true;

            double missTolerance =
                Math.Max(
                    1e-3,
                    Math.Max(
                        candidate.MissDistanceMeters,
                        best.MissDistanceMeters) *
                    1e-12);

            double missDifference =
                candidate.MissDistanceMeters -
                best.MissDistanceMeters;

            if (missDifference < -missTolerance)
                return true;

            if (Math.Abs(missDifference) >
                missTolerance)
                return false;

            if (candidate.SourceLambertVelocityMismatchMetersPerSecond <
                best.SourceLambertVelocityMismatchMetersPerSecond - 1e-9)
                return true;

            if (candidate.SourceLambertVelocityMismatchMetersPerSecond >
                best.SourceLambertVelocityMismatchMetersPerSecond + 1e-9)
                return false;

            return
                candidateEjection.TotalDeltaVMetersPerSecond <
                bestEjection.TotalDeltaVMetersPerSecond;
        }

        private static bool TryFindSoiExit(
            StateVector initial,
            double soiRadius,
            double excessSpeed,
            out double exitUt,
            out StateVector exitState)
        {
            exitUt = double.NaN;
            exitState = null;

            double initialRadius =
                initial.Position.Magnitude;

            if (!FinitePositive(initialRadius) ||
                initialRadius >= soiRadius)
                return false;

            double estimate =
                (soiRadius - initialRadius) /
                Math.Max(
                    1.0,
                    excessSpeed);

            double lowDt = 0.0;

            double highDt =
                Math.Max(
                    30.0,
                    estimate * 1.5);

            bool bracketed = false;

            for (int i = 0;
                i < ExitBracketExpansions;
                i++)
            {
                StateVector highState;

                if (!StateVectorPropagator.TryPropagate(
                        initial,
                        initial.UniversalTimeSeconds +
                            highDt,
                        out highState))
                    return false;

                if (highState.Position.Magnitude >=
                    soiRadius)
                {
                    bracketed = true;
                    break;
                }

                lowDt = highDt;
                highDt *= 2.0;

                if (!Vector3d.Finite(highDt) ||
                    highDt > 1e9)
                    return false;
            }

            if (!bracketed)
                return false;

            for (int i = 0;
                i < ExitBisectionIterations;
                i++)
            {
                double midDt =
                    0.5 *
                    (lowDt + highDt);

                StateVector midState;

                if (!StateVectorPropagator.TryPropagate(
                        initial,
                        initial.UniversalTimeSeconds +
                            midDt,
                        out midState))
                    return false;

                if (midState.Position.Magnitude <
                    soiRadius)
                    lowDt = midDt;
                else
                    highDt = midDt;
            }

            exitUt =
                initial.UniversalTimeSeconds +
                highDt;

            return
                StateVectorPropagator.TryPropagate(
                    initial,
                    exitUt,
                    out exitState);
        }

        private static LambertParkingOrbitEjectionSolution CloneWith(
            LambertParkingOrbitEjectionSolution seed,
            double burnUt,
            double p,
            double n,
            double r)
        {
            double total =
                Math.Sqrt(
                    p * p +
                    n * n +
                    r * r);

            return
                new LambertParkingOrbitEjectionSolution
                {
                    BurnUniversalTimeSeconds =
                        burnUt,
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
                        p,
                    NormalDeltaVMetersPerSecond =
                        n,
                    RadialDeltaVMetersPerSecond =
                        r,
                    TotalDeltaVMetersPerSecond =
                        total,
                    GeometryResidualDegrees =
                        seed.GeometryResidualDegrees
                };
        }

        private static double ResolveParkingPeriodSeconds(
            OrbitalElements parkingOrbit,
            double mu)
        {
            if (parkingOrbit != null &&
                FinitePositive(
                    parkingOrbit.PeriodSeconds))
                return
                    parkingOrbit.PeriodSeconds;

            if (parkingOrbit != null &&
                FinitePositive(
                    parkingOrbit.SemiMajorAxisMeters))
            {
                double a =
                    parkingOrbit.SemiMajorAxisMeters;

                return
                    2.0 *
                    Math.PI *
                    Math.Sqrt(
                        a * a * a /
                        mu);
            }

            return 3600.0;
        }

        private static bool TryNormalize(
            Vector3d value,
            out Vector3d normalized)
        {
            normalized =
                new Vector3d();

            double magnitude =
                value.Magnitude;

            if (!FinitePositive(magnitude))
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
            return
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value));
        }

        private static bool FinitePositive(
            double value)
        {
            return
                Vector3d.Finite(value) &&
                value > 0.0;
        }
    }
}
