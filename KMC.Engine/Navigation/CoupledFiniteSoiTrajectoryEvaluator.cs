using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Shared trajectory assessment used only by the new coupled finite-SOI
    /// shadow solver. It evaluates one complete candidate from parking burn,
    /// through source SOI exit, parent-frame coast, target SOI entry, and
    /// target hyperbolic periapsis.
    /// </summary>
    internal static class CoupledFiniteSoiTrajectoryEvaluator
    {
        private const int TargetEntryBisectionIterations = 56;

        public static bool TryEvaluate(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution ejection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            CelestialBodyState destinationBody,
            double parentMu,
            out FiniteSoiDepartureAssessment sourceAssessment,
            out TargetSoiShootingAssessment targetAssessment,
            out bool sourceOutbound,
            out bool targetInbound,
            double arrivalUniversalTimeSecondsOverride = double.NaN)
        {
            sourceAssessment = null;
            targetAssessment = null;
            sourceOutbound = false;
            targetInbound = false;

            if (transfer == null ||
                ejection == null ||
                parkingOrbit == null ||
                originBody == null ||
                destinationBody == null ||
                originBody.Orbit == null ||
                destinationBody.Orbit == null ||
                !Positive(originBody.GravParameter) ||
                !Positive(originBody.SoiRadiusMeters) ||
                !Positive(destinationBody.SoiRadiusMeters) ||
                !Positive(parentMu))
                return false;

            if (!FiniteSoiDepartureEvaluator.TryEvaluate(
                    transfer,
                    ejection,
                    parkingOrbit,
                    originBody,
                    parentMu,
                    out sourceAssessment))
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
            if (!Normalize(parkingState.Velocity, out prograde) ||
                !Normalize(Vector3d.Cross(parkingState.Position, parkingState.Velocity), out normal) ||
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
            StateVector relativeExitState;
            if (!HyperbolicSoiExitSolver.TrySolve(
                    relativeBurnState,
                    originBody.SoiRadiusMeters,
                    out exitUt,
                    out relativeExitState))
                return false;

            sourceOutbound =
                Vector3d.Dot(
                    relativeExitState.Position,
                    relativeExitState.Velocity) > 0.0;

            double arrivalUt =
                Finite(arrivalUniversalTimeSecondsOverride)
                    ? arrivalUniversalTimeSecondsOverride
                    : transfer.ArrivalUniversalTimeSeconds;
            if (!Finite(arrivalUt) || arrivalUt <= exitUt + 1.0)
                return false;

            StateVector originExitState;
            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentMu,
                    exitUt,
                    out originExitState))
                return false;

            StateVector actualExit =
                new StateVector(
                    originExitState.Position + relativeExitState.Position,
                    originExitState.Velocity + relativeExitState.Velocity,
                    exitUt,
                    parentMu,
                    originBody.ParentName);

            StateVector actualArrival;
            StateVector destinationArrival;
            if (!StateVectorPropagator.TryPropagate(actualExit, arrivalUt, out actualArrival) ||
                !KeplerPropagator.TryPropagate(destinationBody.Orbit, parentMu, arrivalUt, out destinationArrival))
                return false;

            double missDistance =
                (actualArrival.Position - destinationArrival.Position).Magnitude;
            double relativeSpeed =
                (actualArrival.Velocity - destinationArrival.Velocity).Magnitude;

            if (!Finite(missDistance) || !Finite(relativeSpeed))
                return false;

            double velocityMismatch = double.PositiveInfinity;
            LambertSolution boundaryLambert;
            if (LambertSolver.TrySolve(
                    actualExit.Position,
                    destinationArrival.Position,
                    arrivalUt - exitUt,
                    parentMu,
                    transfer.Path,
                    out boundaryLambert))
            {
                velocityMismatch =
                    (actualExit.Velocity - boundaryLambert.DepartureVelocity).Magnitude;
            }
            if (!Finite(velocityMismatch))
                velocityMismatch = 1e300;

            bool predictedEncounter =
                missDistance <= destinationBody.SoiRadiusMeters;

            double desiredPeriapsis = ComputeDesiredPeriapsisRadius(destinationBody);
            double entryUt = double.NaN;
            double periapsisRadius = double.NaN;
            double periapsisAltitude = double.NaN;
            double periapsisError = double.PositiveInfinity;
            double bPlaneRadius = double.NaN;
            double desiredBPlaneRadius = double.NaN;
            double bPlaneError = double.PositiveInfinity;
            double bPlaneErrorFraction = double.PositiveInfinity;
            bool collision = false;
            Vector3d entryRelativePosition = new Vector3d();
            Vector3d entryRelativeVelocity = new Vector3d();

            if (predictedEncounter &&
                Positive(destinationBody.GravParameter) &&
                Positive(destinationBody.RadiusMeters) &&
                Positive(desiredPeriapsis))
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
                    entryRelativePosition =
                        spacecraftEntry.Position - destinationEntry.Position;
                    entryRelativeVelocity =
                        spacecraftEntry.Velocity - destinationEntry.Velocity;

                    targetInbound =
                        Vector3d.Dot(entryRelativePosition, entryRelativeVelocity) < 0.0;

                    if (TryCalculatePeriapsisRadius(
                            entryRelativePosition,
                            entryRelativeVelocity,
                            destinationBody.GravParameter,
                            out periapsisRadius))
                    {
                        periapsisAltitude =
                            periapsisRadius - destinationBody.RadiusMeters;
                        periapsisError =
                            Math.Abs(periapsisRadius - desiredPeriapsis);
                        collision =
                            periapsisRadius <= destinationBody.RadiusMeters;

                        if (TargetBPlanePlanner.TryCalculate(
                                entryRelativePosition,
                                entryRelativeVelocity,
                                destinationBody.GravParameter,
                                destinationBody.SoiRadiusMeters,
                                desiredPeriapsis,
                                out bPlaneRadius,
                                out desiredBPlaneRadius,
                                out bPlaneError))
                        {
                            bPlaneErrorFraction =
                                bPlaneError / destinationBody.SoiRadiusMeters;
                        }
                    }
                }
            }

            targetAssessment =
                new TargetSoiShootingAssessment
                {
                    ArrivalUniversalTimeSeconds = arrivalUt,
                    SourceSoiExitUniversalTimeSeconds = exitUt,
                    MissDistanceMeters = missDistance,
                    MissFractionOfTargetSoi =
                        missDistance / destinationBody.SoiRadiusMeters,
                    RelativeSpeedAtArrivalMetersPerSecond = relativeSpeed,
                    TargetSoiEntryUniversalTimeSeconds = entryUt,
                    TargetSoiEntryRelativePosition = entryRelativePosition,
                    TargetSoiEntryRelativeVelocity = entryRelativeVelocity,
                    DesiredPeriapsisRadiusMeters = desiredPeriapsis,
                    TargetPeriapsisRadiusMeters = periapsisRadius,
                    TargetPeriapsisAltitudeMeters = periapsisAltitude,
                    TargetPeriapsisErrorMeters = periapsisError,
                    TargetBPlaneRadiusMeters = bPlaneRadius,
                    DesiredBPlaneRadiusMeters = desiredBPlaneRadius,
                    TargetBPlaneErrorMeters = bPlaneError,
                    TargetBPlaneErrorFractionOfSoi = bPlaneErrorFraction,
                    PredictedCollision = collision,
                    SourceLambertVelocityMismatchMetersPerSecond = velocityMismatch,
                    PredictedEncounter = predictedEncounter
                };

            return true;
        }

        public static double ComputeDesiredPeriapsisRadius(CelestialBodyState destinationBody)
        {
            if (destinationBody == null || !Positive(destinationBody.SoiRadiusMeters))
                return double.NaN;

            double desired = destinationBody.SoiRadiusMeters * 0.01;
            if (Positive(destinationBody.RadiusMeters))
                desired = Math.Max(desired, destinationBody.RadiusMeters * 2.0);

            double maximum = destinationBody.SoiRadiusMeters * 0.25;
            desired = Math.Min(desired, maximum);

            if (Positive(destinationBody.RadiusMeters) &&
                desired <= destinationBody.RadiusMeters)
            {
                double fallback = destinationBody.RadiusMeters * 1.10;
                if (fallback >= destinationBody.SoiRadiusMeters)
                    return double.NaN;
                desired = Math.Min(fallback, maximum);
            }

            return Positive(desired) ? desired : double.NaN;
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

            if (highDistance > destinationBody.SoiRadiusMeters)
                return false;

            if (lowDistance <= destinationBody.SoiRadiusMeters)
            {
                entryUt = lowUt;
                return true;
            }

            double low = lowUt;
            double high = highUt;
            for (int i = 0; i < TargetEntryBisectionIterations; i++)
            {
                double mid = 0.5 * (low + high);
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

                if (midDistance > destinationBody.SoiRadiusMeters)
                    low = mid;
                else
                    high = mid;
            }

            entryUt = high;
            double finalDistance;
            return TryRelativeDistance(
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

            if (!StateVectorPropagator.TryPropagate(spacecraftExit, ut, out spacecraft) ||
                !KeplerPropagator.TryPropagate(destinationBody.Orbit, parentMu, ut, out destination))
                return false;

            distance = (spacecraft.Position - destination.Position).Magnitude;
            return Finite(distance);
        }

        private static bool TryCalculatePeriapsisRadius(
            Vector3d position,
            Vector3d velocity,
            double mu,
            out double periapsisRadius)
        {
            periapsisRadius = double.NaN;
            double r = position.Magnitude;
            if (!Positive(r) || !Positive(mu) || !position.IsFinite || !velocity.IsFinite)
                return false;

            Vector3d hVector = Vector3d.Cross(position, velocity);
            double h = hVector.Magnitude;
            if (!Positive(h))
                return false;

            double v2 = Vector3d.Dot(velocity, velocity);
            double rv = Vector3d.Dot(position, velocity);
            Vector3d eccentricityVector =
                position * ((v2 - mu / r) / mu) -
                velocity * (rv / mu);
            double eccentricity = eccentricityVector.Magnitude;
            if (!Finite(eccentricity))
                return false;

            double p = h * h / mu;
            periapsisRadius = p / (1.0 + eccentricity);
            return Positive(periapsisRadius);
        }

        private static bool Normalize(Vector3d value, out Vector3d unit)
        {
            unit = new Vector3d();
            double magnitude = value.Magnitude;
            if (!Positive(magnitude))
                return false;
            unit = value * (1.0 / magnitude);
            return unit.IsFinite;
        }

        private static bool Positive(double value)
        {
            return Finite(value) && value > 0.0;
        }

        private static bool Finite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
