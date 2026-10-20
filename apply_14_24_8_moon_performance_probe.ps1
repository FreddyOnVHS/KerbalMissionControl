$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path
$e = Join-Path $root 'KMC.Engine\Navigation\MoonEncounterEvaluator.cs'
$p = Join-Path $root 'KMC.Engine\Navigation\MoonTransferPlanner.cs'
$t = Join-Path $root 'Tools\NavigationTests\Program.cs'
foreach ($f in @($e,$p,$t)) { if (!(Test-Path $f)) { throw "Missing KMC source: $f (run from repository root)" } }
$ec = [IO.File]::ReadAllText($e)
$pc = [IO.File]::ReadAllText($p)
$tc = [IO.File]::ReadAllText($t)
if ($pc.Contains('LambertAndSoiMilliseconds') -or $ec.Contains('BisectIterations = 32')) { throw '14.24.8 already installed; no changes made.' }
if (!$ec.Contains('private const int BisectIterations = 52;') -or
    !$pc.Contains('public int CorrectedCandidates { get; internal set; }') -or
    !$pc.Contains('            List<Seed> feasible = new List<Seed>();') -or
    !$pc.Contains('            feasible.Sort(') -or
    !$pc.Contains('            if (lowest == null) return false;') -or
    !$pc.Contains('                SoiCandidates = feasible.Count, CorrectedCandidates = corrections') -or
    !$tc.Contains('"moon production planner returns verified Minmus transfer", MoonProductionPlannerRegression)')) { throw 'Frozen 14.24.7 source anchors not found. No changes made.' }
# The bisection is already bracketed by 320 samples. 32 halvings leave a UT interval
# far below a millisecond for supported transfer horizons; no reduction to sampling.
$ec = $ec.Replace('private const int BisectIterations = 52;', 'private const int BisectIterations = 32;')
$pc = $pc.Replace('using System.Collections.Generic;', "using System.Collections.Generic;`nusing System.Diagnostics;")
$pc = $pc.Replace('public int CorrectedCandidates { get; internal set; }', @'
public int CorrectedCandidates { get; internal set; }
        public long LambertAndSoiMilliseconds { get; internal set; }
        public long CorrectionMilliseconds { get; internal set; }
        public long TotalPlanningMilliseconds { get { return LambertAndSoiMilliseconds + CorrectionMilliseconds; } }
'@.TrimEnd())
$pc = $pc.Replace('            List<Seed> feasible = new List<Seed>();', @'
            Stopwatch stageClock = Stopwatch.StartNew();
            List<Seed> feasible = new List<Seed>();
'@.TrimEnd())
$pc = $pc.Replace('            feasible.Sort(', @'
            stageClock.Stop();
            long searchMs = stageClock.ElapsedMilliseconds;
            stageClock.Restart();
            feasible.Sort(
'@.TrimEnd())
$pc = $pc.Replace('            if (lowest == null) return false;', @'
            stageClock.Stop();
            long correctionMs = stageClock.ElapsedMilliseconds;
            if (lowest == null) return false;
'@.TrimEnd())
$pc = $pc.Replace('                SoiCandidates = feasible.Count, CorrectedCandidates = corrections', '                SoiCandidates = feasible.Count, CorrectedCandidates = corrections,`n                LambertAndSoiMilliseconds = searchMs, CorrectionMilliseconds = correctionMs'.Replace('`n',"`n"))
# Ensure no accidental syntax from whitespace-sensitive replacements.
$tc = $tc.Replace('"moon production planner returns verified Minmus transfer", MoonProductionPlannerRegression);', '"moon production planner returns verified Minmus transfer", MoonProductionPlannerRegression);`n        Run("moon performance staging and encounter precision", MoonPerformanceStageRegression);'.Replace('`n',"`n"))
$marker = '    private static void SchematicAngleSelectionRegression()'
if (!$tc.Contains($marker)) { throw 'Test insertion marker absent. No changes made.' }
$test = @'
    private static void MoonPerformanceStageRegression()
    {
        // Source contract: the search and correction phases have independent
        // wall-clock measures; fast SOI bisection retains dense 320-sample
        // encounter discovery and never replaces independent final assessment.
        string planner = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Engine", "Navigation", "MoonTransferPlanner.cs"));
        string evaluator = File.ReadAllText(Path.Combine(Environment.CurrentDirectory,
            "KMC.Engine", "Navigation", "MoonEncounterEvaluator.cs"));
        Check(planner.Contains("LambertAndSoiMilliseconds") &&
            planner.Contains("CorrectionMilliseconds") &&
            planner.Contains("Stopwatch stageClock") &&
            evaluator.Contains("private const int Samples = 320;") &&
            evaluator.Contains("private const int BisectIterations = 32;"),
            "moon performance timing or finite-SOI precision safeguards missing");
    }

'@
$tc = $tc.Replace($marker, $test + $marker)
$backup = Join-Path $env:TEMP ('KMC_14_24_8_backup_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
New-Item -ItemType Directory -Path $backup | Out-Null
foreach($file in @($e,$p,$t)) { Copy-Item $file (Join-Path $backup ([IO.Path]::GetFileName($file))) }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText($e,$ec,$utf8)
[IO.File]::WriteAllText($p,$pc,$utf8)
[IO.File]::WriteAllText($t,$tc,$utf8)
Write-Host 'KMC 14.24.8 performance probe applied: tighter SOI bisection + search/correction timing.'
Write-Host "Backup: $backup"
Write-Host 'Build/test locally before pushing. No KSP game-folder DLL replacement.'
