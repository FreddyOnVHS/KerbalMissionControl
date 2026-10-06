using System;

namespace KMC.Engine.CelestialMechanics
{
    /// <summary>
    /// Unperturbed two-body Cartesian propagation using universal variables.
    /// Supports elliptic and hyperbolic states without element conversion.
    /// </summary>
    public static class StateVectorPropagator
    {
        private const int MaximumIterations = 96;
        private const double RelativeTolerance = 2e-12;

        public static bool TryPropagate(
            StateVector initial,
            double targetUniversalTimeSeconds,
            out StateVector propagated)
        {
            propagated = null;

            if (initial == null ||
                !initial.Position.IsFinite ||
                !initial.Velocity.IsFinite ||
                !Vector3d.Finite(initial.GravParameter) ||
                initial.GravParameter <= 0.0 ||
                !Vector3d.Finite(targetUniversalTimeSeconds))
                return false;

            double dt =
                targetUniversalTimeSeconds -
                initial.UniversalTimeSeconds;

            if (!Vector3d.Finite(dt))
                return false;

            if (Math.Abs(dt) <= 1e-12)
            {
                propagated =
                    new StateVector(
                        initial.Position,
                        initial.Velocity,
                        targetUniversalTimeSeconds,
                        initial.GravParameter,
                        initial.ReferenceBodyName);
                return true;
            }

            Vector3d r0v = initial.Position;
            Vector3d v0v = initial.Velocity;
            double mu = initial.GravParameter;
            double r0 = r0v.Magnitude;

            if (!FinitePositive(r0))
                return false;

            double sqrtMu = Math.Sqrt(mu);
            double v02 = Vector3d.Dot(v0v, v0v);
            double r0DotV0 = Vector3d.Dot(r0v, v0v);
            double alpha = 2.0 / r0 - v02 / mu;

            if (!Vector3d.Finite(alpha))
                return false;

            double x =
                Math.Abs(alpha) > 1e-10
                    ? Math.Sign(dt) *
                        sqrtMu *
                        Math.Abs(alpha) *
                        Math.Abs(dt)
                    : Math.Sign(dt) *
                        sqrtMu *
                        Math.Abs(dt) /
                        r0;

            if (!Vector3d.Finite(x) || x == 0.0)
                x = Math.Sign(dt) * 1e-9;

            bool converged = false;

            for (int i = 0; i < MaximumIterations; i++)
            {
                double z = alpha * x * x;
                double c;
                double s;

                if (!TryStumpff(z, out c, out s))
                    return false;

                double x2 = x * x;
                double x3 = x2 * x;

                double f =
                    (r0DotV0 / sqrtMu) *
                        x2 * c +
                    (1.0 - alpha * r0) *
                        x3 * s +
                    r0 * x -
                    sqrtMu * dt;

                double fp =
                    (r0DotV0 / sqrtMu) *
                        x * (1.0 - z * s) +
                    (1.0 - alpha * r0) *
                        x2 * c +
                    r0;

                if (!Vector3d.Finite(f) ||
                    !Vector3d.Finite(fp) ||
                    Math.Abs(fp) < 1e-18)
                    return false;

                double step = f / fp;
                x -= step;

                if (!Vector3d.Finite(x))
                    return false;

                if (Math.Abs(step) <=
                    RelativeTolerance *
                    Math.Max(1.0, Math.Abs(x)))
                {
                    converged = true;
                    break;
                }
            }

            if (!converged)
                return false;

            double finalZ = alpha * x * x;
            double finalC;
            double finalS;

            if (!TryStumpff(
                    finalZ,
                    out finalC,
                    out finalS))
                return false;

            double fCoeff =
                1.0 -
                (x * x / r0) *
                finalC;

            double gCoeff =
                dt -
                (x * x * x / sqrtMu) *
                finalS;

            Vector3d position =
                r0v * fCoeff +
                v0v * gCoeff;

            double r = position.Magnitude;

            if (!FinitePositive(r))
                return false;

            double fdot =
                (sqrtMu / (r * r0)) *
                (alpha *
                    x * x * x *
                    finalS -
                 x);

            double gdot =
                1.0 -
                (x * x / r) *
                finalC;

            Vector3d velocity =
                r0v * fdot +
                v0v * gdot;

            if (!position.IsFinite ||
                !velocity.IsFinite)
                return false;

            propagated =
                new StateVector(
                    position,
                    velocity,
                    targetUniversalTimeSeconds,
                    mu,
                    initial.ReferenceBodyName);

            return true;
        }

        private static bool TryStumpff(
            double z,
            out double c,
            out double s)
        {
            c = double.NaN;
            s = double.NaN;

            if (!Vector3d.Finite(z))
                return false;

            if (Math.Abs(z) < 1e-8)
            {
                double z2 = z * z;
                double z3 = z2 * z;

                c =
                    0.5 -
                    z / 24.0 +
                    z2 / 720.0 -
                    z3 / 40320.0;

                s =
                    1.0 / 6.0 -
                    z / 120.0 +
                    z2 / 5040.0 -
                    z3 / 362880.0;

                return
                    Vector3d.Finite(c) &&
                    Vector3d.Finite(s);
            }

            if (z > 0.0)
            {
                double root = Math.Sqrt(z);

                c =
                    (1.0 - Math.Cos(root)) /
                    z;

                s =
                    (root - Math.Sin(root)) /
                    (root * root * root);
            }
            else
            {
                double root = Math.Sqrt(-z);

                if (root > 700.0)
                    return false;

                c =
                    (Math.Cosh(root) - 1.0) /
                    (-z);

                s =
                    (Math.Sinh(root) - root) /
                    (root * root * root);
            }

            return
                Vector3d.Finite(c) &&
                Vector3d.Finite(s);
        }

        private static bool FinitePositive(double value)
        {
            return
                Vector3d.Finite(value) &&
                value > 0.0;
        }
    }
}
