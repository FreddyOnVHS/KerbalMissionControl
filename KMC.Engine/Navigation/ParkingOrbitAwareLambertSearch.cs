using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Bounded Lambert search ranked by the actual local parking-orbit ejection
    /// impulse rather than departure-vinf + arrival-vinf.
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

            ParkingOrbitAwareTransferSolution best = null;

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
                            ref best);

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
                            ref best);
                }
            }

            solution = best;
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
            ref ParkingOrbitAwareTransferSolution best)
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

            ParkingOrbitAwareTransferSolution candidate =
                new ParkingOrbitAwareTransferSolution(
                    transfer,
                    ejection);

            if (!Vector3d.Finite(candidate.EjectionScoreMetersPerSecond) ||
                candidate.EjectionScoreMetersPerSecond <= 0.0)
                return;

            if (IsBetter(candidate, best))
                best = candidate;
        }

        private static bool IsBetter(
            ParkingOrbitAwareTransferSolution candidate,
            ParkingOrbitAwareTransferSolution best)
        {
            if (best == null)
                return true;

            double scale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidate.EjectionScoreMetersPerSecond,
                        best.EjectionScoreMetersPerSecond));

            double tolerance =
                ScoreTieRelativeTolerance * scale;

            double scoreDifference =
                candidate.EjectionScoreMetersPerSecond -
                best.EjectionScoreMetersPerSecond;

            if (scoreDifference < -tolerance)
                return true;

            if (Math.Abs(scoreDifference) > tolerance)
                return false;

            double arrivalDifference =
                candidate.ArrivalExcessSpeedMetersPerSecond -
                best.ArrivalExcessSpeedMetersPerSecond;

            if (arrivalDifference < -tolerance)
                return true;

            if (Math.Abs(arrivalDifference) > tolerance)
                return false;

            if (candidate.Transfer.DepartureUniversalTimeSeconds <
                best.Transfer.DepartureUniversalTimeSeconds)
                return true;

            if (candidate.Transfer.DepartureUniversalTimeSeconds >
                best.Transfer.DepartureUniversalTimeSeconds)
                return false;

            if (candidate.Transfer.TimeOfFlightSeconds <
                best.Transfer.TimeOfFlightSeconds)
                return true;

            if (candidate.Transfer.TimeOfFlightSeconds >
                best.Transfer.TimeOfFlightSeconds)
                return false;

            return candidate.Transfer.Path == LambertTransferPath.ShortWay &&
                   best.Transfer.Path == LambertTransferPath.LongWay;
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
