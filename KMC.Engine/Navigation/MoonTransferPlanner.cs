using System;
using System.Collections.Generic;
using System.Diagnostics;
using KMC.Engine.CelestialMechanics;

namespace KMC.Engine.Navigation
{
    /// <summary>Bounded same-primary moon transfer search. Returns success only
    /// for independently verified finite-SOI, non-impact, periapsis-feasible
    /// transfers. This is not a global minimum-DV guarantee or a node uplink.</summary>
    public sealed class MoonTransferPlan
    {
        public MoonPeriapsisCorrection Correction { get; internal set; }
        public int LambertCandidates { get; internal set; }
        public int SoiCandidates { get; internal set; }
        public int CorrectedCandidates { get; internal set; }
        public long LambertAndSoiMilliseconds { get; internal set; }
        public long CorrectionMilliseconds { get; internal set; }
        public long TotalPlanningMilliseconds { get { return LambertAndSoiMilliseconds + CorrectionMilliseconds; } }
        public double DepartureDeltaVMetersPerSecond { get { return Correction.DepartureDeltaVMetersPerSecond; } }
    }

    public static class MoonTransferPlanner
    {
        private sealed class Seed
        {
            public MoonTransferCandidate Injection;
            public MoonEncounterAssessment Encounter;
        }

        public static bool TryPlan(OrbitalElements activeOrbit, CelestialBodyState moon,
            double primaryMu, double desiredPeriapsisRadiusMeters,
            double earliestDepartureUt, double latestDepartureUt,
            double minimumFlightSeconds, double maximumFlightSeconds,
            int departureSamples, int flightSamples, int maximumCorrections,
            out MoonTransferPlan plan)
        {
            plan = null;
            if (activeOrbit == null || moon == null || moon.Orbit == null ||
                MoonTransferRoute.Classify(activeOrbit.ReferenceBodyName, moon.Name,
                    moon.ParentName) != TransferRouteKind.SamePrimaryMoon ||
                (!string.IsNullOrWhiteSpace(moon.Orbit.ReferenceBodyName) &&
                 !string.Equals(moon.Orbit.ReferenceBodyName, moon.ParentName,
                     StringComparison.OrdinalIgnoreCase)) ||
                !FinitePositive(primaryMu) || !FinitePositive(moon.GravParameter) ||
                !FinitePositive(moon.RadiusMeters) || !FinitePositive(moon.SoiRadiusMeters) ||
                !FinitePositive(desiredPeriapsisRadiusMeters) ||
                desiredPeriapsisRadiusMeters <= moon.RadiusMeters ||
                desiredPeriapsisRadiusMeters >= moon.SoiRadiusMeters ||
                !Finite(earliestDepartureUt) || !Finite(latestDepartureUt) ||
                latestDepartureUt < earliestDepartureUt ||
                !FinitePositive(minimumFlightSeconds) || !Finite(maximumFlightSeconds) ||
                maximumFlightSeconds < minimumFlightSeconds ||
                departureSamples < 1 || departureSamples > 128 ||
                flightSamples < 1 || flightSamples > 128 ||
                maximumCorrections < 1 || maximumCorrections > 32)
                return false;

            Stopwatch stageClock = Stopwatch.StartNew();
            List<Seed> feasible = new List<Seed>();
            int lambertCount = 0;
            for (int d = 0; d < departureSamples; d++)
            {
                double dep = Sample(earliestDepartureUt, latestDepartureUt, departureSamples, d);
                StateVector craft;
                if (!KeplerPropagator.TryPropagate(activeOrbit, primaryMu, dep, out craft)) continue;
                for (int f = 0; f < flightSamples; f++)
                {
                    double tof = Sample(minimumFlightSeconds, maximumFlightSeconds, flightSamples, f);
                    double arr = dep + tof;
                    if (!Finite(arr)) continue;
                    StateVector target;
                    if (!KeplerPropagator.TryPropagate(moon.Orbit, primaryMu, arr, out target)) continue;
                    for (int arc = 0; arc < 2; arc++)
                    {
                        LambertSolution lambert;
                        LambertTransferPath path = arc == 0 ? LambertTransferPath.ShortWay : LambertTransferPath.LongWay;
                        if (!LambertSolver.TrySolve(craft.Position, target.Position, tof,
                                primaryMu, path, out lambert)) continue;
                        Vector3d dv = lambert.DepartureVelocity - craft.Velocity;
                        if (!dv.IsFinite || !Finite(dv.Magnitude)) continue;
                        lambertCount++;
                        MoonTransferCandidate candidate = new MoonTransferCandidate {
                            DepartureUniversalTimeSeconds = dep,
                            ArrivalUniversalTimeSeconds = arr,
                            DeltaV = dv,
                            ArrivalRelativeVelocity = lambert.ArrivalVelocity - target.Velocity,
                            Path = path
                        };
                        MoonEncounterAssessment assessment;
                        if (!MoonEncounterEvaluator.TryAssess(activeOrbit, moon, primaryMu,
                                candidate, desiredPeriapsisRadiusMeters, out assessment) ||
                            !assessment.EntersSoi) continue;
                        feasible.Add(new Seed { Injection = candidate, Encounter = assessment });
                    }
                }
            }
            stageClock.Stop();
            long searchMs = stageClock.ElapsedMilliseconds;
            stageClock.Restart();
            feasible.Sort((x,y) => x.Injection.DeltaVMetersPerSecond.CompareTo(y.Injection.DeltaVMetersPerSecond));
            MoonPeriapsisCorrection lowest = null;
            int corrections = 0;
            for (int i = 0; i < feasible.Count && corrections < maximumCorrections; i++)
            {
                // Evaluate the cheapest finite-SOI candidates first, then rank
                // only fully corrected and independently verified solutions by DV.
                corrections++;
                MoonPeriapsisCorrection correction;
                bool converged = MoonPeriapsisOptimizer.TryCorrect(activeOrbit, moon,
                    primaryMu, feasible[i].Injection, desiredPeriapsisRadiusMeters,
                    out correction);
                if (!converged || correction == null || correction.Injection == null) continue;
                MoonEncounterAssessment independent;
                if (!MoonEncounterEvaluator.TryAssess(activeOrbit, moon, primaryMu,
                        correction.Injection, desiredPeriapsisRadiusMeters, out independent) ||
                    !independent.EntersSoi || independent.SurfaceImpact ||
                    !independent.RequestedPeriapsisSatisfied) continue;
                if (lowest == null || correction.DepartureDeltaVMetersPerSecond <
                        lowest.DepartureDeltaVMetersPerSecond)
                    lowest = new MoonPeriapsisCorrection {
                        Injection = correction.Injection, Encounter = independent
                    };
            }
            stageClock.Stop();
            long correctionMs = stageClock.ElapsedMilliseconds;
            if (lowest == null) return false;
            plan = new MoonTransferPlan {
                Correction = lowest, LambertCandidates = lambertCount,
                SoiCandidates = feasible.Count, CorrectedCandidates = corrections,
                LambertAndSoiMilliseconds = searchMs, CorrectionMilliseconds = correctionMs
            };
            return true;
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static bool FinitePositive(double x) { return Finite(x) && x > 0; }
        private static double Sample(double a, double b, int count, int index)
        { return count == 1 ? a : a + (b - a) * index / (count - 1.0); }
    }
}