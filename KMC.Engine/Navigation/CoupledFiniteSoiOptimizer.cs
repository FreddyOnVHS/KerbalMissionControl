using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// 14.22.62 coupled finite-SOI optimizer, split-transfer + robust general B-plane terminal stage.
    ///
    /// Shadow mode only. No result from this solver has maneuver authority.
    ///
    /// This stage follows the MechJeb-style split heliocentric formulation:
    /// the source-SOI state is joined to a first Lambert leg, a FREE parent-
    /// frame midpoint position joins the first and second Lambert legs, and
    /// the second leg terminates at the propagated target body. Burn UT,
    /// parking-orbit P/N/R impulse, midpoint XYZ, and arrival UT are solved
    /// together. The midpoint is a mathematical multiple-shooting variable;
    /// it is not a maneuver and never becomes a KSP node.
    ///
    /// Stage 1 minimizes coupled feasibility residuals. Departure delta-v is
    /// retained as the tie-break objective. Once finite-SOI feasibility is genuinely established, Stage 2 keeps
    /// those continuity constraints active and adds target hyperbolic
    /// periapsis geometry as a terminal constraint. The feasible split
    /// solution is the Stage-2 seed; no independent P/N/R coordinate search
    /// is introduced.
    /// </summary>
    public static class CoupledFiniteSoiOptimizer
    {
        private const int VariableCount = 8;
        private const int ResidualCount = 9;
        private const int TerminalResidualCount = 11;
        private const int MaximumTerminalIterations = 42;
        private const int MaximumIterations = 36;
        private const double InitialDamping = 1e-2;
        private const double MinimumDamping = 1e-9;
        private const double MaximumDamping = 1e10;
        private const double InitialTrustRadius = 0.50;
        private const double MaximumTrustRadius = 4.0;
        private const double FiniteDifferenceStep = 1e-3;
        private const double TerminalFiniteDifferenceStep = 2e-3;
        private const double MinimumTerminalFiniteDifferenceStep = 1.25e-4;
        private const int MaximumTerminalDerivativeRetries = 5;
        private const int MaximumTerminalStepRetries = 6;
        private const int MinimumUsableTerminalJacobianColumns = 4;
        private const double MinimumStepNorm = 1e-6;
        private const double ScoreTieTolerance = 1e-8;

        public static bool TrySolveShadow(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrapEjection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            out CoupledFiniteSoiShadowResult result,
            out string failureReason)
        {
            result = null;
            failureReason = string.Empty;

            if (transfer == null ||
                bootstrapEjection == null ||
                parkingOrbit == null ||
                originBody == null ||
                destinationBody == null ||
                originBody.Orbit == null ||
                destinationBody.Orbit == null ||
                !FinitePositive(parentGravParameter))
            {
                failureReason = "COUPLED INPUT / BODY DATA";
                return false;
            }

            StateVector bootstrapExit;
            bool bootstrapOutbound;
            if (!TryCreateParentFrameSourceExit(
                    bootstrapEjection,
                    parkingOrbit,
                    originBody,
                    parentGravParameter,
                    out bootstrapExit,
                    out bootstrapOutbound))
            {
                failureReason = "COUPLED BOOTSTRAP SOURCE EXIT";
                return false;
            }

            double bootstrapArrivalUt = transfer.ArrivalUniversalTimeSeconds;
            if (!Finite(bootstrapArrivalUt) ||
                bootstrapArrivalUt <= bootstrapExit.UniversalTimeSeconds + 2.0)
            {
                failureReason = "COUPLED BOOTSTRAP ARRIVAL UT";
                return false;
            }

            double bootstrapSplitUt =
                0.5 * (bootstrapExit.UniversalTimeSeconds + bootstrapArrivalUt);

            StateVector lambertInitial =
                new StateVector(
                    transfer.OriginDepartureState.Position,
                    transfer.LambertSolution.DepartureVelocity,
                    transfer.DepartureUniversalTimeSeconds,
                    parentGravParameter,
                    originBody.ParentName);

            StateVector bootstrapMidpointState;
            if (!StateVectorPropagator.TryPropagate(
                    lambertInitial,
                    bootstrapSplitUt,
                    out bootstrapMidpointState))
            {
                if (!StateVectorPropagator.TryPropagate(
                        bootstrapExit,
                        bootstrapSplitUt,
                        out bootstrapMidpointState))
                {
                    failureReason = "COUPLED BOOTSTRAP MIDPOINT";
                    return false;
                }
            }

            double parkingPeriod = ResolveParkingPeriodSeconds(
                parkingOrbit,
                originBody.GravParameter);

            double dvScale = Math.Max(
                25.0,
                bootstrapEjection.TotalDeltaVMetersPerSecond * 0.15);

            double midpointScale = Math.Max(
                destinationBody.SoiRadiusMeters * 4.0,
                bootstrapMidpointState.Position.Magnitude * 0.03);

            double arrivalScale = Math.Max(
                3600.0,
                transfer.TimeOfFlightSeconds * 0.05);

            double[] parameterScale =
            {
                Math.Max(30.0, parkingPeriod * 0.25),
                dvScale,
                dvScale,
                dvScale,
                midpointScale,
                midpointScale,
                midpointScale,
                arrivalScale
            };

            Vector3d baseMidpointPosition = bootstrapMidpointState.Position;

            // q = burnUT, P, N, R, midpoint X/Y/Z, arrivalUT offsets.
            double[] q = new double[VariableCount];
            double[] lower = { -2.0, -2.5, -2.5, -2.5, -3.0, -3.0, -3.0, -2.0 };
            double[] upper = {  2.0,  2.5,  2.5,  2.5,  3.0,  3.0,  3.0,  2.0 };

            ConstraintEvaluation current;
            int evaluations = 0;
            if (!TryEvaluateConstraints(
                    transfer,
                    bootstrapEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    q,
                    parameterScale,
                    baseMidpointPosition,
                    out current))
            {
                failureReason = "COUPLED SPLIT BOOTSTRAP RESIDUAL";
                return false;
            }
            evaluations++;

            double[] bestQ = Copy(q);
            ConstraintEvaluation best = current;
            double damping = InitialDamping;
            double trustRadius = InitialTrustRadius;
            int completedIterations = 0;

            for (int iteration = 0; iteration < MaximumIterations; iteration++)
            {
                completedIterations = iteration + 1;

                if (IsSplitFeasible(current, transfer, destinationBody))
                    break;

                double[,] jacobian = new double[ResidualCount, VariableCount];
                bool jacobianOk = true;

                for (int variable = 0; variable < VariableCount; variable++)
                {
                    double h = FiniteDifferenceStep;
                    double[] plusQ = Copy(q);
                    double[] minusQ = Copy(q);
                    plusQ[variable] = Clamp(q[variable] + h, lower[variable], upper[variable]);
                    minusQ[variable] = Clamp(q[variable] - h, lower[variable], upper[variable]);

                    double denominator = plusQ[variable] - minusQ[variable];
                    if (Math.Abs(denominator) < 1e-12)
                    {
                        jacobianOk = false;
                        break;
                    }

                    ConstraintEvaluation plus = null;
                    ConstraintEvaluation minus = null;
                    if (!TryEvaluateConstraints(
                            transfer,
                            bootstrapEjection,
                            parkingOrbit,
                            originBody,
                            destinationBody,
                            parentGravParameter,
                            plusQ,
                            parameterScale,
                            baseMidpointPosition,
                            out plus) ||
                        !TryEvaluateConstraints(
                            transfer,
                            bootstrapEjection,
                            parkingOrbit,
                            originBody,
                            destinationBody,
                            parentGravParameter,
                            minusQ,
                            parameterScale,
                            baseMidpointPosition,
                            out minus))
                    {
                        jacobianOk = false;
                        break;
                    }
                    evaluations += 2;

                    for (int residual = 0; residual < ResidualCount; residual++)
                    {
                        jacobian[residual, variable] =
                            (plus.Residuals[residual] - minus.Residuals[residual]) /
                            denominator;
                    }
                }

                if (!jacobianOk)
                {
                    damping = Math.Min(MaximumDamping, damping * 10.0);
                    trustRadius = Math.Max(0.03, trustRadius * 0.5);
                    if (damping >= MaximumDamping)
                        break;
                    continue;
                }

                double[] step;
                if (!TrySolveDampedNormalEquations(
                        jacobian,
                        current.Residuals,
                        damping,
                        out step))
                {
                    damping = Math.Min(MaximumDamping, damping * 10.0);
                    trustRadius = Math.Max(0.03, trustRadius * 0.5);
                    if (damping >= MaximumDamping)
                        break;
                    continue;
                }

                double stepNorm = Norm(step);
                if (stepNorm > trustRadius && stepNorm > 0.0)
                {
                    double scale = trustRadius / stepNorm;
                    for (int i = 0; i < VariableCount; i++)
                        step[i] *= scale;
                    stepNorm = trustRadius;
                }

                if (stepNorm < MinimumStepNorm)
                    break;

                double[] trialQ = Copy(q);
                for (int i = 0; i < VariableCount; i++)
                    trialQ[i] = Clamp(q[i] + step[i], lower[i], upper[i]);

                ConstraintEvaluation trial;
                if (!TryEvaluateConstraints(
                        transfer,
                        bootstrapEjection,
                        parkingOrbit,
                        originBody,
                        destinationBody,
                        parentGravParameter,
                        trialQ,
                        parameterScale,
                        baseMidpointPosition,
                        out trial))
                {
                    damping = Math.Min(MaximumDamping, damping * 5.0);
                    trustRadius = Math.Max(0.03, trustRadius * 0.5);
                    continue;
                }
                evaluations++;

                if (IsBetterConstraintCandidate(trial, current))
                {
                    q = trialQ;
                    current = trial;
                    damping = Math.Max(MinimumDamping, damping / 3.0);
                    trustRadius = Math.Min(MaximumTrustRadius, trustRadius * 1.5);

                    if (IsBetterConstraintCandidate(current, best))
                    {
                        best = current;
                        bestQ = Copy(q);
                    }
                }
                else
                {
                    damping = Math.Min(MaximumDamping, damping * 5.0);
                    trustRadius = Math.Max(0.03, trustRadius * 0.5);
                    if (damping >= MaximumDamping)
                        break;
                }
            }

            bool splitFeasible = IsSplitFeasible(best, transfer, destinationBody);
            bool terminalSolved = false;
            bool bPlaneInitialized = false;
            int terminalIterations = 0;
            int terminalJacobianColumns = 0;
            int terminalRejectedSteps = 0;
            double terminalFinalTrustRadius = 0.0;

            // 14.22.61 Stage 2: preserve the solved split transfer and add
            // target hyperbolic B-plane geometry to the SAME coupled solve.
            // The first nine residuals remain active. Two additional
            // residuals are the radial B-vector error resolved in a local
            // B-plane basis. Their squared norm is the body-scaled B-magnitude
            // error, but the two components provide a better-conditioned
            // Jacobian than a single scalar periapsis residual. Requested
            // periapsis remains the physical terminal acceptance check.
            if (splitFeasible)
            {
                ConstraintEvaluation terminalCurrent;
                if (TryEvaluateTerminalConstraints(
                        transfer,
                        bootstrapEjection,
                        parkingOrbit,
                        originBody,
                        destinationBody,
                        parentGravParameter,
                        bestQ,
                        parameterScale,
                        baseMidpointPosition,
                        out terminalCurrent))
                {
                    evaluations++;
                    bPlaneInitialized = true;
                    double[] terminalQ = Copy(bestQ);
                    ConstraintEvaluation terminalBest = terminalCurrent;
                    double[] terminalBestQ = Copy(terminalQ);
                    double terminalDamping = InitialDamping;
                    double terminalTrustRadius = 0.20;

                    for (int iteration = 0; iteration < MaximumTerminalIterations; iteration++)
                    {
                        terminalIterations = iteration + 1;
                        if (IsTerminalFeasible(terminalCurrent, transfer, destinationBody))
                        {
                            terminalSolved = true;
                            break;
                        }

                        double[,] jacobian;
                        int usableColumns;
                        if (!TryBuildRobustTerminalJacobian(
                                transfer, bootstrapEjection, parkingOrbit, originBody,
                                destinationBody, parentGravParameter, terminalQ,
                                parameterScale, baseMidpointPosition, terminalCurrent,
                                out jacobian, out usableColumns, ref evaluations))
                        {
                            terminalJacobianColumns = usableColumns;
                            terminalDamping = Math.Min(MaximumDamping, terminalDamping * 10.0);
                            terminalTrustRadius = Math.Max(0.005, terminalTrustRadius * 0.5);
                            terminalRejectedSteps++;
                            if (terminalDamping >= MaximumDamping)
                                break;
                            continue;
                        }
                        terminalJacobianColumns = usableColumns;

                        double[] step;
                        if (!TrySolveDampedNormalEquations(
                                jacobian, terminalCurrent.Residuals, terminalDamping, out step))
                        {
                            terminalDamping = Math.Min(MaximumDamping, terminalDamping * 10.0);
                            terminalTrustRadius = Math.Max(0.005, terminalTrustRadius * 0.5);
                            terminalRejectedSteps++;
                            continue;
                        }

                        double stepNorm = Norm(step);
                        if (stepNorm > terminalTrustRadius && stepNorm > 0.0)
                        {
                            double stepScale = terminalTrustRadius / stepNorm;
                            for (int i = 0; i < VariableCount; i++)
                                step[i] *= stepScale;
                            stepNorm = terminalTrustRadius;
                        }
                        if (stepNorm < MinimumStepNorm)
                        {
                            terminalDamping = Math.Min(MaximumDamping, terminalDamping * 5.0);
                            terminalTrustRadius = Math.Max(0.005, terminalTrustRadius * 0.5);
                            terminalRejectedSteps++;
                            if (terminalTrustRadius <= 0.005)
                                break;
                            continue;
                        }

                        bool accepted = false;
                        double retryScale = 1.0;
                        for (int retry = 0; retry < MaximumTerminalStepRetries; retry++)
                        {
                            double[] trialQ = Copy(terminalQ);
                            for (int i = 0; i < VariableCount; i++)
                                trialQ[i] = Clamp(
                                    terminalQ[i] + step[i] * retryScale,
                                    lower[i], upper[i]);

                            ConstraintEvaluation trial;
                            bool trialOk = TryEvaluateTerminalConstraints(
                                transfer, bootstrapEjection, parkingOrbit, originBody,
                                destinationBody, parentGravParameter, trialQ, parameterScale,
                                baseMidpointPosition, out trial);
                            if (trialOk)
                                evaluations++;

                            if (trialOk && IsBetterConstraintCandidate(trial, terminalCurrent))
                            {
                                terminalQ = trialQ;
                                terminalCurrent = trial;
                                terminalDamping = Math.Max(MinimumDamping, terminalDamping / 3.0);
                                terminalTrustRadius = Math.Min(1.0,
                                    Math.Max(0.01, terminalTrustRadius * 1.4));
                                if (IsBetterConstraintCandidate(terminalCurrent, terminalBest))
                                {
                                    terminalBest = terminalCurrent;
                                    terminalBestQ = Copy(terminalQ);
                                }
                                accepted = true;
                                break;
                            }

                            terminalRejectedSteps++;
                            retryScale *= 0.5;
                        }

                        if (!accepted)
                        {
                            terminalDamping = Math.Min(MaximumDamping, terminalDamping * 5.0);
                            terminalTrustRadius = Math.Max(0.005, terminalTrustRadius * 0.5);
                            if (terminalDamping >= MaximumDamping)
                                break;
                        }
                    }

                    terminalFinalTrustRadius = terminalTrustRadius;

                    // Keep the best Stage-2 point whenever it preserves the
                    // split encounter. Terminal and Stage-1 scores have
                    // different residual dimensions, so they must not be
                    // compared numerically against each other.
                    if (IsSplitFeasible(terminalBest, transfer, destinationBody) &&
                        terminalBest.TargetInbound)
                    {
                        best = terminalBest;
                        bestQ = terminalBestQ;
                    }
                    terminalSolved = IsTerminalFeasible(best, transfer, destinationBody);
                }
            }

            completedIterations += terminalIterations;

            LambertParkingOrbitEjectionSolution bestEjection =
                CreateCandidate(bootstrapEjection, bestQ, parameterScale);

            FiniteSoiDepartureAssessment bestSource;
            TargetSoiShootingAssessment bestTarget;
            bool bestOutbound;
            bool bestInbound;
            if (!CoupledFiniteSoiTrajectoryEvaluator.TryEvaluate(
                    transfer,
                    bestEjection,
                    parkingOrbit,
                    originBody,
                    destinationBody,
                    parentGravParameter,
                    out bestSource,
                    out bestTarget,
                    out bestOutbound,
                    out bestInbound,
                    best.ArrivalUniversalTimeSeconds))
            {
                failureReason = "COUPLED SPLIT FINAL EVALUATION";
                return false;
            }
            evaluations++;

            bool feasibilitySucceeded =
                best.SourceOutbound &&
                bestOutbound &&
                best.SourceVelocityMismatchMetersPerSecond <=
                    SourceVelocityToleranceMetersPerSecond(transfer) &&
                best.MidpointVelocityMismatchMetersPerSecond <=
                    MidpointVelocityToleranceMetersPerSecond(transfer) &&
                bestTarget != null &&
                bestTarget.PredictedEncounter &&
                bestInbound;

            result =
                new CoupledFiniteSoiShadowResult
                {
                    BootstrapEjection = bootstrapEjection,
                    FinalEjection = bestEjection,
                    SourceAssessment = bestSource,
                    TargetAssessment = bestTarget,
                    FeasibilityPassSucceeded = feasibilitySucceeded,
                    BPlaneInitializationApplied = bPlaneInitialized,
                    BootstrapDeltaVMetersPerSecond =
                        bootstrapEjection.TotalDeltaVMetersPerSecond,
                    FinalDeltaVMetersPerSecond =
                        bestEjection.TotalDeltaVMetersPerSecond,
                    // Split leg 1 starts exactly at the physical source-SOI
                    // position. Velocity continuity is the nontrivial source
                    // interface constraint in this multiple-shooting form.
                    SourceInterfacePositionErrorMeters = 0.0,
                    SourceInterfaceVelocityErrorMetersPerSecond =
                        best.SourceVelocityMismatchMetersPerSecond,
                    SplitVelocityMismatchMetersPerSecond =
                        best.MidpointVelocityMismatchMetersPerSecond,
                    OptimizedArrivalUniversalTimeSeconds =
                        best.ArrivalUniversalTimeSeconds,
                    TargetInterfaceErrorMeters =
                        ComputeTargetInterfaceConstraintError(
                            bestTarget,
                            destinationBody.SoiRadiusMeters),
                    TargetPeriapsisErrorMeters =
                        bestTarget.TargetPeriapsisErrorMeters,
                    TargetBPlaneMagnitudeErrorMeters =
                        best.TargetBPlaneMagnitudeErrorMeters,
                    TargetBPlaneTErrorMeters = best.TargetBPlaneTErrorMeters,
                    TargetBPlaneRErrorMeters = best.TargetBPlaneRErrorMeters,
                    TerminalJacobianColumns = terminalJacobianColumns,
                    TerminalRejectedSteps = terminalRejectedSteps,
                    TerminalTrustRadius = terminalFinalTrustRadius,
                    SourceOutbound = bestOutbound,
                    TargetInbound = bestInbound,
                    Iterations = completedIterations,
                    Evaluations = evaluations,
                    Stage = terminalSolved
                        ? "B-PLANE / PE SOLVED"
                        : (feasibilitySucceeded
                            ? "B-PLANE / PE ITERATING"
                            : "SPLIT FEASIBILITY ITERATING")
                };

            return true;
        }

        private static bool TryEvaluateConstraints(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrap,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double[] q,
            double[] scale,
            Vector3d baseMidpointPosition,
            out ConstraintEvaluation evaluation)
        {
            evaluation = null;

            LambertParkingOrbitEjectionSolution candidate =
                CreateCandidate(bootstrap, q, scale);

            StateVector actualExit;
            bool sourceOutbound;
            if (!TryCreateParentFrameSourceExit(
                    candidate,
                    parkingOrbit,
                    originBody,
                    parentMu,
                    out actualExit,
                    out sourceOutbound))
                return false;

            double arrivalUt =
                transfer.ArrivalUniversalTimeSeconds + q[7] * scale[7];
            if (!Finite(arrivalUt) ||
                arrivalUt <= actualExit.UniversalTimeSeconds + 2.0)
                return false;

            double splitUt =
                0.5 * (actualExit.UniversalTimeSeconds + arrivalUt);
            double leg1Time = splitUt - actualExit.UniversalTimeSeconds;
            double leg2Time = arrivalUt - splitUt;
            if (!FinitePositive(leg1Time) || !FinitePositive(leg2Time))
                return false;

            Vector3d midpointPosition =
                baseMidpointPosition +
                new Vector3d(
                    q[4] * scale[4],
                    q[5] * scale[5],
                    q[6] * scale[6]);
            if (!midpointPosition.IsFinite ||
                !FinitePositive(midpointPosition.Magnitude))
                return false;

            StateVector destinationArrival;
            if (!KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentMu,
                    arrivalUt,
                    out destinationArrival))
                return false;

            LambertSolution leg1;
            LambertSolution leg2;
            if (!TrySolveSplitLambert(
                    actualExit.Position,
                    midpointPosition,
                    leg1Time,
                    parentMu,
                    out leg1) ||
                !TrySolveSplitLambert(
                    midpointPosition,
                    destinationArrival.Position,
                    leg2Time,
                    parentMu,
                    out leg2))
                return false;

            Vector3d sourceVelocityResidual =
                actualExit.Velocity - leg1.DepartureVelocity;
            Vector3d midpointVelocityResidual =
                leg1.ArrivalVelocity - leg2.DepartureVelocity;

            StateVector actualArrival;
            if (!StateVectorPropagator.TryPropagate(
                    actualExit,
                    arrivalUt,
                    out actualArrival))
                return false;

            Vector3d targetPositionResidual =
                actualArrival.Position - destinationArrival.Position;
            Vector3d targetRelativeVelocity =
                actualArrival.Velocity - destinationArrival.Velocity;

            double sourceVelocityTolerance =
                SourceVelocityToleranceMetersPerSecond(transfer);
            double midpointVelocityTolerance =
                MidpointVelocityToleranceMetersPerSecond(transfer);
            double targetTolerance = destinationBody.SoiRadiusMeters;

            double[] residuals =
            {
                sourceVelocityResidual.X / sourceVelocityTolerance,
                sourceVelocityResidual.Y / sourceVelocityTolerance,
                sourceVelocityResidual.Z / sourceVelocityTolerance,
                midpointVelocityResidual.X / midpointVelocityTolerance,
                midpointVelocityResidual.Y / midpointVelocityTolerance,
                midpointVelocityResidual.Z / midpointVelocityTolerance,
                targetPositionResidual.X / targetTolerance,
                targetPositionResidual.Y / targetTolerance,
                targetPositionResidual.Z / targetTolerance
            };

            for (int i = 0; i < residuals.Length; i++)
            {
                if (!Finite(residuals[i]))
                    return false;
            }

            evaluation =
                new ConstraintEvaluation
                {
                    Candidate = candidate,
                    Residuals = residuals,
                    Score = SumSquares(residuals),
                    SourceVelocityMismatchMetersPerSecond =
                        sourceVelocityResidual.Magnitude,
                    MidpointVelocityMismatchMetersPerSecond =
                        midpointVelocityResidual.Magnitude,
                    TargetPositionErrorMeters =
                        targetPositionResidual.Magnitude,
                    SourceOutbound = sourceOutbound,
                    TargetClosingAtArrival =
                        Vector3d.Dot(
                            targetPositionResidual,
                            targetRelativeVelocity) < 0.0,
                    ArrivalUniversalTimeSeconds = arrivalUt,
                    SplitUniversalTimeSeconds = splitUt,
                    MidpointPosition = midpointPosition
                };

            return Finite(evaluation.Score) &&
                Finite(evaluation.SourceVelocityMismatchMetersPerSecond) &&
                Finite(evaluation.MidpointVelocityMismatchMetersPerSecond) &&
                Finite(evaluation.TargetPositionErrorMeters);
        }

        private static bool TryBuildRobustTerminalJacobian(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrap,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double[] q,
            double[] scale,
            Vector3d baseMidpointPosition,
            ConstraintEvaluation current,
            out double[,] jacobian,
            out int usableColumns,
            ref int evaluations)
        {
            jacobian = new double[TerminalResidualCount, VariableCount];
            usableColumns = 0;
            if (current == null || current.Residuals == null ||
                current.Residuals.Length != TerminalResidualCount)
                return false;

            for (int variable = 0; variable < VariableCount; variable++)
            {
                bool columnBuilt = false;
                double h = TerminalFiniteDifferenceStep;

                for (int attempt = 0; attempt < MaximumTerminalDerivativeRetries; attempt++)
                {
                    double[] plusQ = Copy(q);
                    double[] minusQ = Copy(q);
                    plusQ[variable] = Clamp(q[variable] + h, -TerminalVariableBound(variable), TerminalVariableBound(variable));
                    minusQ[variable] = Clamp(q[variable] - h, -TerminalVariableBound(variable), TerminalVariableBound(variable));

                    double plusStep = plusQ[variable] - q[variable];
                    double minusStep = q[variable] - minusQ[variable];
                    if (Math.Abs(plusStep) < 1e-12 && Math.Abs(minusStep) < 1e-12)
                        break;

                    ConstraintEvaluation plus = null;
                    ConstraintEvaluation minus = null;
                    bool plusOk = Math.Abs(plusStep) >= 1e-12 &&
                        TryEvaluateTerminalConstraints(
                            transfer, bootstrap, parkingOrbit, originBody, destinationBody,
                            parentMu, plusQ, scale, baseMidpointPosition, out plus);
                    bool minusOk = Math.Abs(minusStep) >= 1e-12 &&
                        TryEvaluateTerminalConstraints(
                            transfer, bootstrap, parkingOrbit, originBody, destinationBody,
                            parentMu, minusQ, scale, baseMidpointPosition, out minus);
                    if (plusOk) evaluations++;
                    if (minusOk) evaluations++;

                    if (plusOk && minusOk)
                    {
                        double denominator = plusQ[variable] - minusQ[variable];
                        if (Math.Abs(denominator) >= 1e-12)
                        {
                            for (int residual = 0; residual < TerminalResidualCount; residual++)
                                jacobian[residual, variable] =
                                    (plus.Residuals[residual] - minus.Residuals[residual]) / denominator;
                            columnBuilt = IsFiniteJacobianColumn(jacobian, variable);
                        }
                    }
                    // Fall back to a one-sided derivative when only one side
                    // remains inside the valid terminal encounter domain.
                    else if (plusOk)
                    {
                        for (int residual = 0; residual < TerminalResidualCount; residual++)
                            jacobian[residual, variable] =
                                (plus.Residuals[residual] - current.Residuals[residual]) / plusStep;
                        columnBuilt = IsFiniteJacobianColumn(jacobian, variable);
                    }
                    else if (minusOk)
                    {
                        for (int residual = 0; residual < TerminalResidualCount; residual++)
                            jacobian[residual, variable] =
                                (current.Residuals[residual] - minus.Residuals[residual]) / minusStep;
                        columnBuilt = IsFiniteJacobianColumn(jacobian, variable);
                    }

                    if (columnBuilt)
                        break;

                    h *= 0.5;
                    if (h < MinimumTerminalFiniteDifferenceStep)
                        break;
                }

                if (columnBuilt)
                    usableColumns++;
                else
                {
                    // Leave an unavailable column at zero. Damped normal
                    // equations can still make progress with the remaining
                    // independent directions; one bad SOI-boundary derivative
                    // must not discard the whole terminal Jacobian.
                    for (int residual = 0; residual < TerminalResidualCount; residual++)
                        jacobian[residual, variable] = 0.0;
                }
            }

            return usableColumns >= MinimumUsableTerminalJacobianColumns;
        }

        private static double TerminalVariableBound(int variable)
        {
            // Mirrors the normalized q bounds used by the solve. Keeping this
            // local helper avoids destination-specific derivative behavior.
            if (variable == 0 || variable == 7)
                return 2.0;
            if (variable >= 1 && variable <= 3)
                return 2.5;
            return 3.0;
        }

        private static bool IsFiniteJacobianColumn(double[,] jacobian, int column)
        {
            bool hasSignal = false;
            for (int residual = 0; residual < TerminalResidualCount; residual++)
            {
                double value = jacobian[residual, column];
                if (!Finite(value))
                    return false;
                if (Math.Abs(value) > 1e-12)
                    hasSignal = true;
            }
            return hasSignal;
        }

        private static bool TryEvaluateTerminalConstraints(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution bootstrap,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            double[] q,
            double[] scale,
            Vector3d baseMidpointPosition,
            out ConstraintEvaluation evaluation)
        {
            evaluation = null;
            ConstraintEvaluation baseEvaluation;
            if (!TryEvaluateConstraints(
                    transfer, bootstrap, parkingOrbit, originBody, destinationBody,
                    parentMu, q, scale, baseMidpointPosition, out baseEvaluation))
                return false;

            FiniteSoiDepartureAssessment sourceAssessment;
            TargetSoiShootingAssessment targetAssessment;
            bool sourceOutbound;
            bool targetInbound;
            if (!CoupledFiniteSoiTrajectoryEvaluator.TryEvaluate(
                    transfer, baseEvaluation.Candidate, parkingOrbit, originBody,
                    destinationBody, parentMu, out sourceAssessment, out targetAssessment,
                    out sourceOutbound, out targetInbound,
                    baseEvaluation.ArrivalUniversalTimeSeconds))
                return false;

            if (targetAssessment == null ||
                !targetAssessment.PredictedEncounter ||
                !targetInbound ||
                !Finite(targetAssessment.TargetPeriapsisRadiusMeters) ||
                !FinitePositive(targetAssessment.DesiredPeriapsisRadiusMeters))
                return false;

            double signedPeError =
                targetAssessment.TargetPeriapsisRadiusMeters -
                targetAssessment.DesiredPeriapsisRadiusMeters;

            double bTError;
            double bRError;
            double bMagnitudeError;
            double desiredBRadius;
            if (!TryComputeBPlaneRadialErrors(
                    targetAssessment,
                    destinationBody,
                    out bTError,
                    out bRError,
                    out bMagnitudeError,
                    out desiredBRadius))
                return false;

            double bTolerance = TargetBPlaneToleranceMeters(desiredBRadius);
            double[] residuals = new double[TerminalResidualCount];
            for (int i = 0; i < ResidualCount; i++)
                residuals[i] = baseEvaluation.Residuals[i];

            // These two components are the radial distance from the current
            // B-vector to the desired-B circle, expressed in a local B-plane
            // basis. They do not impose an arbitrary clock angle: the nearest
            // point on the desired circle is used for every evaluation.
            residuals[9] = bTError / bTolerance;
            residuals[10] = bRError / bTolerance;

            baseEvaluation.Residuals = residuals;
            baseEvaluation.Score = SumSquares(residuals);
            baseEvaluation.TargetPeriapsisErrorMeters = Math.Abs(signedPeError);
            baseEvaluation.TargetBPlaneMagnitudeErrorMeters = bMagnitudeError;
            baseEvaluation.TargetBPlaneTErrorMeters = bTError;
            baseEvaluation.TargetBPlaneRErrorMeters = bRError;
            baseEvaluation.TargetInbound = targetInbound;
            baseEvaluation.TargetAssessment = targetAssessment;
            evaluation = baseEvaluation;
            return Finite(evaluation.Score);
        }

        private static bool IsTerminalFeasible(
            ConstraintEvaluation evaluation,
            TransferSearchSolution transfer,
            CelestialBodyState destinationBody)
        {
            if (evaluation == null ||
                !evaluation.TargetInbound ||
                evaluation.TargetAssessment == null ||
                !evaluation.TargetAssessment.PredictedEncounter ||
                !IsSplitFeasible(evaluation, transfer, destinationBody))
                return false;

            double desiredB = evaluation.TargetAssessment.DesiredBPlaneRadiusMeters;
            return evaluation.TargetPeriapsisErrorMeters <=
                    TargetPeriapsisToleranceMeters(evaluation.TargetAssessment) &&
                FinitePositive(desiredB) &&
                evaluation.TargetBPlaneMagnitudeErrorMeters <=
                    TargetBPlaneToleranceMeters(desiredB);
        }

        private static bool TryComputeBPlaneRadialErrors(
            TargetSoiShootingAssessment assessment,
            CelestialBodyState destinationBody,
            out double bTErrorMeters,
            out double bRErrorMeters,
            out double magnitudeErrorMeters,
            out double desiredBRadiusMeters)
        {
            bTErrorMeters = double.NaN;
            bRErrorMeters = double.NaN;
            magnitudeErrorMeters = double.NaN;
            desiredBRadiusMeters = double.NaN;

            if (assessment == null || destinationBody == null ||
                !assessment.TargetSoiEntryRelativePosition.IsFinite ||
                !assessment.TargetSoiEntryRelativeVelocity.IsFinite)
                return false;

            double actualB;
            double desiredB;
            double ignoredError;
            Vector3d sHat;
            Vector3d bVector;
            if (!TargetBPlanePlanner.TryCalculateGeometry(
                    assessment.TargetSoiEntryRelativePosition,
                    assessment.TargetSoiEntryRelativeVelocity,
                    destinationBody.GravParameter,
                    destinationBody.SoiRadiusMeters,
                    assessment.DesiredPeriapsisRadiusMeters,
                    out sHat,
                    out bVector,
                    out actualB,
                    out desiredB,
                    out ignoredError) ||
                !FinitePositive(actualB) || !FinitePositive(desiredB) ||
                !sHat.IsFinite || !bVector.IsFinite)
                return false;

            double bMagnitude = bVector.Magnitude;
            if (!FinitePositive(bMagnitude))
                return false;

            // Construct a stable local basis perpendicular to the TRUE incoming
            // hyperbolic asymptote, not the instantaneous velocity direction at
            // the finite SOI boundary.  The reference axis is selected
            // geometrically, never by destination identity. Since we target the
            // nearest point on the desired-B circle, this basis does not
            // prescribe clock angle.
            Vector3d reference = Math.Abs(sHat.Z) < 0.90
                ? new Vector3d(0.0, 0.0, 1.0)
                : new Vector3d(0.0, 1.0, 0.0);
            Vector3d tHat;
            if (!Normalize(Vector3d.Cross(reference, sHat), out tHat))
                return false;
            Vector3d rHat;
            if (!Normalize(Vector3d.Cross(sHat, tHat), out rHat))
                return false;

            double bT = Vector3d.Dot(bVector, tHat);
            double bR = Vector3d.Dot(bVector, rHat);
            double radialScale = 1.0 - desiredB / bMagnitude;

            bTErrorMeters = bT * radialScale;
            bRErrorMeters = bR * radialScale;
            magnitudeErrorMeters = Math.Abs(bMagnitude - desiredB);
            desiredBRadiusMeters = desiredB;

            return Finite(bTErrorMeters) && Finite(bRErrorMeters) &&
                Finite(magnitudeErrorMeters);
        }

        private static double TargetBPlaneToleranceMeters(double desiredBRadiusMeters)
        {
            if (!FinitePositive(desiredBRadiusMeters))
                return 1000.0;

            // Same relative precision policy as the Pe check, expressed in the
            // actual terminal coordinate.  No body-specific constants.
            return Math.Max(1000.0, desiredBRadiusMeters * 0.002);
        }

        private static double TargetPeriapsisToleranceMeters(
            TargetSoiShootingAssessment assessment)
        {
            if (assessment == null ||
                !FinitePositive(assessment.DesiredPeriapsisRadiusMeters))
                return 1000.0;

            // Body-scaled, never destination-specific. 0.2% of requested Pe
            // with a 1 km floor is tight enough for node validation while
            // remaining numerically reasonable across stock and modded bodies.
            return Math.Max(1000.0,
                assessment.DesiredPeriapsisRadiusMeters * 0.002);
        }

        private static bool TrySolveSplitLambert(
            Vector3d departurePosition,
            Vector3d arrivalPosition,
            double timeOfFlightSeconds,
            double mu,
            out LambertSolution solution)
        {
            // A free midpoint keeps each arc away from the single-leg near-180
            // singularity. Try short-way first; long-way remains a fallback so
            // the solver is destination-data driven rather than planet-specific.
            if (LambertSolver.TrySolve(
                    departurePosition,
                    arrivalPosition,
                    timeOfFlightSeconds,
                    mu,
                    LambertTransferPath.ShortWay,
                    out solution))
                return true;

            return LambertSolver.TrySolve(
                departurePosition,
                arrivalPosition,
                timeOfFlightSeconds,
                mu,
                LambertTransferPath.LongWay,
                out solution);
        }

        private static bool IsSplitFeasible(
            ConstraintEvaluation evaluation,
            TransferSearchSolution transfer,
            CelestialBodyState destinationBody)
        {
            return evaluation != null &&
                evaluation.SourceOutbound &&
                evaluation.SourceVelocityMismatchMetersPerSecond <=
                    SourceVelocityToleranceMetersPerSecond(transfer) &&
                evaluation.MidpointVelocityMismatchMetersPerSecond <=
                    MidpointVelocityToleranceMetersPerSecond(transfer) &&
                evaluation.TargetPositionErrorMeters <=
                    destinationBody.SoiRadiusMeters;
        }

        private static bool TryCreateParentFrameSourceExit(
            LambertParkingOrbitEjectionSolution ejection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            double parentMu,
            out StateVector actualExit,
            out bool outbound)
        {
            actualExit = null;
            outbound = false;

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
            if (!Normalize(parkingState.Velocity, out prograde) ||
                !Normalize(
                    Vector3d.Cross(parkingState.Position, parkingState.Velocity),
                    out normal) ||
                !Normalize(Vector3d.Cross(prograde, normal), out radial))
                return false;

            Vector3d postBurnVelocity =
                parkingState.Velocity +
                prograde * ejection.ProgradeDeltaVMetersPerSecond +
                normal * ejection.NormalDeltaVMetersPerSecond +
                radial * ejection.RadialDeltaVMetersPerSecond;

            StateVector relativeBurnState =
                new StateVector(
                    parkingState.Position,
                    postBurnVelocity,
                    ejection.BurnUniversalTimeSeconds,
                    originBody.GravParameter,
                    originBody.Name);

            double exitUt;
            StateVector relativeExit;
            if (!HyperbolicSoiExitSolver.TrySolve(
                    relativeBurnState,
                    originBody.SoiRadiusMeters,
                    out exitUt,
                    out relativeExit))
                return false;

            outbound =
                Vector3d.Dot(relativeExit.Position, relativeExit.Velocity) > 0.0;

            StateVector originExit;
            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentMu,
                    exitUt,
                    out originExit))
                return false;

            actualExit =
                new StateVector(
                    originExit.Position + relativeExit.Position,
                    originExit.Velocity + relativeExit.Velocity,
                    exitUt,
                    parentMu,
                    originBody.ParentName);

            return actualExit.Position.IsFinite && actualExit.Velocity.IsFinite;
        }

        private static double SourceVelocityToleranceMetersPerSecond(
            TransferSearchSolution transfer)
        {
            return Math.Max(
                1.0,
                Math.Min(
                    5.0,
                    transfer.DepartureExcessSpeedMetersPerSecond * 0.005));
        }

        private static double MidpointVelocityToleranceMetersPerSecond(
            TransferSearchSolution transfer)
        {
            return Math.Max(
                1.0,
                Math.Min(
                    5.0,
                    transfer.DepartureExcessSpeedMetersPerSecond * 0.005));
        }

        private static bool IsBetterConstraintCandidate(
            ConstraintEvaluation candidate,
            ConstraintEvaluation current)
        {
            if (candidate == null)
                return false;
            if (current == null)
                return true;

            if (candidate.Score < current.Score - ScoreTieTolerance)
                return true;
            if (candidate.Score > current.Score + ScoreTieTolerance)
                return false;

            return candidate.Candidate.TotalDeltaVMetersPerSecond <
                current.Candidate.TotalDeltaVMetersPerSecond;
        }

        private static bool TrySolveDampedNormalEquations(
            double[,] jacobian,
            double[] residuals,
            double damping,
            out double[] step)
        {
            step = null;
            double[,] a = new double[VariableCount, VariableCount];
            double[] b = new double[VariableCount];

            for (int row = 0; row < residuals.Length; row++)
            {
                for (int i = 0; i < VariableCount; i++)
                {
                    double ji = jacobian[row, i];
                    b[i] -= ji * residuals[row];
                    for (int j = 0; j < VariableCount; j++)
                        a[i, j] += ji * jacobian[row, j];
                }
            }

            for (int i = 0; i < VariableCount; i++)
                a[i, i] += damping * Math.Max(1.0, a[i, i]);

            return TrySolveLinearSystem(a, b, out step);
        }

        private static bool TrySolveLinearSystem(
            double[,] matrix,
            double[] rhs,
            out double[] solution)
        {
            solution = null;
            int n = rhs.Length;
            double[,] a = new double[n, n + 1];

            for (int r = 0; r < n; r++)
            {
                for (int c = 0; c < n; c++)
                    a[r, c] = matrix[r, c];
                a[r, n] = rhs[r];
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double pivotAbs = Math.Abs(a[pivot, col]);
                for (int row = col + 1; row < n; row++)
                {
                    double value = Math.Abs(a[row, col]);
                    if (value > pivotAbs)
                    {
                        pivot = row;
                        pivotAbs = value;
                    }
                }

                if (!Finite(pivotAbs) || pivotAbs < 1e-14)
                    return false;

                if (pivot != col)
                {
                    for (int c = col; c <= n; c++)
                    {
                        double temp = a[col, c];
                        a[col, c] = a[pivot, c];
                        a[pivot, c] = temp;
                    }
                }

                double divisor = a[col, col];
                for (int c = col; c <= n; c++)
                    a[col, c] /= divisor;

                for (int row = 0; row < n; row++)
                {
                    if (row == col)
                        continue;
                    double factor = a[row, col];
                    if (factor == 0.0)
                        continue;
                    for (int c = col; c <= n; c++)
                        a[row, c] -= factor * a[col, c];
                }
            }

            solution = new double[n];
            for (int i = 0; i < n; i++)
            {
                solution[i] = a[i, n];
                if (!Finite(solution[i]))
                {
                    solution = null;
                    return false;
                }
            }
            return true;
        }

        private static LambertParkingOrbitEjectionSolution CreateCandidate(
            LambertParkingOrbitEjectionSolution bootstrap,
            double[] q,
            double[] scale)
        {
            double burnUt =
                bootstrap.BurnUniversalTimeSeconds + q[0] * scale[0];
            double prograde =
                bootstrap.ProgradeDeltaVMetersPerSecond + q[1] * scale[1];
            double normal =
                bootstrap.NormalDeltaVMetersPerSecond + q[2] * scale[2];
            double radial =
                bootstrap.RadialDeltaVMetersPerSecond + q[3] * scale[3];
            double total = Math.Sqrt(
                prograde * prograde +
                normal * normal +
                radial * radial);

            return
                new LambertParkingOrbitEjectionSolution
                {
                    BurnUniversalTimeSeconds = burnUt,
                    WindowOffsetSeconds =
                        bootstrap.WindowOffsetSeconds +
                        (burnUt - bootstrap.BurnUniversalTimeSeconds),
                    ParkingRadiusMeters = bootstrap.ParkingRadiusMeters,
                    ParkingAltitudeMeters = bootstrap.ParkingAltitudeMeters,
                    ParkingSpeedMetersPerSecond = bootstrap.ParkingSpeedMetersPerSecond,
                    HyperbolicExcessSpeedMetersPerSecond =
                        bootstrap.HyperbolicExcessSpeedMetersPerSecond,
                    HyperbolicPeriapsisSpeedMetersPerSecond =
                        bootstrap.HyperbolicPeriapsisSpeedMetersPerSecond,
                    HyperbolicEccentricity = bootstrap.HyperbolicEccentricity,
                    AsymptoteAngleDegrees = bootstrap.AsymptoteAngleDegrees,
                    ProgradeDeltaVMetersPerSecond = prograde,
                    NormalDeltaVMetersPerSecond = normal,
                    RadialDeltaVMetersPerSecond = radial,
                    TotalDeltaVMetersPerSecond = total,
                    GeometryResidualDegrees = bootstrap.GeometryResidualDegrees
                };
        }

        private static double ComputeTargetInterfaceConstraintError(
            TargetSoiShootingAssessment assessment,
            double targetSoiRadius)
        {
            if (assessment == null || !FinitePositive(targetSoiRadius))
                return double.PositiveInfinity;

            if (assessment.PredictedEncounter)
                return 0.0;

            return Math.Max(0.0, assessment.MissDistanceMeters - targetSoiRadius);
        }

        private static double ResolveParkingPeriodSeconds(
            OrbitalElements parkingOrbit,
            double bodyMu)
        {
            if (parkingOrbit != null && FinitePositive(parkingOrbit.PeriodSeconds))
                return parkingOrbit.PeriodSeconds;

            if (parkingOrbit != null &&
                FinitePositive(parkingOrbit.SemiMajorAxisMeters) &&
                FinitePositive(bodyMu))
            {
                double a = parkingOrbit.SemiMajorAxisMeters;
                return 2.0 * Math.PI * Math.Sqrt(a * a * a / bodyMu);
            }

            return 3600.0;
        }

        private static bool Normalize(Vector3d vector, out Vector3d unit)
        {
            double magnitude = vector.Magnitude;
            if (!FinitePositive(magnitude))
            {
                unit = new Vector3d();
                return false;
            }
            unit = vector * (1.0 / magnitude);
            return unit.IsFinite;
        }

        private static double SumSquares(double[] values)
        {
            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
                sum += values[i] * values[i];
            return sum;
        }

        private static double Norm(double[] values)
        {
            return Math.Sqrt(SumSquares(values));
        }

        private static double[] Copy(double[] source)
        {
            double[] copy = new double[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool FinitePositive(double value)
        {
            return Finite(value) && value > 0.0;
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private sealed class ConstraintEvaluation
        {
            public LambertParkingOrbitEjectionSolution Candidate;
            public double[] Residuals;
            public double Score;
            public double SourceVelocityMismatchMetersPerSecond;
            public double MidpointVelocityMismatchMetersPerSecond;
            public double TargetPositionErrorMeters;
            public double TargetPeriapsisErrorMeters;
            public double TargetBPlaneMagnitudeErrorMeters;
            public double TargetBPlaneTErrorMeters;
            public double TargetBPlaneRErrorMeters;
            public bool TargetInbound;
            public TargetSoiShootingAssessment TargetAssessment;
            public bool SourceOutbound;
            public bool TargetClosingAtArrival;
            public double ArrivalUniversalTimeSeconds;
            public double SplitUniversalTimeSeconds;
            public Vector3d MidpointPosition;
        }
    }
}
