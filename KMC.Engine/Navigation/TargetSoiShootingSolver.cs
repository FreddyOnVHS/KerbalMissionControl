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
    /// The solver first establishes an actual target-SOI encounter. Once an
    /// encounter exists, it shapes the target-relative hyperbola toward a safe
    /// generic periapsis radius instead of driving the trajectory through the
    /// target center. Source-SOI Lambert mismatch and DV are tie breakers.
    /// </summary>
    public static class TargetSoiShootingSolver
    {
        private const int ExitBracketExpansions = 80;
        private const int ExitBisectionIterations = 64;
        private const int TargetEntryBisectionIterations = 56;
        private const int MaximumIterations = 96;

        private const double MinimumBurnUtStepSeconds = 0.25;
        private const double MinimumDvStepMetersPerSecond = 0.01;
        private const double MinimumArrivalStepSeconds = 1.0;

        private enum OptimizationStage
        {
            Encounter,
            Periapsis
        }

        public static bool TrySolve(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            out TargetSoiShootingResult result)
        {
            string ignoredFailureReason;

            return
                TrySolve(
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    out result,
                    out ignoredFailureReason);
        }

        public static bool TrySolve(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seedEjection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            out TargetSoiShootingResult result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;

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
            {
                failureReason = "INVALID INPUT / MISSING BODY DATA";
                return false;
            }

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
            {
                failureReason =
                    DiagnoseSeedFailure(
                        transfer,
                        seedEjection,
                        seedArrivalUt,
                        parkingOrbit,
                        originBody,
                        destinationBody,
                        parentGravParameter);
                return false;
            }

            double seedDv =
                seedEjection.TotalDeltaVMetersPerSecond;

            double maximumDv =
                TargetSoiOptimizationPolicy.
                    ComputeMaximumDepartureDeltaV(
                        seedDv);

            if (!FinitePositive(seedDv) ||
                !FinitePositive(maximumDv))
            {
                failureReason = "INVALID DEPARTURE DV SEED";
                return false;
            }

            double parkingPeriod =
                ResolveParkingPeriodSeconds(
                    parkingOrbit,
                    originBody.GravParameter);

            double transferTime =
                Math.Max(
                    3600.0,
                    transfer.TimeOfFlightSeconds);

            double initialBurnUtStep =
                Clamp(
                    parkingPeriod / 24.0,
                    10.0,
                    240.0);

            double initialDvStep =
                Clamp(
                    seedDv * 0.025,
                    2.0,
                    80.0);

            double initialArrivalStep =
                Clamp(
                    transferTime / 48.0,
                    1800.0,
                    172800.0);

            double burnHalfRange =
                Math.Max(
                    120.0,
                    parkingPeriod * 0.50);

            /*
             * Keep KMC's coordinate box generous, but the actual vector
             * magnitude is separately constrained by the seed-derived DV
             * trust region. This is analogous to MechJeb's bounded variables:
             * feasibility may move the burn, but cannot purchase an encounter
             * with an arbitrarily expensive impulse.
             */
            double componentHalfRange =
                Math.Max(
                    300.0,
                    seedDv * 1.50);

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

            /*
             * PASS 1 -- FEASIBILITY
             *
             * MechJeb first establishes a dynamically consistent trajectory
             * before applying terminal conditions. KMC mirrors that behavior:
             * establish target-SOI encounter while respecting the departure-DV
             * trust region.
             */
            RunStage(
                OptimizationStage.Encounter,
                48,
                transfer,
                seedEjection,
                parkingOrbit,
                originBody,
                destinationBody,
                parentGravParameter,
                seedDv,
                maximumDv,
                initialBurnUtStep,
                initialDvStep,
                initialArrivalStep,
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
                ref evaluations,
                ref iterations);

            bool bPlaneBootstrapApplied =
                false;

            LambertParkingOrbitEjectionSolution encounterIncumbentEjection =
                bestEjection;

            TargetSoiShootingAssessment encounterIncumbentAssessment =
                bestAssessment;

            /*
             * B-PLANE TERMINAL CONSTRAINT SEARCH
             *
             * MechJeb includes target-SOI position/velocity in the nonlinear
             * transfer constraints. KMC's bounded optimizer cannot express
             * that full SQP problem directly, so search the equivalent
             * terminal geometry explicitly:
             *
             * - keep the established encounter as incumbent;
             * - use its live target-relative SOI-entry velocity as the
             *   incoming B-plane direction;
             * - vary target B-plane azimuth and arrival epoch;
             * - solve a new parent-frame Lambert leg to that finite SOI point;
             * - convert the departure vector through the general 3D parking
             *   ejection solver;
             * - accept only candidates inside the departure-DV trust region;
             * - evaluate the full finite-SOI trajectory before ranking.
             */
            if (bestAssessment.PredictedEncounter &&
                FinitePositive(
                    bestAssessment.DesiredPeriapsisRadiusMeters) &&
                bestAssessment.TargetSoiEntryRelativeVelocity.IsFinite &&
                FinitePositive(
                    bestAssessment.TargetSoiEntryRelativeVelocity.Magnitude))
            {
                RunBPlaneTerminalSearch(
                    transfer,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    seedDv,
                    bestAssessment.TargetSoiEntryRelativeVelocity,
                    bestAssessment.DesiredPeriapsisRadiusMeters,
                    minArrivalUt,
                    maxArrivalUt,
                    ref bestEjection,
                    ref bestAssessment,
                    ref evaluations,
                    ref bPlaneBootstrapApplied);
            }

            /*
             * PASS 2 -- TERMINAL GEOMETRY
             *
             * Only after an encounter exists do we turn on periapsis/B-plane
             * shaping. Once the periapsis constraint is feasible, departure DV
             * becomes the primary objective again.
             */
            if (bestAssessment.PredictedEncounter)
            {
                RunStage(
                    OptimizationStage.Periapsis,
                    48,
                    transfer,
                    seedEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    seedDv,
                    maximumDv,
                    initialBurnUtStep * 0.5,
                    initialDvStep * 0.5,
                    initialArrivalStep * 0.5,
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
                    ref evaluations,
                    ref iterations);
            }

            /*
             * The B-plane state is an optimizer initialization, not authority
             * to discard a known-good encounter. If terminal refinement does
             * not beat the encounter incumbent under the terminal objective,
             * preserve the incumbent.
             */
            if (encounterIncumbentAssessment != null &&
                encounterIncumbentAssessment.PredictedEncounter &&
                !IsBetter(
                    OptimizationStage.Periapsis,
                    bestEjection,
                    bestAssessment,
                    encounterIncumbentEjection,
                    encounterIncumbentAssessment))
            {
                bestEjection =
                    encounterIncumbentEjection;

                bestAssessment =
                    encounterIncumbentAssessment;

                bPlaneBootstrapApplied =
                    false;
            }

            result =
                new TargetSoiShootingResult(
                    seedEjection,
                    seedAssessment,
                    bestEjection,
                    bestAssessment,
                    iterations,
                    evaluations,
                    bPlaneBootstrapApplied);

            return true;
        }

        private static void RunBPlaneTerminalSearch(
            TransferSearchSolution transfer,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double seedDv,
            Vector3d incomingTargetRelativeVelocity,
            double desiredPeriapsisRadius,
            double minArrivalUt,
            double maxArrivalUt,
            ref LambertParkingOrbitEjectionSolution bestEjection,
            ref TargetSoiShootingAssessment bestAssessment,
            ref int evaluations,
            ref bool bPlaneApplied)
        {
            const int azimuthSamples = 16;
            const int arrivalSamples = 7;
            const int refinementPasses = 3;

            double centerArrivalUt =
                bestAssessment.ArrivalUniversalTimeSeconds;

            double arrivalHalfSpan =
                Math.Min(
                    Math.Max(
                        1800.0,
                        (maxArrivalUt - minArrivalUt) * 0.25),
                    172800.0);

            double centerAzimuth =
                0.0;

            LambertParkingOrbitEjectionSolution searchBestEjection =
                bestEjection;

            TargetSoiShootingAssessment searchBestAssessment =
                bestAssessment;

            bool improved =
                false;

            for (int pass = 0;
                pass < refinementPasses;
                pass++)
            {
                LambertParkingOrbitEjectionSolution passBestEjection =
                    searchBestEjection;

                TargetSoiShootingAssessment passBestAssessment =
                    searchBestAssessment;

                double passBestAzimuth =
                    centerAzimuth;

                double passBestArrival =
                    centerArrivalUt;

                for (int ai = 0;
                    ai < arrivalSamples;
                    ai++)
                {
                    double arrivalFraction =
                        arrivalSamples == 1
                            ? 0.0
                            : ai /
                                (double)(arrivalSamples - 1);

                    double arrivalUt =
                        centerArrivalUt +
                        (arrivalFraction * 2.0 - 1.0) *
                        arrivalHalfSpan;

                    arrivalUt =
                        Clamp(
                            arrivalUt,
                            minArrivalUt,
                            maxArrivalUt);

                    for (int bi = 0;
                        bi < azimuthSamples;
                        bi++)
                    {
                        double azimuth =
                            centerAzimuth +
                            2.0 * Math.PI *
                            bi /
                            azimuthSamples;

                        LambertParkingOrbitEjectionSolution trialEjection;
                        double desiredB;

                        if (!TargetBPlaneBootstrapPlanner.
                                TryCreateEjectionAtEntry(
                                    transfer,
                                    parkingOrbit,
                                    originBody,
                                    destinationBody,
                                    parentMu,
                                    incomingTargetRelativeVelocity,
                                    desiredPeriapsisRadius,
                                    arrivalUt,
                                    azimuth,
                                    out trialEjection,
                                    out desiredB))
                            continue;

                        if (!TargetSoiOptimizationPolicy.
                                IsWithinDepartureTrustRegion(
                                    seedDv,
                                    trialEjection.TotalDeltaVMetersPerSecond))
                            continue;

                        TargetSoiShootingAssessment trialAssessment;

                        if (!TryEvaluate(
                                transfer,
                                trialEjection,
                                arrivalUt,
                                parkingOrbit,
                                originBody,
                                destinationBody,
                                parentMu,
                                out trialAssessment))
                            continue;

                        evaluations++;

                        if (!trialAssessment.PredictedEncounter)
                            continue;

                        if (!IsBetter(
                                OptimizationStage.Periapsis,
                                trialEjection,
                                trialAssessment,
                                passBestEjection,
                                passBestAssessment))
                            continue;

                        passBestEjection =
                            trialEjection;

                        passBestAssessment =
                            trialAssessment;

                        passBestAzimuth =
                            azimuth;

                        passBestArrival =
                            arrivalUt;
                    }
                }

                if (passBestEjection != searchBestEjection ||
                    passBestAssessment != searchBestAssessment)
                {
                    searchBestEjection =
                        passBestEjection;

                    searchBestAssessment =
                        passBestAssessment;

                    centerAzimuth =
                        passBestAzimuth;

                    centerArrivalUt =
                        passBestArrival;

                    improved =
                        true;
                }

                arrivalHalfSpan *=
                    0.35;
            }

            if (improved &&
                IsBetter(
                    OptimizationStage.Periapsis,
                    searchBestEjection,
                    searchBestAssessment,
                    bestEjection,
                    bestAssessment))
            {
                bestEjection =
                    searchBestEjection;

                bestAssessment =
                    searchBestAssessment;

                bPlaneApplied =
                    true;
            }
        }

        private static void RunStage(
            OptimizationStage stage,
            int maximumIterations,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seed,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double seedDv,
            double maximumDv,
            double initialBurnUtStep,
            double initialDvStep,
            double initialArrivalStep,
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
            ref int evaluations,
            ref int iterations)
        {
            double burnUtStep =
                Math.Max(
                    MinimumBurnUtStepSeconds,
                    initialBurnUtStep);

            double dvStep =
                Math.Max(
                    MinimumDvStepMetersPerSecond,
                    initialDvStep);

            double arrivalStep =
                Math.Max(
                    MinimumArrivalStepSeconds,
                    initialArrivalStep);

            for (int iteration = 0;
                iteration < maximumIterations;
                iteration++)
            {
                iterations++;
                bool improved = false;

                for (int axis = 0;
                    axis < 5;
                    axis++)
                {
                    double step =
                        axis == 0
                            ? burnUtStep
                            : axis == 4
                                ? arrivalStep
                                : dvStep;

                    improved |= TryAxis(
                        stage,
                        transfer,
                        seed,
                        parkingOrbit,
                        originBody,
                        destinationBody,
                        parentMu,
                        seedDv,
                        maximumDv,
                        axis,
                        step,
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
                }

                if (stage == OptimizationStage.Periapsis &&
                    TargetSoiOptimizationPolicy.
                        IsPeriapsisFeasible(
                            bestAssessment) &&
                    burnUtStep <= 2.0 &&
                    dvStep <= 0.10 &&
                    arrivalStep <= 30.0)
                    break;

                /*
                 * Once feasibility is established in pass 1, stop there.
                 * Periapsis shaping belongs to pass 2, not the encounter pass.
                 */
                if (stage == OptimizationStage.Encounter &&
                    bestAssessment.PredictedEncounter)
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
        }

        private static bool TryAxis(
            OptimizationStage stage,
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution seed,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double seedDv,
            double maximumDv,
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

                if (!TargetSoiOptimizationPolicy.
                        IsWithinDepartureTrustRegion(
                            seedDv,
                            trial.TotalDeltaVMetersPerSecond) ||
                    trial.TotalDeltaVMetersPerSecond >
                        maximumDv + 1e-9)
                    continue;

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
                        stage,
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

            bool predictedEncounter =
                missDistance <=
                destinationBody.SoiRadiusMeters;

            double desiredPeriapsis =
                ComputeDesiredPeriapsisRadius(
                    destinationBody);

            double entryUt =
                double.NaN;

            double periapsisRadius =
                double.NaN;

            double periapsisAltitude =
                double.NaN;

            double periapsisError =
                double.PositiveInfinity;

            double bPlaneRadius =
                double.NaN;

            double desiredBPlaneRadius =
                double.NaN;

            double bPlaneError =
                double.PositiveInfinity;

            double bPlaneErrorFraction =
                double.PositiveInfinity;

            bool collision =
                false;

            Vector3d targetEntryRelativePosition =
                new Vector3d();

            Vector3d targetEntryRelativeVelocity =
                new Vector3d();

            if (predictedEncounter &&
                FinitePositive(destinationBody.GravParameter) &&
                FinitePositive(destinationBody.RadiusMeters) &&
                FinitePositive(desiredPeriapsis))
            {
                StateVector spacecraftEntry;
                StateVector destinationEntry;

                if (TryFindTargetSoiEntry(
                        actualExit,
                        destinationBody,
                        parentMu,
                        exitUt,
                        arrivalUt,
                        out entryUt,
                        out spacecraftEntry,
                        out destinationEntry))
                {
                    Vector3d relativeEntryPosition =
                        spacecraftEntry.Position -
                        destinationEntry.Position;

                    Vector3d relativeEntryVelocity =
                        spacecraftEntry.Velocity -
                        destinationEntry.Velocity;

                    targetEntryRelativePosition =
                        relativeEntryPosition;

                    targetEntryRelativeVelocity =
                        relativeEntryVelocity;

                    if (TryCalculatePeriapsisRadius(
                            relativeEntryPosition,
                            relativeEntryVelocity,
                            destinationBody.GravParameter,
                            out periapsisRadius))
                    {
                        periapsisAltitude =
                            periapsisRadius -
                            destinationBody.RadiusMeters;

                        periapsisError =
                            Math.Abs(
                                periapsisRadius -
                                desiredPeriapsis);

                        collision =
                            periapsisRadius <=
                            destinationBody.RadiusMeters;

                        if (TargetBPlanePlanner.TryCalculate(
                                relativeEntryPosition,
                                relativeEntryVelocity,
                                destinationBody.GravParameter,
                                destinationBody.SoiRadiusMeters,
                                desiredPeriapsis,
                                out bPlaneRadius,
                                out desiredBPlaneRadius,
                                out bPlaneError))
                        {
                            bPlaneErrorFraction =
                                bPlaneError /
                                destinationBody.SoiRadiusMeters;
                        }
                    }
                }
            }

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
                    TargetSoiEntryUniversalTimeSeconds =
                        entryUt,
                    TargetSoiEntryRelativePosition =
                        targetEntryRelativePosition,
                    TargetSoiEntryRelativeVelocity =
                        targetEntryRelativeVelocity,
                    DesiredPeriapsisRadiusMeters =
                        desiredPeriapsis,
                    TargetPeriapsisRadiusMeters =
                        periapsisRadius,
                    TargetPeriapsisAltitudeMeters =
                        periapsisAltitude,
                    TargetPeriapsisErrorMeters =
                        periapsisError,
                    TargetBPlaneRadiusMeters =
                        bPlaneRadius,
                    DesiredBPlaneRadiusMeters =
                        desiredBPlaneRadius,
                    TargetBPlaneErrorMeters =
                        bPlaneError,
                    TargetBPlaneErrorFractionOfSoi =
                        bPlaneErrorFraction,
                    PredictedCollision =
                        collision,
                    SourceLambertVelocityMismatchMetersPerSecond =
                        velocityMismatch,
                    PredictedEncounter =
                        predictedEncounter
                };

            return true;
        }

        private static string DiagnoseSeedFailure(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution ejection,
            double arrivalUt,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu)
        {
            if (!FinitePositive(arrivalUt) ||
                arrivalUt <= ejection.BurnUniversalTimeSeconds)
                return "ARRIVAL UT <= BURN UT";

            StateVector parkingState;

            if (!KeplerPropagator.TryPropagate(
                    parkingOrbit,
                    originBody.GravParameter,
                    ejection.BurnUniversalTimeSeconds,
                    out parkingState))
                return "PARKING STATE PROPAGATION";

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
                return "PARKING P/N/R FRAME";

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
                return "SOURCE SOI EXIT";

            if (arrivalUt <= exitUt + 1.0)
                return "ARRIVAL BEFORE SOURCE SOI EXIT";

            StateVector originExitState;

            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentMu,
                    exitUt,
                    out originExitState))
                return "ORIGIN PARENT STATE AT EXIT";

            StateVector actualExit =
                new StateVector(
                    originExitState.Position +
                        relativeExitState.Position,
                    originExitState.Velocity +
                        relativeExitState.Velocity,
                    exitUt,
                    parentMu,
                    originBody.ParentName);

            StateVector actualArrival;

            if (!StateVectorPropagator.TryPropagate(
                    actualExit,
                    arrivalUt,
                    out actualArrival))
                return "PARENT-FRAME COAST TO ARRIVAL";

            StateVector destinationArrival;

            if (!KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentMu,
                    arrivalUt,
                    out destinationArrival))
                return "DESTINATION STATE AT ARRIVAL";

            double missDistance =
                (actualArrival.Position -
                 destinationArrival.Position).Magnitude;

            double relativeSpeed =
                (actualArrival.Velocity -
                 destinationArrival.Velocity).Magnitude;

            if (!Vector3d.Finite(missDistance) ||
                !Vector3d.Finite(relativeSpeed))
                return "NONFINITE ARRIVAL ASSESSMENT";

            return "ASSESSMENT FINALIZATION";
        }

        private static bool IsBetter(
            OptimizationStage stage,
            LambertParkingOrbitEjectionSolution candidateEjection,
            TargetSoiShootingAssessment candidate,
            LambertParkingOrbitEjectionSolution bestEjection,
            TargetSoiShootingAssessment best)
        {
            if (candidate == null)
                return false;

            if (best == null)
                return true;

            if (stage == OptimizationStage.Encounter)
            {
                /*
                 * Feasibility pass:
                 * - an encounter is a constraint transition;
                 * - before feasibility, reduce miss distance;
                 * - once both are encounters, minimize departure DV.
                 */
                if (candidate.PredictedEncounter !=
                    best.PredictedEncounter)
                    return candidate.PredictedEncounter;

                if (!candidate.PredictedEncounter)
                {
                    double missTolerance =
                        Math.Max(
                            1e-3,
                            Math.Max(
                                candidate.MissDistanceMeters,
                                best.MissDistanceMeters) *
                            1e-12);

                    if (candidate.MissDistanceMeters <
                        best.MissDistanceMeters -
                        missTolerance)
                        return true;

                    if (candidate.MissDistanceMeters >
                        best.MissDistanceMeters +
                        missTolerance)
                        return false;
                }

                return
                    candidateEjection.TotalDeltaVMetersPerSecond <
                    bestEjection.TotalDeltaVMetersPerSecond -
                    1e-9;
            }

            /*
             * Terminal-geometry pass:
             * never sacrifice the target encounter.
             */
            if (!candidate.PredictedEncounter)
                return false;

            if (!best.PredictedEncounter)
                return true;

            bool candidateFeasible =
                TargetSoiOptimizationPolicy.
                    IsPeriapsisFeasible(
                        candidate);

            bool bestFeasible =
                TargetSoiOptimizationPolicy.
                    IsPeriapsisFeasible(
                        best);

            if (candidateFeasible !=
                bestFeasible)
                return candidateFeasible;

            if (candidateFeasible)
            {
                /*
                 * Once encounter + safe periapsis constraints are satisfied,
                 * departure DV is the actual objective, as in MechJeb's
                 * constrained optimization.
                 */
                double dvDifference =
                    candidateEjection.TotalDeltaVMetersPerSecond -
                    bestEjection.TotalDeltaVMetersPerSecond;

                if (dvDifference < -1e-9)
                    return true;

                if (dvDifference > 1e-9)
                    return false;

                return
                    candidate.TargetPeriapsisErrorMeters <
                    best.TargetPeriapsisErrorMeters;
            }

            double candidateConstraintScore =
                TargetSoiOptimizationPolicy.
                    ComputePeriapsisConstraintScore(
                        candidate);

            double bestConstraintScore =
                TargetSoiOptimizationPolicy.
                    ComputePeriapsisConstraintScore(
                        best);

            if (candidateConstraintScore <
                bestConstraintScore - 1e-12)
                return true;

            if (candidateConstraintScore >
                bestConstraintScore + 1e-12)
                return false;

            return
                candidateEjection.TotalDeltaVMetersPerSecond <
                bestEjection.TotalDeltaVMetersPerSecond -
                1e-9;
        }

        private static double ComputeDesiredPeriapsisRadius(
            CelestialBodyState destinationBody)
        {
            double soi =
                destinationBody.SoiRadiusMeters;

            double bodyRadius =
                destinationBody.RadiusMeters;

            if (!FinitePositive(soi))
                return double.NaN;

            double desired =
                soi * 0.01;

            if (FinitePositive(bodyRadius))
            {
                desired =
                    Math.Max(
                        desired,
                        bodyRadius * 2.0);
            }

            /*
             * Keep the default aim point comfortably inside the SOI. If a
             * modded body has unusually little room between surface and SOI,
             * prefer the deepest still-useful generic encounter we can express
             * without stock atmosphere assumptions.
             */
            double maximum =
                soi * 0.25;

            desired =
                Math.Min(
                    desired,
                    maximum);

            if (FinitePositive(bodyRadius) &&
                desired <= bodyRadius)
            {
                double fallback =
                    bodyRadius * 1.10;

                if (fallback >= soi)
                    return double.NaN;

                desired =
                    Math.Min(
                        fallback,
                        maximum);
            }

            return
                FinitePositive(desired)
                    ? desired
                    : double.NaN;
        }

        private static bool TryFindTargetSoiEntry(
            StateVector spacecraftExit,
            CelestialBodyState destinationBody,
            double parentMu,
            double lowUt,
            double highUt,
            out double entryUt,
            out StateVector spacecraftEntry,
            out StateVector destinationEntry)
        {
            entryUt = double.NaN;
            spacecraftEntry = null;
            destinationEntry = null;

            if (spacecraftExit == null ||
                destinationBody == null ||
                destinationBody.Orbit == null ||
                !FinitePositive(destinationBody.SoiRadiusMeters) ||
                !Vector3d.Finite(lowUt) ||
                !Vector3d.Finite(highUt) ||
                highUt <= lowUt)
                return false;

            double lowDistance;

            if (!TryRelativeDistance(
                    spacecraftExit,
                    destinationBody,
                    parentMu,
                    lowUt,
                    out lowDistance,
                    out spacecraftEntry,
                    out destinationEntry))
                return false;

            double highDistance;
            StateVector highSpacecraft;
            StateVector highDestination;

            if (!TryRelativeDistance(
                    spacecraftExit,
                    destinationBody,
                    parentMu,
                    highUt,
                    out highDistance,
                    out highSpacecraft,
                    out highDestination))
                return false;

            if (highDistance >
                destinationBody.SoiRadiusMeters)
                return false;

            if (lowDistance <=
                destinationBody.SoiRadiusMeters)
            {
                entryUt = lowUt;
                return true;
            }

            double low =
                lowUt;

            double high =
                highUt;

            for (int i = 0;
                i < TargetEntryBisectionIterations;
                i++)
            {
                double mid =
                    0.5 *
                    (low + high);

                double midDistance;
                StateVector midSpacecraft;
                StateVector midDestination;

                if (!TryRelativeDistance(
                        spacecraftExit,
                        destinationBody,
                        parentMu,
                        mid,
                        out midDistance,
                        out midSpacecraft,
                        out midDestination))
                    return false;

                if (midDistance >
                    destinationBody.SoiRadiusMeters)
                    low = mid;
                else
                    high = mid;
            }

            entryUt =
                high;

            double finalDistance;

            return
                TryRelativeDistance(
                    spacecraftExit,
                    destinationBody,
                    parentMu,
                    entryUt,
                    out finalDistance,
                    out spacecraftEntry,
                    out destinationEntry);
        }

        private static bool TryRelativeDistance(
            StateVector spacecraftExit,
            CelestialBodyState destinationBody,
            double parentMu,
            double ut,
            out double distance,
            out StateVector spacecraft,
            out StateVector destination)
        {
            distance = double.NaN;
            spacecraft = null;
            destination = null;

            if (!StateVectorPropagator.TryPropagate(
                    spacecraftExit,
                    ut,
                    out spacecraft))
                return false;

            if (!KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentMu,
                    ut,
                    out destination))
                return false;

            distance =
                (spacecraft.Position -
                 destination.Position).Magnitude;

            return
                Vector3d.Finite(distance);
        }

        private static bool TryCalculatePeriapsisRadius(
            Vector3d position,
            Vector3d velocity,
            double mu,
            out double periapsisRadius)
        {
            periapsisRadius = double.NaN;

            double r =
                position.Magnitude;

            if (!FinitePositive(r) ||
                !FinitePositive(mu) ||
                !position.IsFinite ||
                !velocity.IsFinite)
                return false;

            Vector3d hVector =
                Vector3d.Cross(
                    position,
                    velocity);

            double h =
                hVector.Magnitude;

            if (!FinitePositive(h))
                return false;

            double v2 =
                Vector3d.Dot(
                    velocity,
                    velocity);

            double rv =
                Vector3d.Dot(
                    position,
                    velocity);

            Vector3d eccentricityVector =
                position *
                    ((v2 - mu / r) / mu) -
                velocity *
                    (rv / mu);

            double eccentricity =
                eccentricityVector.Magnitude;

            if (!Vector3d.Finite(eccentricity))
                return false;

            double p =
                h * h /
                mu;

            periapsisRadius =
                p /
                (1.0 + eccentricity);

            return
                FinitePositive(
                    periapsisRadius);
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

            /*
             * Primary path: solve the outbound hyperbolic SOI crossing
             * directly from the osculating two-body conic. This is both
             * cheaper and more robust than asking the generic Cartesian
             * propagator to bracket a very long escape coast.
             */
            if (HyperbolicSoiExitSolver.TrySolve(
                    initial,
                    soiRadius,
                    out exitUt,
                    out exitState))
                return true;

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
