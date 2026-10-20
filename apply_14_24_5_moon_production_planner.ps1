$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$engine = Join-Path $root 'KMC.Engine\Navigation\MoonTransferPlanner.cs'
$proj = Join-Path $root 'KMC.Engine\KMC.Engine.csproj'
$tests = Join-Path $root 'Tools\NavigationTests\Program.cs'
$expected = Join-Path $root 'KMC.Engine\Navigation\MoonPeriapsisOptimizer.cs'
if (!(Test-Path $proj) -or !(Test-Path $tests) -or !(Test-Path $expected)) { throw 'Run from the KMC repo root at frozen 14.24.4a.' }
if (Test-Path $engine) { throw 'MoonTransferPlanner.cs already exists; no changes made.' }
$p = [IO.File]::ReadAllText($proj)
$t = [IO.File]::ReadAllText($tests)
$projectAnchor = '    <Compile Include="Navigation\MoonPeriapsisOptimizer.cs" />'
$testAnchor = '        Run("inclined Minmus finite-SOI periapsis targeting validation", InclinedMoonTargetingValidation);'
$methodAnchor = '    private static void SchematicAngleSelectionRegression()'
if (!$p.Contains($projectAnchor) -or !$t.Contains($testAnchor) -or !$t.Contains($methodAnchor) -or $t.Contains('MoonProductionPlannerRegression')) { throw 'Frozen baseline anchors do not match; nothing changed.' }
$backup = Join-Path $env:TEMP ('KMC_14_24_5_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $proj (Join-Path $backup 'KMC.Engine.csproj')
Copy-Item $tests (Join-Path $backup 'Program.cs')
$source = @'
using System;
using System.Collections.Generic;
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
            if (lowest == null) return false;
            plan = new MoonTransferPlan {
                Correction = lowest, LambertCandidates = lambertCount,
                SoiCandidates = feasible.Count, CorrectedCandidates = corrections
            };
            return true;
        }
        private static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static bool FinitePositive(double x) { return Finite(x) && x > 0; }
        private static double Sample(double a, double b, int count, int index)
        { return count == 1 ? a : a + (b - a) * index / (count - 1.0); }
    }
}
'@
$testMethod = @'
    private static void MoonProductionPlannerRegression()
    {
        const double primaryMu = 3.5316e12;
        OrbitalElements craft = new OrbitalElements {
            ReferenceBodyName = "Kerbin", SemiMajorAxisMeters = 700000,
            Eccentricity = 0, InclinationDegrees = 0,
            LongitudeOfAscendingNodeDegrees = 0, ArgumentOfPeriapsisDegrees = 0,
            EpochUniversalTimeSeconds = 0, MeanAnomalyAtEpochRadians = 0
        };
        CelestialBodyState minmus = new CelestialBodyState {
            Name = "Minmus", ParentName = "Kerbin",
            RadiusMeters = 60000, SoiRadiusMeters = 2247428,
            GravParameter = 1.7658e9,
            Orbit = new OrbitalElements {
                ReferenceBodyName = "Kerbin", SemiMajorAxisMeters = 47000000,
                Eccentricity = 0, InclinationDegrees = 6,
                LongitudeOfAscendingNodeDegrees = 78,
                ArgumentOfPeriapsisDegrees = 0,
                EpochUniversalTimeSeconds = 0, MeanAnomalyAtEpochRadians = 2
            }
        };
        MoonTransferPlan plan;
        bool found = MoonTransferPlanner.TryPlan(craft, minmus, primaryMu, 80000,
            0, 18000, 48000, 145000, 9, 9, 3, out plan);
        Check(found && plan != null && plan.Correction != null &&
            plan.Correction.Encounter.RequestedPeriapsisSatisfied &&
            !plan.Correction.Encounter.SurfaceImpact &&
            plan.SoiCandidates > 0 && plan.CorrectedCandidates > 0,
            "moon production planner did not return a verified Minmus periapsis");
        MoonEncounterAssessment independentlyVerified;
        Check(MoonEncounterEvaluator.TryAssess(craft, minmus, primaryMu,
            plan.Correction.Injection, 80000, out independentlyVerified) &&
            independentlyVerified.RequestedPeriapsisSatisfied &&
            !independentlyVerified.SurfaceImpact,
            "moon production planner returned an unverified candidate");
        Console.WriteLine("Moon plan: Lambert=" + plan.LambertCandidates +
            " finite-SOI=" + plan.SoiCandidates + " corrected=" +
            plan.CorrectedCandidates + " PeError=" +
            independentlyVerified.PeriapsisErrorMeters.ToString("0.0") + "m DV=" +
            plan.DepartureDeltaVMetersPerSecond.ToString("0.0") + "m/s");
        MoonTransferPlan invalid;
        Check(!MoonTransferPlanner.TryPlan(craft, minmus, primaryMu,
            double.NaN, 0, 18000, 48000, 145000, 9, 9, 3, out invalid) &&
            invalid == null, "moon production planner accepted invalid Pe");
    }

'@
$utf8 = New-Object System.Text.UTF8Encoding($false)
try {
    [IO.File]::WriteAllText($engine, $source, $utf8)
    $p = $p.Replace($projectAnchor, $projectAnchor + "`r`n" + '    <Compile Include="Navigation\MoonTransferPlanner.cs" />')
    $t = $t.Replace($testAnchor, $testAnchor + "`r`n" + '        Run("moon production planner returns verified Minmus transfer", MoonProductionPlannerRegression);')
    $t = $t.Replace($methodAnchor, $testMethod + "`r`n" + $methodAnchor)
    [IO.File]::WriteAllText($proj, $p, $utf8)
    [IO.File]::WriteAllText($tests, $t, $utf8)
} catch {
    Copy-Item (Join-Path $backup 'KMC.Engine.csproj') $proj -Force
    Copy-Item (Join-Path $backup 'Program.cs') $tests -Force
    Remove-Item $engine -ErrorAction SilentlyContinue
    throw
}
Write-Host 'KMC 14.24.5 moon production-planning engine patch applied.'
Write-Host "Backup: $backup"
Write-Host 'No KSP DLL replacement is required. Run NavigationTests.'
