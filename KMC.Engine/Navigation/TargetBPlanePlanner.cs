using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Exact hyperbolic B-plane geometry reconstructed from a finite-radius
    /// target-relative SOI entry state.
    ///
    /// The B-plane is perpendicular to the incoming hyperbolic asymptote, not
    /// to the instantaneous velocity at the SOI boundary.  At finite radius
    /// the target has already bent the trajectory, so using the local velocity
    /// direction produces a systematically wrong impact parameter for strong
    /// encounters.  Reconstruct the osculating hyperbola, recover the incoming
    /// v-infinity direction, then evaluate B from angular momentum.
    /// </summary>
    public static class TargetBPlanePlanner
    {
        public static bool TryCalculate(
            Vector3d relativeEntryPosition,
            Vector3d relativeEntryVelocity,
            double targetMu,
            double targetSoiRadiusMeters,
            double desiredPeriapsisRadiusMeters,
            out double actualBPlaneRadiusMeters,
            out double desiredBPlaneRadiusMeters,
            out double errorMeters)
        {
            Vector3d ignoredSHat;
            Vector3d ignoredBVector;
            return TryCalculateGeometry(
                relativeEntryPosition,
                relativeEntryVelocity,
                targetMu,
                targetSoiRadiusMeters,
                desiredPeriapsisRadiusMeters,
                out ignoredSHat,
                out ignoredBVector,
                out actualBPlaneRadiusMeters,
                out desiredBPlaneRadiusMeters,
                out errorMeters);
        }

        /// <summary>
        /// Recover the true incoming asymptote and B-vector from a finite-SOI
        /// state on an unbound two-body target-relative trajectory.
        ///
        /// For the osculating hyperbola:
        ///   v_inf^2 = v^2 - 2 mu / r
        ///   h         = r x v
        ///   e_vec     = (v x h)/mu - r_hat
        ///
        /// With p_hat along eccentricity/periapsis and
        /// q_hat = h_hat x p_hat, the incoming asymptotic velocity direction is
        ///
        ///   S_hat = p_hat/e + q_hat*sqrt(e^2 - 1)/e .
        ///
        /// Since h = B x (v_inf S_hat),
        ///
        ///   B_vec = (S_hat x h) / v_inf,
        ///   |B|   = |h| / v_inf.
        ///
        /// This remains exact at any finite SOI radius for the osculating
        /// two-body hyperbola and avoids the local-velocity approximation.
        /// </summary>
        public static bool TryCalculateGeometry(
            Vector3d relativeEntryPosition,
            Vector3d relativeEntryVelocity,
            double targetMu,
            double targetSoiRadiusMeters,
            double desiredPeriapsisRadiusMeters,
            out Vector3d incomingAsymptoteDirection,
            out Vector3d actualBVectorMeters,
            out double actualBPlaneRadiusMeters,
            out double desiredBPlaneRadiusMeters,
            out double errorMeters)
        {
            incomingAsymptoteDirection = new Vector3d(double.NaN, double.NaN, double.NaN);
            actualBVectorMeters = new Vector3d(double.NaN, double.NaN, double.NaN);
            actualBPlaneRadiusMeters = double.NaN;
            desiredBPlaneRadiusMeters = double.NaN;
            errorMeters = double.NaN;

            if (!relativeEntryPosition.IsFinite ||
                !relativeEntryVelocity.IsFinite ||
                !FinitePositive(targetMu) ||
                !FinitePositive(targetSoiRadiusMeters) ||
                !FinitePositive(desiredPeriapsisRadiusMeters) ||
                desiredPeriapsisRadiusMeters >= targetSoiRadiusMeters)
                return false;

            double r = relativeEntryPosition.Magnitude;
            double speed = relativeEntryVelocity.Magnitude;
            if (!FinitePositive(r) || !FinitePositive(speed))
                return false;

            double vinfSquared = speed * speed - 2.0 * targetMu / r;
            if (!FinitePositive(vinfSquared))
                return false;
            double vinf = Math.Sqrt(vinfSquared);

            Vector3d hVector = Vector3d.Cross(
                relativeEntryPosition,
                relativeEntryVelocity);
            double h = hVector.Magnitude;
            if (!FinitePositive(h))
                return false;

            Vector3d rHat = relativeEntryPosition * (1.0 / r);
            Vector3d eccentricityVector =
                Vector3d.Cross(relativeEntryVelocity, hVector) * (1.0 / targetMu) -
                rHat;
            double eccentricity = eccentricityVector.Magnitude;
            if (!FinitePositive(eccentricity) || eccentricity <= 1.0)
                return false;

            Vector3d pHat = eccentricityVector * (1.0 / eccentricity);
            Vector3d hHat = hVector * (1.0 / h);
            Vector3d qHat = Vector3d.Cross(hHat, pHat);
            double qMagnitude = qHat.Magnitude;
            if (!FinitePositive(qMagnitude))
                return false;
            qHat = qHat * (1.0 / qMagnitude);

            double asymptoteFactorSquared = eccentricity * eccentricity - 1.0;
            if (!FinitePositive(asymptoteFactorSquared))
                return false;

            incomingAsymptoteDirection =
                pHat * (1.0 / eccentricity) +
                qHat * (Math.Sqrt(asymptoteFactorSquared) / eccentricity);
            double sMagnitude = incomingAsymptoteDirection.Magnitude;
            if (!FinitePositive(sMagnitude))
                return false;
            incomingAsymptoteDirection =
                incomingAsymptoteDirection * (1.0 / sMagnitude);

            actualBVectorMeters =
                Vector3d.Cross(incomingAsymptoteDirection, hVector) *
                (1.0 / vinf);
            actualBPlaneRadiusMeters = actualBVectorMeters.Magnitude;
            if (!FinitePositive(actualBPlaneRadiusMeters))
                return false;

            // Equivalent scalar identity used as a numerical self-check:
            // |B| = |h| / v_inf.
            double angularMomentumB = h / vinf;
            double bConsistencyTolerance = Math.Max(1.0e-6, angularMomentumB * 1.0e-9);
            if (!FinitePositive(angularMomentumB) ||
                Math.Abs(actualBPlaneRadiusMeters - angularMomentumB) >
                    bConsistencyTolerance)
                return false;

            // Hyperbolic impact parameter associated with the requested
            // periapsis at the same v-infinity.
            desiredBPlaneRadiusMeters =
                desiredPeriapsisRadiusMeters *
                Math.Sqrt(
                    1.0 +
                    2.0 * targetMu /
                    (desiredPeriapsisRadiusMeters * vinfSquared));

            if (!FinitePositive(desiredBPlaneRadiusMeters))
                return false;

            desiredBPlaneRadiusMeters =
                Math.Min(desiredBPlaneRadiusMeters, targetSoiRadiusMeters * 0.99);

            errorMeters = Math.Abs(
                actualBPlaneRadiusMeters - desiredBPlaneRadiusMeters);

            return incomingAsymptoteDirection.IsFinite &&
                actualBVectorMeters.IsFinite &&
                Vector3d.Finite(errorMeters);
        }

        private static bool FinitePositive(double value)
        {
            return Vector3d.Finite(value) && value > 0.0;
        }
    }
}
