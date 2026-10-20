$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$tests = Join-Path $root 'Tools\NavigationTests\Program.cs'
if (!(Test-Path $tests) -or !(Test-Path (Join-Path $root 'KMC.Engine\Navigation\MoonTransferBootstrap.cs'))) {
    throw 'Run from the KMC repository root.'
}
$t = [IO.File]::ReadAllText($tests)
$startTag = '        MoonTransferCandidate injection;'
$endTag = '        MoonPeriapsisCorrection result;'
$methodStart = $t.IndexOf('    private static void InclinedMoonTargetingValidation()')
$methodEnd = $t.IndexOf('    private static void SchematicAngleSelectionRegression()', $methodStart)
if ($methodStart -lt 0 -or $methodEnd -lt 0) { throw '14.24.4 test not found. No changes made.' }
$method = $t.Substring($methodStart, $methodEnd - $methodStart)
$first = $method.IndexOf($startTag)
$last = $method.IndexOf($endTag)
if ($first -lt 0 -or $last -lt $first) { throw 'Expected Minmus test section not found. No changes made.' }
if ($method.Contains('int feasibleSeeds = 0;')) { throw '14.24.4a already installed. No changes made.' }
$new = @'
        const double requestedPeRadius = 80000;
        // Bootstrap is ranked by injection DV, not SOI feasibility. Inspect
        // individual bounded Lambert samples to choose a genuinely verified
        // inbound encounter before testing the periapsis correction layer.
        MoonTransferCandidate injection = null;
        MoonEncounterAssessment seedAssessment = null;
        int validLambertSeeds = 0;
        int feasibleSeeds = 0;
        const int samples = 9;
        for (int d = 0; d < samples; d++)
        {
            double dep = 18000.0 * d / (samples - 1);
            for (int f = 0; f < samples; f++)
            {
                double flight = 48000.0 + (145000.0 - 48000.0) * f / (samples - 1);
                MoonTransferCandidate sample;
                if (!MoonTransferBootstrap.TryFindInjection(craft, minmus,
                    primaryMu, dep, dep, flight, flight, 1, 1, out sample))
                    continue;
                validLambertSeeds++;
                MoonEncounterAssessment assessment;
                if (!MoonEncounterEvaluator.TryAssess(craft, minmus, primaryMu,
                    sample, requestedPeRadius, out assessment) || !assessment.EntersSoi)
                    continue;
                feasibleSeeds++;
                if (injection == null || sample.DeltaVMetersPerSecond < injection.DeltaVMetersPerSecond)
                {
                    injection = sample;
                    seedAssessment = assessment;
                }
            }
        }
        Console.WriteLine("Minmus seeds: Lambert=" + validLambertSeeds +
            " finite-SOI=" + feasibleSeeds);
        Check(injection != null && seedAssessment != null,
            "no finite-SOI Minmus seed in the prescribed search grid");
'@
$method = $method.Substring(0, $first) + $new + $method.Substring($last)
$backup = Join-Path $env:TEMP ('KMC_14_24_4a_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
Copy-Item $tests (Join-Path $backup 'Program.cs')
$t = $t.Substring(0, $methodStart) + $method + $t.Substring($methodEnd)
[IO.File]::WriteAllText($tests, $t, (New-Object Text.UTF8Encoding($false)))
Write-Host 'KMC 14.24.4a feasible Minmus seed validation installed (tests only).'
Write-Host "Backup: $backup"
Write-Host 'No KSP DLL replacement required.'
