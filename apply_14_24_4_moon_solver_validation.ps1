$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$project = Join-Path $root 'KMC.Engine\Navigation\MoonPeriapsisOptimizer.cs'
$tests = Join-Path $root 'Tools\NavigationTests\Program.cs'
if (!(Test-Path $project) -or !(Test-Path $tests)) { throw 'Run from the KMC repository root.' }
$t = [IO.File]::ReadAllText($tests)
$p = [IO.File]::ReadAllText($project)
$registration = '        Run("moon 3D periapsis correction preserves SOI feasibility", MoonPeriapsisCorrectionRegression);'
$anchor = '    private static void SchematicAngleSelectionRegression()'
$newRegistration = '        Run("inclined Minmus finite-SOI periapsis targeting validation", InclinedMoonTargetingValidation);'
if (!$t.Contains($registration) -or !$t.Contains($anchor) -or !$p.Contains('public static class MoonPeriapsisOptimizer')) { throw 'Frozen 14.24.3 source anchors missing. No changes made.' }
if ($t.Contains('InclinedMoonTargetingValidation')) { throw '14.24.4 validation already present. No changes made.' }
$method = @'
    // 14.24.4: separate noncoplanar Minmus-like target case. Convergence is
    // independently checked against the actual patched-conic encounter.
    private static void InclinedMoonTargetingValidation()
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
        MoonTransferCandidate injection;
        Check(MoonTransferBootstrap.TryFindInjection(craft, minmus, primaryMu,
            0, 18000, 48000, 145000, 9, 9, out injection),
            "inclined Minmus Lambert bootstrap missing");
        const double requestedPeRadius = 80000;
        MoonEncounterAssessment seedAssessment;
        Check(MoonEncounterEvaluator.TryAssess(craft, minmus, primaryMu,
            injection, requestedPeRadius, out seedAssessment) && seedAssessment.EntersSoi,
            "inclined Minmus seed has no inbound SOI crossing");
        MoonPeriapsisCorrection result;
        bool converged = MoonPeriapsisOptimizer.TryCorrect(craft, minmus,
            primaryMu, injection, requestedPeRadius, out result);
        Check(result != null && result.Encounter != null &&
            result.Injection.DeltaV.IsFinite && result.Encounter.EntersSoi,
            "inclined Minmus correction lost feasible SOI entry");
        Check(Math.Abs(result.Encounter.PeriapsisErrorMeters) <=
            Math.Abs(seedAssessment.PeriapsisErrorMeters) + 0.02,
            "inclined Minmus correction worsened periapsis error");
        Check(!converged || (result.RequestedPeriapsisSatisfied &&
            !result.Encounter.SurfaceImpact &&
            Math.Abs(result.Encounter.PeriapsisErrorMeters) <= 1000),
            "inclined Minmus optimizer falsely declared convergence");
        // Tight verification: the reported encounter is recomputed, not trusted
        // solely because the optimizer returned a candidate.
        MoonEncounterAssessment independent;
        Check(MoonEncounterEvaluator.TryAssess(craft, minmus, primaryMu,
            result.Injection, requestedPeRadius, out independent) &&
            Math.Abs(independent.PredictedPeriapsisRadiusMeters -
                result.Encounter.PredictedPeriapsisRadiusMeters) < 0.1,
            "inclined Minmus correction disagrees with independent evaluation");
        Console.WriteLine("Minmus validation: converged=" + converged +
            " PeError=" + result.Encounter.PeriapsisErrorMeters.ToString("0.0") +
            "m DV=" + result.DepartureDeltaVMetersPerSecond.ToString("0.0") + "m/s");
    }

'@
$backup = Join-Path $env:TEMP ('KMC_14_24_4_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $tests (Join-Path $backup 'Program.cs')
try {
  $t = $t.Replace($registration, $registration + "`r`n" + $newRegistration)
  $t = $t.Replace($anchor, $method + $anchor)
  [IO.File]::WriteAllText($tests, $t, (New-Object Text.UTF8Encoding($false)))
} catch {
  Copy-Item (Join-Path $backup 'Program.cs') $tests -Force
  throw
}
Write-Host 'KMC 14.24.4 validation patch applied (tests only).'
Write-Host "Backup: $backup"
Write-Host 'No KSP DLL replacement. Run NavigationTests and report the Minmus validation diagnostic.'
