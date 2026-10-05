using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Deterministic bounded grid search over departure UT and time of flight.
    ///
    /// Each sample:
    /// 1. propagates origin and destination body states with KeplerPropagator,
    /// 2. solves the selected zero-revolution Lambert path(s),
    /// 3. compares Lambert endpoint velocities with body orbital velocities,
    /// 4. ranks by departure v-infinity + arrival v-infinity magnitude.
    ///
    /// This layer produces a parent-frame transfer candidate only. It does not
    /// calculate parking-orbit ejection, capture, SOI transitions or KSP nodes.
    /// </summary>
    public static class LambertTransferSearch
    {
        private const int MaximumSamplesPerAxis = 256;
        private const double ScoreTieRelativeTolerance = 1e-10;

        public static bool TryFindBest(
            TransferSearchRequest request,
            out TransferSearchSolution solution)
        {
            solution = null;
            if (!IsValidRequest(request))
                return false;

            TransferSearchSolution best = null;

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
                    {
                        Consider(
                            request,
                            departureUt,
                            arrivalUt,
                            originState,
                            destinationState,
                            LambertTransferPath.ShortWay,
                            ref best);
                    }

                    if (request.SearchLongWay)
                    {
                        Consider(
                            request,
                            departureUt,
                            arrivalUt,
                            originState,
                            destinationState,
                            LambertTransferPath.LongWay,
                            ref best);
                    }
                }
            }

            solution = best;
            return solution != null;
        }

        private static void Consider(
            TransferSearchRequest request,
            double departureUt,
            double arrivalUt,
            StateVector originState,
            StateVector destinationState,
            LambertTransferPath path,
            ref TransferSearchSolution best)
        {
            double timeOfFlight = arrivalUt - departureUt;

            LambertSolution lambert;
            if (!LambertSolver.TrySolve(
                    originState.Position,
                    destinationState.Position,
                    timeOfFlight,
                    request.ParentGravParameter,
                    path,
                    out lambert))
                return;

            Vector3d departureExcess =
                lambert.DepartureVelocity -
                originState.Velocity;

            Vector3d arrivalExcess =
                lambert.ArrivalVelocity -
                destinationState.Velocity;

            if (!departureExcess.IsFinite ||
                !arrivalExcess.IsFinite)
                return;

            TransferSearchSolution candidate =
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

            if (!Vector3d.Finite(
                    candidate.CombinedExcessSpeedMetersPerSecond))
                return;

            if (IsBetter(candidate, best))
                best = candidate;
        }

        private static bool IsBetter(
            TransferSearchSolution candidate,
            TransferSearchSolution best)
        {
            if (best == null)
                return true;

            double scale =
                Math.Max(
                    1.0,
                    Math.Max(
                        candidate.CombinedExcessSpeedMetersPerSecond,
                        best.CombinedExcessSpeedMetersPerSecond));

            double tolerance =
                ScoreTieRelativeTolerance * scale;

            double difference =
                candidate.CombinedExcessSpeedMetersPerSecond -
                best.CombinedExcessSpeedMetersPerSecond;

            if (difference < -tolerance)
                return true;

            if (Math.Abs(difference) > tolerance)
                return false;

            if (candidate.DepartureUniversalTimeSeconds <
                best.DepartureUniversalTimeSeconds)
                return true;

            if (candidate.DepartureUniversalTimeSeconds >
                best.DepartureUniversalTimeSeconds)
                return false;

            if (candidate.TimeOfFlightSeconds <
                best.TimeOfFlightSeconds)
                return true;

            if (candidate.TimeOfFlightSeconds >
                best.TimeOfFlightSeconds)
                return false;

            return
                candidate.Path == LambertTransferPath.ShortWay &&
                best.Path == LambertTransferPath.LongWay;
        }

        private static bool IsValidRequest(
            TransferSearchRequest request)
        {
            if (request == null ||
                request.OriginBody == null ||
                request.DestinationBody == null ||
                request.OriginBody.Orbit == null ||
                request.DestinationBody.Orbit == null)
                return false;

            if (!request.OriginBody.Orbit.HasFiniteGeometry ||
                !request.DestinationBody.Orbit.HasFiniteGeometry)
                return false;

            if (string.IsNullOrWhiteSpace(
                    request.OriginBody.ParentName) ||
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
                    request.DestinationBody.ParentName))
                return false;

            if (!Vector3d.Finite(
                    request.ParentGravParameter) ||
                request.ParentGravParameter <= 0.0)
                return false;

            if (!Vector3d.Finite(
                    request.EarliestDepartureUniversalTimeSeconds) ||
                !Vector3d.Finite(
                    request.LatestDepartureUniversalTimeSeconds) ||
                request.LatestDepartureUniversalTimeSeconds <
                    request.EarliestDepartureUniversalTimeSeconds)
                return false;

            if (!Vector3d.Finite(
                    request.MinimumTimeOfFlightSeconds) ||
                !Vector3d.Finite(
                    request.MaximumTimeOfFlightSeconds) ||
                request.MinimumTimeOfFlightSeconds <= 0.0 ||
                request.MaximumTimeOfFlightSeconds <
                    request.MinimumTimeOfFlightSeconds)
                return false;

            if (request.DepartureSamples < 1 ||
                request.DepartureSamples > MaximumSamplesPerAxis ||
                request.TimeOfFlightSamples < 1 ||
                request.TimeOfFlightSamples > MaximumSamplesPerAxis)
                return false;

            if (!request.SearchShortWay &&
                !request.SearchLongWay)
                return false;

            return true;
        }

        private static double Sample(
            double minimum,
            double maximum,
            int count,
            int index)
        {
            if (count <= 1)
                return minimum;

            return
                minimum +
                ((maximum - minimum) *
                index / (count - 1.0));
        }

        private static bool MatchesOptionalFrame(
            string referenceBodyName,
            string expectedParentName)
        {
            return
                string.IsNullOrWhiteSpace(referenceBodyName) ||
                string.Equals(
                    referenceBodyName,
                    expectedParentName,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
