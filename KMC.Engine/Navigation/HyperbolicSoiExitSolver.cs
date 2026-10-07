using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Finds the future outbound intersection of an escaping two-body state
    /// with a spherical SOI boundary directly from its hyperbolic conic.
    ///
    /// This avoids a long universal-variable propagation/bracketing search
    /// for a quantity that is available analytically from the osculating
    /// hyperbola. No body-specific constants are used.
    /// </summary>
    public static class HyperbolicSoiExitSolver
    {
        private const double GeometryTolerance = 1e-10;

        public static bool TrySolve(
            StateVector initial,
            double soiRadiusMeters,
            out double exitUniversalTimeSeconds,
            out StateVector exitState)
        {
            exitUniversalTimeSeconds = double.NaN;
            exitState = null;

            if (initial == null ||
                !initial.Position.IsFinite ||
                !initial.Velocity.IsFinite ||
                !FinitePositive(initial.GravParameter) ||
                !FinitePositive(soiRadiusMeters))
                return false;

            double mu = initial.GravParameter;
            double r0 = initial.Position.Magnitude;

            if (!FinitePositive(r0) ||
                r0 >= soiRadiusMeters)
                return false;

            double v2 =
                Vector3d.Dot(
                    initial.Velocity,
                    initial.Velocity);

            double energy =
                0.5 * v2 -
                mu / r0;

            // A source-SOI escape must be genuinely hyperbolic.
            if (!FinitePositive(energy))
                return false;

            Vector3d hVector =
                Vector3d.Cross(
                    initial.Position,
                    initial.Velocity);

            double h =
                hVector.Magnitude;

            if (!FinitePositive(h))
                return false;

            Vector3d eccentricityVector =
                Vector3d.Cross(
                    initial.Velocity,
                    hVector) *
                    (1.0 / mu) -
                initial.Position *
                    (1.0 / r0);

            double eccentricity =
                eccentricityVector.Magnitude;

            if (!Vector3d.Finite(eccentricity) ||
                eccentricity <= 1.0 +
                    GeometryTolerance)
                return false;

            double p =
                h * h / mu;

            if (!FinitePositive(p))
                return false;

            Vector3d periapsisDirection =
                eccentricityVector *
                (1.0 / eccentricity);

            Vector3d normalDirection =
                hVector *
                (1.0 / h);

            Vector3d transverseDirection =
                Vector3d.Cross(
                    normalDirection,
                    periapsisDirection);

            double transverseMagnitude =
                transverseDirection.Magnitude;

            if (!FinitePositive(transverseMagnitude))
                return false;

            transverseDirection =
                transverseDirection *
                (1.0 / transverseMagnitude);

            double cosNu0 =
                Vector3d.Dot(
                    eccentricityVector,
                    initial.Position) /
                (eccentricity * r0);

            double sinNu0 =
                Vector3d.Dot(
                    Vector3d.Cross(
                        eccentricityVector,
                        initial.Position),
                    hVector) /
                (eccentricity * r0 * h);

            cosNu0 =
                Clamp(cosNu0, -1.0, 1.0);

            sinNu0 =
                Clamp(sinNu0, -1.0, 1.0);

            double nu0 =
                Math.Atan2(
                    sinNu0,
                    cosNu0);

            double targetCosNu =
                (p / soiRadiusMeters - 1.0) /
                eccentricity;

            if (!Vector3d.Finite(targetCosNu) ||
                targetCosNu < -1.0 -
                    GeometryTolerance ||
                targetCosNu > 1.0 +
                    GeometryTolerance)
                return false;

            targetCosNu =
                Clamp(
                    targetCosNu,
                    -1.0,
                    1.0);

            // Positive true anomaly is the future outbound SOI crossing.
            double targetNu =
                Math.Acos(
                    targetCosNu);

            if (!Vector3d.Finite(targetNu) ||
                targetNu <= nu0 +
                    GeometryTolerance)
                return false;

            double h0;
            double hTarget;

            if (!TryHyperbolicAnomaly(
                    nu0,
                    eccentricity,
                    out h0) ||
                !TryHyperbolicAnomaly(
                    targetNu,
                    eccentricity,
                    out hTarget))
                return false;

            double m0 =
                eccentricity *
                    Math.Sinh(h0) -
                h0;

            double mTarget =
                eccentricity *
                    Math.Sinh(hTarget) -
                hTarget;

            if (!Vector3d.Finite(m0) ||
                !Vector3d.Finite(mTarget))
                return false;

            double semiMajorAxisMagnitude =
                mu /
                (2.0 * energy);

            if (!FinitePositive(
                    semiMajorAxisMagnitude))
                return false;

            double meanMotion =
                Math.Sqrt(
                    mu /
                    (semiMajorAxisMagnitude *
                     semiMajorAxisMagnitude *
                     semiMajorAxisMagnitude));

            if (!FinitePositive(meanMotion))
                return false;

            double dt =
                (mTarget - m0) /
                meanMotion;

            if (!FinitePositive(dt))
                return false;

            double sinTargetNu =
                Math.Sin(targetNu);

            Vector3d positionDirection =
                periapsisDirection *
                    targetCosNu +
                transverseDirection *
                    sinTargetNu;

            Vector3d position =
                positionDirection *
                soiRadiusMeters;

            double velocityScale =
                Math.Sqrt(mu / p);

            Vector3d velocity =
                periapsisDirection *
                    (-sinTargetNu * velocityScale) +
                transverseDirection *
                    ((eccentricity +
                      targetCosNu) *
                     velocityScale);

            if (!position.IsFinite ||
                !velocity.IsFinite)
                return false;

            exitUniversalTimeSeconds =
                initial.UniversalTimeSeconds +
                dt;

            if (!Vector3d.Finite(
                    exitUniversalTimeSeconds))
                return false;

            exitState =
                new StateVector(
                    position,
                    velocity,
                    exitUniversalTimeSeconds,
                    mu,
                    initial.ReferenceBodyName);

            return true;
        }

        private static bool TryHyperbolicAnomaly(
            double trueAnomaly,
            double eccentricity,
            out double hyperbolicAnomaly)
        {
            hyperbolicAnomaly = double.NaN;

            if (!Vector3d.Finite(trueAnomaly) ||
                !Vector3d.Finite(eccentricity) ||
                eccentricity <= 1.0)
                return false;

            double factor =
                Math.Sqrt(
                    (eccentricity - 1.0) /
                    (eccentricity + 1.0));

            double value =
                factor *
                Math.Tan(
                    trueAnomaly * 0.5);

            if (!Vector3d.Finite(value) ||
                Math.Abs(value) >= 1.0)
                return false;

            hyperbolicAnomaly =
                Math.Log(
                    (1.0 + value) /
                    (1.0 - value));

            return
                Vector3d.Finite(
                    hyperbolicAnomaly);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            if (value < minimum)
                return minimum;

            if (value > maximum)
                return maximum;

            return value;
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
