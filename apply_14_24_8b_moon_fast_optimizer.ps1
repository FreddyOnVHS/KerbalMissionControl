$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$optimizer = Join-Path $root 'KMC.Engine\Navigation\MoonPeriapsisOptimizer.cs'
$planner = Join-Path $root 'KMC.Engine\Navigation\MoonTransferPlanner.cs'
if (!(Test-Path $optimizer) -or !(Test-Path $planner)) { throw 'Run from KMC repository root.' }
$code = [IO.File]::ReadAllText($optimizer)
$plan = [IO.File]::ReadAllText($planner)
if ($code.Contains('private static bool TryFastCorrect(')) { throw '14.24.8b already applied.' }
if (!$code.Contains('MoonEncounterAssessment original;') -or !$code.Contains('private static MoonTransferCandidate Clone(')) {
    throw 'Optimizer does not match expected frozen baseline. No files changed.'
}
if (!$plan.Contains('LambertAndSoiMilliseconds') -or !$plan.Contains('CorrectionMilliseconds')) {
    throw 'Apply 14.24.8 performance probe first. No files changed.'
}
$backup = Join-Path $env:TEMP ('KMC_14_24_8b_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $optimizer (Join-Path $backup 'MoonPeriapsisOptimizer.cs')
$needle = '            MoonTransferCandidate incumbent = Clone(seed, seed.DeltaV);'
$replacement = @'
            // First attempt inexpensive finite-difference trust-region targeting.
            MoonPeriapsisCorrection fast;
            if (TryFastCorrect(activeOrbit, moon, primaryMu, seed,
                requestedPeriapsisRadiusMeters, original, out fast))
            {
                best = fast;
                return true;
            }

'@ + $needle
if (!$code.Contains($needle)) { throw 'Expected fallback search anchor absent. No changes made.' }
$code = $code.Replace($needle, $replacement)
$marker = '        private static MoonTransferCandidate Clone('
$insert = @'
__FAST__
'@
$insert = $insert.Replace('__FAST__', @'
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
'@)
$code = $code.Replace($marker, $insert + [Environment]::NewLine + $marker)
[IO.File]::WriteAllText($optimizer, $code, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.8b fast Pe-targeting optimizer installed with legacy fallback.'
Write-Host "Backup: $backup"
Write-Host 'Engine only; no KSP DLL replacement required.'
