using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Bounded Lambert search using MechJeb-style optimization semantics:
    /// finite-SOI handoff quality is a feasibility constraint; among feasible
    /// candidates the actual local parking-orbit departure impulse is minimized.
    /// </summary>
    public static class ParkingOrbitAwareLambertSearch
    {
        private const int MaximumSamplesPerAxis = 256;
        private const double ScoreTieRelativeTolerance = 1e-10;

        public static bool TryFindBest(
            TransferSearchRequest request,
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            out ParkingOrbitAwareTransferSolution solution)
        {
            solution = null;
            if (!IsValidRequest(request, parkingOrbit, referenceBodyRadiusMeters))
                return false;

            ParkingOrbitAwareTransferSolution bestFeasible = null;
            ParkingOrbitAwareTransferSolution bestBootstrap = null;

            for (int departureIndex = 0;
                departureIndex < request.DepartureSamples;
                departureIndex++)
            {
                double departureUt =
                    Sample(
                        request.EarliestDepartureUniversalTimeSeconds,
                        request.LatestDepartureUniversalTimeSeconds,
                        request.DepartureSamples,
                        departureIndex);

                StateVector originState;
                if (!KeplerPropagator.TryPropagate(
                        request.OriginBody.Orbit,
                        request.ParentGravParameter,
                        departureUt,
                        out originState))
                    continue;

                for (int flightIndex = 0;
                    flightIndex < request.TimeOfFlightSamples;
                    flightIndex++)
                {
                    double timeOfFlight =
                        Sample(
                            request.MinimumTimeOfFlightSeconds,
                            request.MaximumTimeOfFlightSeconds,
                            request.TimeOfFlightSamples,
                            flightIndex);

                    double arrivalUt = departureUt + timeOfFlight;
                    if (!Vector3d.Finite(arrivalUt))
                        continue;

                    StateVector destinationState;
                    if (!KeplerPropagator.TryPropagate(
                            request.DestinationBody.Orbit,
                            request.ParentGravParameter,
                            arrivalUt,
                            out destinationState))
                        continue;

                    if (request.SearchShortWay)
                        Consider(
                            request,
                            parkingOrbit,
                            referenceBodyRadiusMeters,
                            departureUt,
                            arrivalUt,
                            originState,
                            destinationState,
                            LambertTransferPath.ShortWay,
                            ref bestFeasible,
                            ref bestBootstrap);

                    if (request.SearchLongWay)
                        Consider(
                            request,
                            parkingOrbit,
                            referenceBodyRadiusMeters,
                            departureUt,
                            arrivalUt,
                            originState,
                            destinationState,
                            LambertTransferPath.LongWay,
                            ref bestFeasible,
                            ref bestBootstrap);
                }
            }

            solution = bestFeasible ?? bestBootstrap;
            return solution != null;
        }

        private static void Consider(
            TransferSearchRequest request,
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters,
            double departureUt,
            double arrivalUt,
            StateVector originState,
            StateVector destinationState,
            LambertTransferPath path,
            ref ParkingOrbitAwareTransferSolution bestFeasible,
            ref ParkingOrbitAwareTransferSolution bestBootstrap)
        {
            LambertSolution lambert;
            if (!LambertSolver.TrySolve(
                    originState.Position,
                    destinationState.Position,
                    arrivalUt - departureUt,
                    request.ParentGravParameter,
                    path,
                    out lambert))
                return;

            Vector3d departureExcess =
                lambert.DepartureVelocity - originState.Velocity;

            Vector3d arrivalExcess =
                lambert.ArrivalVelocity - destinationState.Velocity;

            if (!departureExcess.IsFinite || !arrivalExcess.IsFinite)
                return;

            TransferSearchSolution transfer =
                new TransferSearchSolution(
                    request.OriginBody.Name,
                    request.DestinationBody.Name,
                    request.OriginBody.ParentName,
                    departureUt,
                    arrivalUt,
                    originState,
                    destinationState,
                    lambert,
                    departureExcess,
                    arrivalExcess);

            LambertParkingOrbitEjectionSolution ejection;
            if (!LambertParkingOrbitEjectionPlanner.TryCalculate(
                    parkingOrbit,
                    referenceBodyRadiusMeters,
                    request.OriginBody,
                    departureExcess,
                    departureUt,
                    out ejection))
                return;

            FiniteSoiDepartureAssessment finiteSoiAssessment;

            if (!FiniteSoiDepartureEvaluator.TryEvaluate(
                    transfer,
                    ejection,
                    parkingOrbit,
                    request.OriginBody,
                    request.ParentGravParameter,
                    out finiteSoiAssessment))
                return;

            ParkingOrbitAwareTransferSolution candidate =
                new ParkingOrbitAwareTransferSolution(
                    transfer,
                    ejection,
                    finiteSoiAssessment);

            if (!Vector3d.Finite(candidate.EjectionScoreMetersPerSecond) ||
                candidate.EjectionScoreMetersPerSecond <= 0.0 ||
                candidate.FiniteSoiAssessment == null ||
                !Vector3d.Finite(
                    candidate.FiniteSoiAssessment.NormalizedStateError))
                return;

            if (IsLowerDepartureDv(
                    candidate,
                    bestBootstrap))
                bestBootstrap = candidate;

            if (FiniteSoiCandidateSelectionPolicy.IsFeasible(
                    candidate.FiniteSoiAssessment) &&
                IsBetterFeasible(
                    candidate,
                    bestFeasible))
                bestFeasible = candidate;
        }

        private static bool IsBetterFeasible(
            ParkingOrbitAwareTransferSolution candidate,
            ParkingOrbitAwareTransferSolution best)
        {
            if (candidate == null ||
                !FiniteSoiCandidateSelectionPolicy.IsFeasible(
                    candidate.FiniteSoiAssessment))
                return false;

            if (best == null)
                return true;

            if (IsLowerDepartureDv(candidate, best))
                return true;

            double dvScale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidate.EjectionScoreMetersPerSecond,
                        best.EjectionScoreMetersPerSecond));

            double dvTolerance =
                ScoreTieRelativeTolerance *
                dvScale;

            if (Math.Abs(
                    candidate.EjectionScoreMetersPerSecond -
                    best.EjectionScoreMetersPerSecond) >
                dvTolerance)
                return false;

            double stateDifference =
                candidate.FiniteSoiAssessment.NormalizedStateError -
                best.FiniteSoiAssessment.NormalizedStateError;

            double stateScale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidate.FiniteSoiAssessment.NormalizedStateError,
                        best.FiniteSoiAssessment.NormalizedStateError));

            double stateTolerance =
                ScoreTieRelativeTolerance *
                stateScale;

            if (stateDifference < -stateTolerance)
                return true;

            if (Math.Abs(stateDifference) >
                stateTolerance)
                return false;

            return PreferDeterministicTieBreak(
                candidate,
                best,
                dvTolerance);
        }

        private static bool IsLowerDepartureDv(
            ParkingOrbitAwareTransferSolution candidate,
            ParkingOrbitAwareTransferSolution best)
        {
            if (candidate == null)
                return false;

            if (best == null)
                return true;

            double scale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidate.EjectionScoreMetersPerSecond,
                        best.EjectionScoreMetersPerSecond));

            double tolerance =
                ScoreTieRelativeTolerance *
                scale;

            double difference =
                candidate.EjectionScoreMetersPerSecond -
                best.EjectionScoreMetersPerSecond;

            if (difference < -tolerance)
                return true;

            if (Math.Abs(difference) >
                tolerance)
                return false;

            double stateDifference =
                candidate.FiniteSoiAssessment.NormalizedStateError -
                best.FiniteSoiAssessment.NormalizedStateError;

            if (stateDifference < -ScoreTieRelativeTolerance)
                return true;

            if (Math.Abs(stateDifference) >
                ScoreTieRelativeTolerance)
                return false;

            return PreferDeterministicTieBreak(
                candidate,
                best,
                tolerance);
        }

        private static bool PreferDeterministicTieBreak(
            ParkingOrbitAwareTransferSolution candidate,
            ParkingOrbitAwareTransferSolution best,
            double velocityTolerance)
        {
            double arrivalDifference =
                candidate.ArrivalExcessSpeedMetersPerSecond -
                best.ArrivalExcessSpeedMetersPerSecond;

            if (arrivalDifference < -velocityTolerance)
                return true;

            if (Math.Abs(arrivalDifference) >
                velocityTolerance)
                return false;

            if (candidate.Transfer.DepartureUniversalTimeSeconds <
                best.Transfer.DepartureUniversalTimeSeconds)
                return true;

            if (candidate.Transfer.DepartureUniversalTimeSeconds >
                best.Transfer.DepartureUniversalTimeSeconds)
                return false;

            return
                candidate.Transfer.Path ==
                    LambertTransferPath.ShortWay &&
                best.Transfer.Path ==
                    LambertTransferPath.LongWay;
        }

        private static bool IsValidRequest(
            TransferSearchRequest request,
            OrbitalElements parkingOrbit,
            double referenceBodyRadiusMeters)
        {
            if (request == null ||
                request.OriginBody == null ||
                request.DestinationBody == null ||
                request.OriginBody.Orbit == null ||
                request.DestinationBody.Orbit == null ||
                parkingOrbit == null)
                return false;

            if (!request.OriginBody.Orbit.HasFiniteGeometry ||
                !request.DestinationBody.Orbit.HasFiniteGeometry ||
                !parkingOrbit.HasFiniteGeometry)
                return false;

            if (string.IsNullOrWhiteSpace(request.OriginBody.ParentName) ||
                !string.Equals(
                    request.OriginBody.ParentName,
                    request.DestinationBody.ParentName,
                    StringComparison.OrdinalIgnoreCase))
                return false;

            if (!MatchesOptionalFrame(
                    request.OriginBody.Orbit.ReferenceBodyName,
                    request.OriginBody.ParentName) ||
                !MatchesOptionalFrame(
                    request.DestinationBody.Orbit.ReferenceBodyName,
                    request.DestinationBody.ParentName) ||
                !MatchesOptionalFrame(
                    parkingOrbit.ReferenceBodyName,
                    request.OriginBody.Name))
                return false;

            if (!Vector3d.Finite(request.OriginBody.SoiRadiusMeters) ||
                request.OriginBody.SoiRadiusMeters <= 0.0)
                return false;

            if (!Vector3d.Finite(request.ParentGravParameter) ||
                request.ParentGravParameter <= 0.0 ||
                !Vector3d.Finite(referenceBodyRadiusMeters) ||
                referenceBodyRadiusMeters <= 0.0)
                return false;

            if (!Vector3d.Finite(request.EarliestDepartureUniversalTimeSeconds) ||
                !Vector3d.Finite(request.LatestDepartureUniversalTimeSeconds) ||
                request.LatestDepartureUniversalTimeSeconds <
                    request.EarliestDepartureUniversalTimeSeconds)
                return false;

            if (!Vector3d.Finite(request.MinimumTimeOfFlightSeconds) ||
                !Vector3d.Finite(request.MaximumTimeOfFlightSeconds) ||
                request.MinimumTimeOfFlightSeconds <= 0.0 ||
                request.MaximumTimeOfFlightSeconds <
                    request.MinimumTimeOfFlightSeconds)
                return false;

            if (request.DepartureSamples < 1 ||
                request.DepartureSamples > MaximumSamplesPerAxis ||
                request.TimeOfFlightSamples < 1 ||
                request.TimeOfFlightSamples > MaximumSamplesPerAxis)
                return false;

            return request.SearchShortWay || request.SearchLongWay;
        }

        private static double Sample(
            double minimum,
            double maximum,
            int count,
            int index)
        {
            if (count <= 1)
                return minimum;

            return minimum +
                ((maximum - minimum) * index / (count - 1.0));
        }

        private static bool MatchesOptionalFrame(
            string referenceBodyName,
            string expectedName)
        {
            return string.IsNullOrWhiteSpace(referenceBodyName) ||
                string.Equals(
                    referenceBodyName,
                    expectedName,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
