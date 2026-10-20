using System;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>
    /// 14.24.3 experimental bounded 3D finite-SOI correction.
    /// This is a local feasibility search around a Lambert injection, not yet
    /// a globally optimal minimum-DV transfer and never creates a KSP node.
    /// </summary>
    public sealed class MoonPeriapsisCorrection
    {
        public MoonTransferCandidate Injection { get; internal set; }
        public MoonEncounterAssessment Encounter { get; internal set; }
        public bool RequestedPeriapsisSatisfied { get { return Encounter != null && Encounter.RequestedPeriapsisSatisfied; } }
        public double DepartureDeltaVMetersPerSecond { get { return Injection.DeltaVMetersPerSecond; } }
    }

    public static class MoonPeriapsisOptimizer
    {
        /// <summary>
        /// Coordinate-pattern search over the three inertial components of the
        /// injection impulse. Rank encounter feasibility and target Pe first;
        /// compare departure DV only among candidates satisfying the same Pe
        /// tolerance. An unverified best effort is returned even on failure.
        /// </summary>
        public static bool TryCorrect(OrbitalElements activeOrbit,
            CelestialBodyState moon, double primaryMu, MoonTransferCandidate seed,
            double requestedPeriapsisRadiusMeters, out MoonPeriapsisCorrection best)
        {
            best = null;
            if (activeOrbit == null || moon == null || seed == null ||
                !seed.DeltaV.IsFinite || double.IsNaN(requestedPeriapsisRadiusMeters) ||
                double.IsInfinity(requestedPeriapsisRadiusMeters) ||
                requestedPeriapsisRadiusMeters <= 0) return false;

            MoonEncounterAssessment original;
            if (!MoonEncounterEvaluator.TryAssess(activeOrbit, moon, primaryMu,
                    seed, requestedPeriapsisRadiusMeters, out original)) return false;

            MoonTransferCandidate incumbent = Clone(seed, seed.DeltaV);
            MoonEncounterAssessment current = original;
            double step = Math.Max(1.0, seed.DeltaVMetersPerSecond * 0.08);
            // Deterministic axes plus diagonal directions help leave a radial
            // collision without requiring a particular initial orbit plane.
            int[,] directions = {
                {1,0,0}, {-1,0,0}, {0,1,0}, {0,-1,0}, {0,0,1}, {0,0,-1},
                {1,1,0}, {1,-1,0}, {-1,1,0}, {-1,-1,0},
                {1,0,1}, {1,0,-1}, {-1,0,1}, {-1,0,-1},
                {0,1,1}, {0,1,-1}, {0,-1,1}, {0,-1,-1}
            };
            for (int iteration = 0; iteration < 110 && step >= 0.002; iteration++)
            {
                MoonTransferCandidate bestStepCandidate = incumbent;
                MoonEncounterAssessment bestStepAssessment = current;
                for (int i = 0; i < directions.GetLength(0); i++)
                {
                    double nx = directions[i,0], ny = directions[i,1], nz = directions[i,2];
                    double norm = Math.Sqrt(nx*nx + ny*ny + nz*nz);
                    Vector3d delta = new Vector3d(
                        incumbent.DeltaV.X + step * nx / norm,
                        incumbent.DeltaV.Y + step * ny / norm,
                        incumbent.DeltaV.Z + step * nz / norm);
                    MoonTransferCandidate candidate = Clone(seed, delta);
                    MoonEncounterAssessment assessment;
                    if (!MoonEncounterEvaluator.TryAssess(activeOrbit, moon, primaryMu,
                            candidate, requestedPeriapsisRadiusMeters, out assessment)) continue;
                    if (Better(candidate, assessment, bestStepCandidate, bestStepAssessment))
                    {
                        bestStepCandidate = candidate;
                        bestStepAssessment = assessment;
                    }
                }
                if (object.ReferenceEquals(bestStepCandidate, incumbent)) step *= 0.5;
                else { incumbent = bestStepCandidate; current = bestStepAssessment; }
            }
            best = new MoonPeriapsisCorrection { Injection = incumbent, Encounter = current };
            return best.RequestedPeriapsisSatisfied && !current.SurfaceImpact;
        }

        private static MoonTransferCandidate Clone(MoonTransferCandidate old, Vector3d dv)
        {
            return new MoonTransferCandidate {
                DepartureUniversalTimeSeconds = old.DepartureUniversalTimeSeconds,
                ArrivalUniversalTimeSeconds = old.ArrivalUniversalTimeSeconds,
                DeltaV = dv, ArrivalRelativeVelocity = old.ArrivalRelativeVelocity,
                Path = old.Path
            };
        }

        private static bool Better(MoonTransferCandidate a, MoonEncounterAssessment x,
            MoonTransferCandidate b, MoonEncounterAssessment y)
        {
            if (x.RequestedPeriapsisSatisfied != y.RequestedPeriapsisSatisfied)
                return x.RequestedPeriapsisSatisfied;
            if (x.RequestedPeriapsisSatisfied)
            {
                if (a.DeltaVMetersPerSecond < b.DeltaVMetersPerSecond - 1e-6) return true;
                if (a.DeltaVMetersPerSecond > b.DeltaVMetersPerSecond + 1e-6) return false;
            }
            double xe = Math.Abs(x.PeriapsisErrorMeters);
            double ye = Math.Abs(y.PeriapsisErrorMeters);
            if (xe < ye - 0.01) return true;
            if (xe > ye + 0.01) return false;
            return a.DeltaVMetersPerSecond < b.DeltaVMetersPerSecond;
        }
    }
}
