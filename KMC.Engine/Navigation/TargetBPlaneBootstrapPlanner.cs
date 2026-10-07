using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// MechJeb-style target-arrival bootstrap.
    ///
    /// A desired target periapsis is converted into a B-plane impact
    /// parameter. Candidate SOI-entry points are then placed around the
    /// incoming v-infinity direction and used as Lambert terminal points.
    ///
    /// KMC samples B-plane azimuth because its downstream optimizer is a
    /// bounded coordinate search rather than MechJeb's full SQP solver.
    /// The lowest-DV valid parking-orbit departure becomes the bootstrap.
    /// </summary>
    public static class TargetBPlaneBootstrapPlanner
    {
        private const int AzimuthSamples = 12;
        private const double DirectionTolerance = 1e-12;

        public static bool TryCreateEjection(
            TransferSearchSolution transfer,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            double desiredPeriapsisRadiusMeters,
            out LambertParkingOrbitEjectionSolution ejection,
            out double desiredBPlaneRadiusMeters)
        {
            ejection = null;
            desiredBPlaneRadiusMeters = double.NaN;

            if (transfer == null ||
                parkingOrbit == null ||
                originBody == null ||
                destinationBody == null ||
                originBody.Orbit == null ||
                destinationBody.Orbit == null ||
                !FinitePositive(parentGravParameter) ||
                !FinitePositive(originBody.RadiusMeters) ||
                !FinitePositive(destinationBody.GravParameter) ||
                !FinitePositive(destinationBody.SoiRadiusMeters) ||
                !FinitePositive(desiredPeriapsisRadiusMeters) ||
                desiredPeriapsisRadiusMeters >=
                    destinationBody.SoiRadiusMeters ||
                !transfer.ArrivalExcessVelocity.IsFinite)
                return false;

            double arrivalVinf =
                transfer.ArrivalExcessVelocity.Magnitude;

            if (!FinitePositive(arrivalVinf))
                return false;

            desiredBPlaneRadiusMeters =
                desiredPeriapsisRadiusMeters *
                Math.Sqrt(
                    1.0 +
                    2.0 * destinationBody.GravParameter /
                    (desiredPeriapsisRadiusMeters *
                     arrivalVinf *
                     arrivalVinf));

            if (!FinitePositive(
                    desiredBPlaneRadiusMeters))
                return false;

            desiredBPlaneRadiusMeters =
                Math.Min(
                    desiredBPlaneRadiusMeters,
                    destinationBody.SoiRadiusMeters *
                        0.99);

            StateVector originDeparture;
            StateVector destinationArrival;

            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentGravParameter,
                    transfer.DepartureUniversalTimeSeconds,
                    out originDeparture) ||
                !KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentGravParameter,
                    transfer.ArrivalUniversalTimeSeconds,
                    out destinationArrival))
                return false;

            Vector3d incomingDirection;

            if (!TryNormalize(
                    transfer.ArrivalExcessVelocity,
                    out incomingDirection))
                return false;

            Vector3d basis1;
            Vector3d basis2;

            if (!TryBPlaneBasis(
                    incomingDirection,
                    out basis1,
                    out basis2))
                return false;

            double alongMagnitudeSquared =
                destinationBody.SoiRadiusMeters *
                    destinationBody.SoiRadiusMeters -
                desiredBPlaneRadiusMeters *
                    desiredBPlaneRadiusMeters;

            if (!Vector3d.Finite(alongMagnitudeSquared) ||
                alongMagnitudeSquared <= 0.0)
                return false;

            double alongMagnitude =
                Math.Sqrt(
                    alongMagnitudeSquared);

            LambertParkingOrbitEjectionSolution best =
                null;

            for (int i = 0;
                i < AzimuthSamples;
                i++)
            {
                double angle =
                    2.0 * Math.PI *
                    i /
                    AzimuthSamples;

                Vector3d bDirection =
                    basis1 * Math.Cos(angle) +
                    basis2 * Math.Sin(angle);

                Vector3d entryOffset =
                    bDirection *
                        desiredBPlaneRadiusMeters -
                    incomingDirection *
                        alongMagnitude;

                Vector3d targetPoint =
                    destinationArrival.Position +
                    entryOffset;

                LambertSolution lambert;

                if (!LambertSolver.TrySolve(
                        originDeparture.Position,
                        targetPoint,
                        transfer.TimeOfFlightSeconds,
                        parentGravParameter,
                        transfer.Path,
                        out lambert))
                    continue;

                Vector3d departureExcess =
                    lambert.DepartureVelocity -
                    originDeparture.Velocity;

                LambertParkingOrbitEjectionSolution trial;

                if (!LambertParkingOrbitEjectionPlanner.TryCalculate(
                        parkingOrbit,
                        originBody.RadiusMeters,
                        originBody,
                        departureExcess,
                        transfer.DepartureUniversalTimeSeconds,
                        out trial))
                    continue;

                if (trial == null ||
                    !FinitePositive(
                        trial.TotalDeltaVMetersPerSecond))
                    continue;

                if (best == null ||
                    trial.TotalDeltaVMetersPerSecond <
                    best.TotalDeltaVMetersPerSecond)
                    best = trial;
            }

            ejection = best;
            return ejection != null;
        }

        public static bool TryCreateEjectionAtEntry(
            TransferSearchSolution transfer,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentGravParameter,
            Vector3d incomingTargetRelativeVelocity,
            double desiredPeriapsisRadiusMeters,
            double arrivalUniversalTimeSeconds,
            double azimuthRadians,
            out LambertParkingOrbitEjectionSolution ejection,
            out double desiredBPlaneRadiusMeters)
        {
            ejection = null;
            desiredBPlaneRadiusMeters = double.NaN;

            if (transfer == null ||
                parkingOrbit == null ||
                originBody == null ||
                destinationBody == null ||
                originBody.Orbit == null ||
                destinationBody.Orbit == null ||
                !FinitePositive(parentGravParameter) ||
                !FinitePositive(originBody.RadiusMeters) ||
                !FinitePositive(destinationBody.GravParameter) ||
                !FinitePositive(destinationBody.SoiRadiusMeters) ||
                !FinitePositive(desiredPeriapsisRadiusMeters) ||
                !Vector3d.Finite(arrivalUniversalTimeSeconds) ||
                arrivalUniversalTimeSeconds <=
                    transfer.DepartureUniversalTimeSeconds)
                return false;

            Vector3d entryOffset;

            if (!TryBuildEntryOffset(
                    incomingTargetRelativeVelocity,
                    destinationBody.GravParameter,
                    destinationBody.SoiRadiusMeters,
                    desiredPeriapsisRadiusMeters,
                    azimuthRadians,
                    out entryOffset,
                    out desiredBPlaneRadiusMeters))
                return false;

            StateVector originDeparture;
            StateVector destinationArrival;

            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentGravParameter,
                    transfer.DepartureUniversalTimeSeconds,
                    out originDeparture) ||
                !KeplerPropagator.TryPropagate(
                    destinationBody.Orbit,
                    parentGravParameter,
                    arrivalUniversalTimeSeconds,
                    out destinationArrival))
                return false;

            double timeOfFlight =
                arrivalUniversalTimeSeconds -
                transfer.DepartureUniversalTimeSeconds;

            Vector3d targetPoint =
                destinationArrival.Position +
                entryOffset;

            LambertSolution lambert;

            if (!LambertSolver.TrySolve(
                    originDeparture.Position,
                    targetPoint,
                    timeOfFlight,
                    parentGravParameter,
                    transfer.Path,
                    out lambert))
                return false;

            Vector3d departureExcess =
                lambert.DepartureVelocity -
                originDeparture.Velocity;

            return
                LambertParkingOrbitEjectionPlanner.TryCalculate(
                    parkingOrbit,
                    originBody.RadiusMeters,
                    originBody,
                    departureExcess,
                    transfer.DepartureUniversalTimeSeconds,
                    out ejection) &&
                ejection != null &&
                FinitePositive(
                    ejection.TotalDeltaVMetersPerSecond);
        }

        public static bool TryBuildEntryOffset(
            Vector3d incomingVInfinity,
            double targetGravParameter,
            double targetSoiRadiusMeters,
            double desiredPeriapsisRadiusMeters,
            double azimuthRadians,
            out Vector3d entryOffset,
            out double desiredBPlaneRadiusMeters)
        {
            entryOffset = new Vector3d();
            desiredBPlaneRadiusMeters = double.NaN;

            double vinf =
                incomingVInfinity.Magnitude;

            if (!incomingVInfinity.IsFinite ||
                !FinitePositive(vinf) ||
                !FinitePositive(targetGravParameter) ||
                !FinitePositive(targetSoiRadiusMeters) ||
                !FinitePositive(desiredPeriapsisRadiusMeters) ||
                desiredPeriapsisRadiusMeters >=
                    targetSoiRadiusMeters ||
                !Vector3d.Finite(azimuthRadians))
                return false;

            desiredBPlaneRadiusMeters =
                desiredPeriapsisRadiusMeters *
                Math.Sqrt(
                    1.0 +
                    2.0 * targetGravParameter /
                    (desiredPeriapsisRadiusMeters *
                     vinf *
                     vinf));

            desiredBPlaneRadiusMeters =
                Math.Min(
                    desiredBPlaneRadiusMeters,
                    targetSoiRadiusMeters *
                        0.99);

            Vector3d incomingDirection;

            if (!TryNormalize(
                    incomingVInfinity,
                    out incomingDirection))
                return false;

            Vector3d basis1;
            Vector3d basis2;

            if (!TryBPlaneBasis(
                    incomingDirection,
                    out basis1,
                    out basis2))
                return false;

            double alongSquared =
                targetSoiRadiusMeters *
                    targetSoiRadiusMeters -
                desiredBPlaneRadiusMeters *
                    desiredBPlaneRadiusMeters;

            if (!Vector3d.Finite(alongSquared) ||
                alongSquared <= 0.0)
                return false;

            Vector3d bDirection =
                basis1 * Math.Cos(azimuthRadians) +
                basis2 * Math.Sin(azimuthRadians);

            entryOffset =
                bDirection *
                    desiredBPlaneRadiusMeters -
                incomingDirection *
                    Math.Sqrt(alongSquared);

            return
                entryOffset.IsFinite;
        }

        private static bool TryBPlaneBasis(
            Vector3d incomingDirection,
            out Vector3d first,
            out Vector3d second)
        {
            first = new Vector3d();
            second = new Vector3d();

            Vector3d reference =
                Math.Abs(incomingDirection.Z) < 0.9
                    ? new Vector3d(0.0, 0.0, 1.0)
                    : new Vector3d(1.0, 0.0, 0.0);

            if (!TryNormalize(
                    Vector3d.Cross(
                        incomingDirection,
                        reference),
                    out first))
                return false;

            if (!TryNormalize(
                    Vector3d.Cross(
                        incomingDirection,
                        first),
                    out second))
                return false;

            return true;
        }

        private static bool TryNormalize(
            Vector3d value,
            out Vector3d normalized)
        {
            normalized = new Vector3d();

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

        private static bool FinitePositive(
            double value)
        {
            return
                Vector3d.Finite(value) &&
                value > DirectionTolerance;
        }
    }
}
