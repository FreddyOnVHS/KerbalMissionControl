using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// Bootstrap feasibility policy for selecting a coarse finite-SOI
    /// interplanetary departure.
    ///
    /// The source-SOI handoff is a trajectory-continuity constraint, not the
    /// final optimization objective. Candidates that satisfy a broad,
    /// dimensionless handoff tolerance are considered feasible; among those
    /// candidates departure delta-v is minimized.
    ///
    /// If no coarse Lambert candidate satisfies this bootstrap constraint,
    /// the lowest-delta-v coarse candidate remains available as the optimizer
    /// bootstrap. Constraint error never promotes a more expensive seed.
    /// </summary>
    public static class FiniteSoiCandidateSelectionPolicy
    {
        private const double MaximumPositionErrorFractionOfSoi = 0.10;
        private const double MaximumVelocityErrorFractionOfDepartureExcess = 0.10;

        public static double PositionErrorLimitFraction
        {
            get { return MaximumPositionErrorFractionOfSoi; }
        }

        public static double VelocityErrorLimitFraction
        {
            get { return MaximumVelocityErrorFractionOfDepartureExcess; }
        }

        public static bool IsFeasible(
            FiniteSoiDepartureAssessment assessment)
        {
            if (assessment == null ||
                !Vector3d.Finite(
                    assessment.PositionErrorFractionOfSoi) ||
                !Vector3d.Finite(
                    assessment.VelocityErrorFractionOfDepartureExcess))
                return false;

            return
                assessment.PositionErrorFractionOfSoi >= 0.0 &&
                assessment.VelocityErrorFractionOfDepartureExcess >= 0.0 &&
                assessment.PositionErrorFractionOfSoi <=
                    MaximumPositionErrorFractionOfSoi &&
                assessment.VelocityErrorFractionOfDepartureExcess <=
                    MaximumVelocityErrorFractionOfDepartureExcess;
        }
    }
}
