using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Evaluates the actual finite-SOI handoff produced by an ejection against
    /// the desired Lambert parent-frame trajectory at the same UT.
    /// </summary>
    public static class FiniteSoiDepartureEvaluator
    {
        private const int ExitBracketExpansions = 80;
        private const int ExitBisectionIterations = 72;

        public static bool TryEvaluate(
            TransferSearchSolution transfer,
            LambertParkingOrbitEjectionSolution ejection,
            OrbitalElements parkingOrbit,
            CelestialBodyState originBody,
            double parentGravParameter,
            out FiniteSoiDepartureAssessment assessment)
        {
            assessment = null;

            if (transfer == null ||
                ejection == null ||
                parkingOrbit == null ||
                originBody == null ||
                originBody.Orbit == null ||
                !FinitePositive(originBody.GravParameter) ||
                !FinitePositive(originBody.SoiRadiusMeters) ||
                !FinitePositive(parentGravParameter))
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
                    ejection.HyperbolicExcessSpeedMetersPerSecond,
                    out exitUt,
                    out relativeExitState))
                return false;

            StateVector originExitState;

            if (!KeplerPropagator.TryPropagate(
                    originBody.Orbit,
                    parentGravParameter,
                    exitUt,
                    out originExitState))
                return false;

            StateVector lambertInitial =
                new StateVector(
                    transfer.OriginDepartureState.Position,
                    transfer.LambertSolution.DepartureVelocity,
                    transfer.DepartureUniversalTimeSeconds,
                    parentGravParameter,
                    originBody.ParentName);

            StateVector desiredTransferState;

            if (!StateVectorPropagator.TryPropagate(
                    lambertInitial,
                    exitUt,
                    out desiredTransferState))
                return false;

            Vector3d actualPosition =
                originExitState.Position +
                relativeExitState.Position;

            Vector3d actualVelocity =
                originExitState.Velocity +
                relativeExitState.Velocity;

            double positionError =
                (actualPosition -
                 desiredTransferState.Position).Magnitude;

            double velocityError =
                (actualVelocity -
                 desiredTransferState.Velocity).Magnitude;

            if (!Vector3d.Finite(positionError) ||
                !Vector3d.Finite(velocityError))
                return false;

            double positionFraction =
                positionError /
                originBody.SoiRadiusMeters;

            double velocityFraction =
                velocityError /
                Math.Max(
                    1.0,
                    transfer.DepartureExcessSpeedMetersPerSecond);

            double normalized =
                Math.Sqrt(
                    positionFraction *
                    positionFraction +
                    velocityFraction *
                    velocityFraction);

            if (!Vector3d.Finite(normalized))
                return false;

            assessment =
                new FiniteSoiDepartureAssessment
                {
                    ExitUniversalTimeSeconds = exitUt,
                    TimeFromBurnToExitSeconds =
                        exitUt -
                        ejection.BurnUniversalTimeSeconds,
                    PositionErrorMeters = positionError,
                    VelocityErrorMetersPerSecond =
                        velocityError,
                    PositionErrorFractionOfSoi =
                        positionFraction,
                    VelocityErrorFractionOfDepartureExcess =
                        velocityFraction,
                    NormalizedStateError =
                        normalized
                };

            return true;
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
                Math.Max(1.0, excessSpeed);

            double lowDt = 0.0;
            double highDt =
                Math.Max(30.0, estimate * 1.5);

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
                    0.5 * (lowDt + highDt);

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

        private static bool TryNormalize(
            Vector3d value,
            out Vector3d normalized)
        {
            normalized = new Vector3d();

            double magnitude = value.Magnitude;

            if (!FinitePositive(magnitude))
                return false;

            normalized =
                value * (1.0 / magnitude);

            return normalized.IsFinite;
        }

        private static bool FinitePositive(double value)
        {
            return
                Vector3d.Finite(value) &&
                value > 0.0;
        }
    }
}
