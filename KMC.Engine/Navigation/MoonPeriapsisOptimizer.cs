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

            // First attempt inexpensive finite-difference trust-region targeting.
            MoonPeriapsisCorrection fast;
            if (TryFastCorrect(activeOrbit, moon, primaryMu, seed,
                requestedPeriapsisRadiusMeters, original, out fast))
            {
                best = fast;
                return true;
            }
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

        // 14.24.8b: bounded finite-difference targeting of the Pe constraint.
        // The legacy pattern search below remains the fallback if this does not
        // produce an independently assessed, safe periapsis solution.
        private static bool TryFastCorrect(OrbitalElements orbit, CelestialBodyState moon,
            double mu, MoonTransferCandidate seed, double requestedRadius,
            MoonEncounterAssessment original, out MoonPeriapsisCorrection result)
        {
            result = null;
            MoonTransferCandidate current = Clone(seed, seed.DeltaV);
            MoonEncounterAssessment assessment = original;
            MoonTransferCandidate feasible = null;
            MoonEncounterAssessment feasibleAssessment = null;
            double trust = Math.Max(3.0, Math.Min(50.0, seed.DeltaVMetersPerSecond * 0.04));
            for (int iteration = 0; iteration < 18; iteration++)
            {
                if (assessment.RequestedPeriapsisSatisfied && !assessment.SurfaceImpact)
                {
                    if (feasible == null || current.DeltaVMetersPerSecond < feasible.DeltaVMetersPerSecond)
                    {
                        feasible = current;
                        feasibleAssessment = assessment;
                    }
                    // The derivative search is for Pe feasibility. Once feasible,
                    // don't spend hundreds of trajectories chasing tiny DV savings.
                    break;
                }
                double epsilon = Math.Max(0.02, Math.Min(0.5, trust * 0.02));
                double[] gradient = new double[3];
                double[] components = { current.DeltaV.X, current.DeltaV.Y, current.DeltaV.Z };
                int available = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    double[] perturbed = (double[])components.Clone();
                    perturbed[axis] += epsilon;
                    MoonEncounterAssessment trialAssessment;
                    MoonTransferCandidate trial = Clone(seed,
                        new Vector3d(perturbed[0], perturbed[1], perturbed[2]));
                    if (MoonEncounterEvaluator.TryAssess(orbit, moon, mu, trial,
                        requestedRadius, out trialAssessment))
                    {
                        gradient[axis] = (trialAssessment.PeriapsisErrorMeters -
                            assessment.PeriapsisErrorMeters) / epsilon;
                        available++;
                    }
                }
                double norm2 = gradient[0] * gradient[0] +
                    gradient[1] * gradient[1] + gradient[2] * gradient[2];
                if (available == 0 || double.IsNaN(norm2) ||
                    double.IsInfinity(norm2) || norm2 < 1e-12) break;
                double factor = -assessment.PeriapsisErrorMeters / norm2;
                double magnitude = Math.Sqrt(norm2) * Math.Abs(factor);
                if (magnitude > trust) factor *= trust / magnitude;
                bool accepted = false;
                double scale = 1.0;
                for (int backtrack = 0; backtrack < 7; backtrack++)
                {
                    Vector3d dv = new Vector3d(
                        components[0] + scale * factor * gradient[0],
                        components[1] + scale * factor * gradient[1],
                        components[2] + scale * factor * gradient[2]);
                    MoonTransferCandidate trial = Clone(seed, dv);
                    MoonEncounterAssessment trialAssessment;
                    if (MoonEncounterEvaluator.TryAssess(orbit, moon, mu, trial,
                        requestedRadius, out trialAssessment) &&
                        (trialAssessment.RequestedPeriapsisSatisfied && !trialAssessment.SurfaceImpact ||
                         Math.Abs(trialAssessment.PeriapsisErrorMeters) <
                             Math.Abs(assessment.PeriapsisErrorMeters) - 0.1))
                    {
                        current = trial;
                        assessment = trialAssessment;
                        accepted = true;
                        break;
                    }
                    scale *= 0.5;
                }
                if (!accepted)
                {
                    trust *= 0.5;
                    if (trust < 0.01) break;
                }
                else trust = Math.Min(75.0, trust * 1.4);
            }
            if (feasible == null && assessment.RequestedPeriapsisSatisfied &&
                !assessment.SurfaceImpact)
            {
                feasible = current;
                feasibleAssessment = assessment;
            }
            if (feasible == null) return false;
            // Never trust the derivative-stage assessment alone.
            MoonEncounterAssessment verified;
            if (!MoonEncounterEvaluator.TryAssess(orbit, moon, mu, feasible,
                requestedRadius, out verified) || !verified.EntersSoi ||
                verified.SurfaceImpact || !verified.RequestedPeriapsisSatisfied)
                return false;
            result = new MoonPeriapsisCorrection {
                Injection = feasible, Encounter = verified
            };
            return true;
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
