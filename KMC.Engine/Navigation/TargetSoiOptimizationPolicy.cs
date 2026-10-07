using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Generic optimization policy for finite-SOI target shooting.
    ///
    /// MechJeb's interplanetary solver treats encounter/periapsis geometry as
    /// constraints and departure impulse as the quantity to minimize. KMC does
    /// not copy MechJeb's SQP implementation; this policy applies the same
    /// separation to KMC's bounded coordinate search.
    /// </summary>
    public static class TargetSoiOptimizationPolicy
    {
        private const double MaximumDeltaVRatioFromSeed = 1.50;
        private const double PeriapsisRelativeTolerance = 0.01;
        private const double PeriapsisAbsoluteToleranceMeters = 1000.0;

        public static double ComputeMaximumDepartureDeltaV(
            double seedDeltaVMetersPerSecond)
        {
            if (!FinitePositive(seedDeltaVMetersPerSecond))
                return double.NaN;

            return
                seedDeltaVMetersPerSecond *
                MaximumDeltaVRatioFromSeed;
        }

        public static bool IsWithinDepartureTrustRegion(
            double seedDeltaVMetersPerSecond,
            double candidateDeltaVMetersPerSecond)
        {
            double maximum =
                ComputeMaximumDepartureDeltaV(
                    seedDeltaVMetersPerSecond);

            return
                FinitePositive(candidateDeltaVMetersPerSecond) &&
                Vector3d.Finite(maximum) &&
                candidateDeltaVMetersPerSecond <=
                    maximum + 1e-9;
        }

        public static bool IsPeriapsisFeasible(
            TargetSoiShootingAssessment assessment)
        {
            if (assessment == null ||
                !assessment.PredictedEncounter ||
                assessment.PredictedCollision ||
                !FinitePositive(
                    assessment.DesiredPeriapsisRadiusMeters) ||
                !Vector3d.Finite(
                    assessment.TargetPeriapsisErrorMeters))
                return false;

            double tolerance =
                Math.Max(
                    PeriapsisAbsoluteToleranceMeters,
                    assessment.DesiredPeriapsisRadiusMeters *
                        PeriapsisRelativeTolerance);

            return
                assessment.TargetPeriapsisErrorMeters <=
                    tolerance;
        }

        public static double ComputePeriapsisConstraintScore(
            TargetSoiShootingAssessment assessment)
        {
            if (assessment == null ||
                !assessment.PredictedEncounter ||
                !FinitePositive(
                    assessment.DesiredPeriapsisRadiusMeters) ||
                !Vector3d.Finite(
                    assessment.TargetPeriapsisErrorMeters))
                return double.PositiveInfinity;

            double peFraction =
                assessment.TargetPeriapsisErrorMeters /
                assessment.DesiredPeriapsisRadiusMeters;

            double bPlaneFraction =
                Vector3d.Finite(
                    assessment.TargetBPlaneErrorFractionOfSoi)
                    ? assessment.TargetBPlaneErrorFractionOfSoi
                    : 1.0;

            double collisionPenalty =
                assessment.PredictedCollision
                    ? 10.0
                    : 0.0;

            return
                peFraction +
                0.25 * bPlaneFraction +
                collisionPenalty;
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
