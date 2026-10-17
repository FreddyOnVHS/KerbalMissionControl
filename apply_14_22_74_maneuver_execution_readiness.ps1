param(
    [string]$RepoRoot = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) {
        throw "Missing file: $path"
    }

    return [System.IO.File]::ReadAllText($path)
}

function WriteText([string]$path, [string]$text) {
    [System.IO.File]::WriteAllText(
        $path,
        $text,
        [System.Text.UTF8Encoding]::new($false))
}

# ===========================================================================
# 1. GUIDANCE SOLUTION MODEL: CREW-VISIBLE BURN READINESS
# ===========================================================================

$modelPath =
    Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSolutionModel.cs"

$model =
    ReadText $modelPath

if (-not $model.Contains("BurnReadiness =")) {
    $needle =
        '            Status = "UNAVAILABLE";'

    if (-not $model.Contains($needle)) {
        throw "Could not locate GuidanceSolutionModel default Status."
    }

    $model =
        $model.Replace(
            $needle,
            $needle + "`r`n" +
            '            BurnReadiness = "UNAVAILABLE";')
}

if (-not $model.Contains("public string BurnReadiness")) {
    $needle =
        "        public string Status { get; internal set; }"

    if (-not $model.Contains($needle)) {
        throw "Could not locate GuidanceSolutionModel Status property."
    }

    $model =
        $model.Replace(
            $needle,
            $needle + "`r`n" +
            "        public string BurnReadiness { get; internal set; }")
}

if (-not $model.Contains("BurnReadiness = BurnReadiness")) {
    $needle =
        "                Status = Status,"

    if (-not $model.Contains($needle)) {
        throw "Could not locate GuidanceSolutionModel Clone Status."
    }

    $model =
        $model.Replace(
            $needle,
            $needle + "`r`n" +
            "                BurnReadiness = BurnReadiness,")
}

WriteText $modelPath $model

# ===========================================================================
# 2. ENGINE: ONE AUTHORITATIVE READINESS CLASSIFICATION
# ===========================================================================

$guidancePath =
    Join-Path $RepoRoot "KMC.Engine\Guidance\GuidanceSystem.cs"

$guidance =
    ReadText $guidancePath

if (-not $guidance.Contains("PopulateBurnReadiness(_latest);")) {
    $needle = @'
            if (receivedUtc - _lastDiagnosticUtc >=
'@

    if (-not $guidance.Contains($needle)) {
        throw "Could not locate GuidanceSystem diagnostic boundary."
    }

    $replacement = @'
            /*
             * 14.22.74:
             * Classify crew-visible burn readiness only after the complete
             * Engine solution has been built. This keeps MAP/MNV ownership
             * separate from actual execution readiness.
             */
            PopulateBurnReadiness(_latest);

            if (receivedUtc - _lastDiagnosticUtc >=
'@

    $guidance =
        $guidance.Replace(
            $needle,
            $replacement)
}

if (-not $guidance.Contains("private static void PopulateBurnReadiness(")) {
    $needle =
        "        private static bool ShouldStartBurn("

    if (-not $guidance.Contains($needle)) {
        throw "Could not locate ShouldStartBurn insertion point."
    }

    $helper = @'
        private static void PopulateBurnReadiness(
            GuidanceSolutionModel solution)
        {
            if (solution == null)
            {
                return;
            }

            if (solution.BurnComplete)
            {
                solution.BurnReadiness =
                    solution.PostBurnVerificationAvailable
                        ? "COMPLETE / VERIFIED"
                        : "COMPLETE / VERIFYING";

                return;
            }

            if (solution.BurnActive)
            {
                solution.BurnReadiness =
                    "BURN IN PROGRESS";

                return;
            }

            if (!solution.Available)
            {
                solution.BurnReadiness =
                    "UNAVAILABLE";

                return;
            }

            if (string.Equals(
                    solution.Status,
                    "REPLAN REQUIRED",
                    StringComparison.OrdinalIgnoreCase) ||
                (IsFinite(solution.TimeToNodeSeconds) &&
                 solution.TimeToNodeSeconds < 0.0))
            {
                solution.BurnReadiness =
                    "REPLAN REQUIRED";

                return;
            }

            if (!solution.NodeExists)
            {
                solution.BurnReadiness =
                    "NODE NOT LOADED";

                return;
            }

            if (!solution.NodeVerified ||
                !solution.ExecutionAuthorized)
            {
                solution.BurnReadiness =
                    "NODE NOT VERIFIED";

                return;
            }

            if (!solution.ManeuverVectorAvailable)
            {
                solution.BurnReadiness =
                    "VECTOR UNAVAILABLE";

                return;
            }

            if (!IsFinite(
                    solution.AlignmentErrorDegrees))
            {
                solution.BurnReadiness =
                    "ALIGNMENT UNKNOWN";

                return;
            }

            if (solution.AlignmentErrorDegrees >
                AlignmentHoldToleranceDegrees)
            {
                solution.BurnReadiness =
                    "ALIGN REQUIRED";

                return;
            }

            if (!IsFinite(
                    solution.TimeToIgnitionSeconds))
            {
                solution.BurnReadiness =
                    "TIMING UNAVAILABLE";

                return;
            }

            if (solution.TimeToIgnitionSeconds <= 0.0)
            {
                solution.BurnReadiness =
                    "IGNITION DUE";

                return;
            }

            if (solution.TimeToIgnitionSeconds <=
                IgnitionStandbySeconds)
            {
                solution.BurnReadiness =
                    "FINAL COUNT";

                return;
            }

            solution.BurnReadiness =
                "READY";
        }

'@

    $guidance =
        $guidance.Replace(
            $needle,
            $helper + $needle)
}

WriteText $guidancePath $guidance

# ===========================================================================
# 3. GUID PAGE: SHOW BURN STATUS DIRECTLY
# ===========================================================================

$pagePath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\GuidancePage.cs"

$page =
    ReadText $pagePath

$old = @'
                    new FieldPair(
                        guidance != null &&
                        guidance.PostBurnVerificationAvailable
                            ? "RESULT"
                            : "STATUS",
                        guidance != null &&
                        guidance.PostBurnVerificationAvailable
                            ? guidance.PostBurnResult
                            : guidance != null &&
                              !activeManeuverNode &&
                              !guidance.BurnComplete
                                ? "PLAN NOT ACTIVE"
                                : guidance != null
                                    ? guidance.Status
                                    : "---")
'@

$new = @'
                    new FieldPair(
                        guidance != null &&
                        guidance.PostBurnVerificationAvailable
                            ? "RESULT"
                            : "BURN STATUS",
                        guidance != null &&
                        guidance.PostBurnVerificationAvailable
                            ? guidance.PostBurnResult
                            : guidance != null &&
                              !activeManeuverNode &&
                              !guidance.BurnComplete
                                ? "PLAN NOT ACTIVE"
                                : guidance != null
                                    ? guidance.BurnReadiness
                                    : "---")
'@

if ($page.Contains($old)) {
    $page =
        $page.Replace(
            $old,
            $new)
}
elseif (-not $page.Contains('"BURN STATUS"') -or
        -not $page.Contains("guidance.BurnReadiness")) {
    throw "Could not locate GuidancePage execution status field."
}

WriteText $pagePath $page

# ===========================================================================
# 4. PLAYER-FACING HANDOFF LANGUAGE
#    MAP/MNV are ready for GUIDANCE; GUID alone decides READY FOR BURN.
# ===========================================================================

$mapPath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\MapPage.cs"

$map =
    ReadText $mapPath

$map =
    $map.Replace(
        '"STATUS NODE CREATED / READY FOR BURN  AUTH COUPLED"',
        '"STATUS NODE CREATED / READY FOR GUIDANCE  AUTH COUPLED"')

WriteText $mapPath $map

$mnvPath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\ManeuverPage.cs"

$mnv =
    ReadText $mnvPath

$mnv =
    $mnv.Replace(
        '"NODE VERIFIED - KSP MATCHES " + Safe(plan.PlanId)',
        '"NODE VERIFIED / READY FOR GUIDANCE - KSP MATCHES " + Safe(plan.PlanId)')

WriteText $mnvPath $mnv

# ===========================================================================
# 5. NAVIGATION REGRESSIONS
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

# Existing transfer-summary test must now expect honest MAP handoff wording.
$tests =
    $tests.Replace(
        '"STATUS NODE CREATED / READY FOR BURN"',
        '"STATUS NODE CREATED / READY FOR GUIDANCE"')

if (-not $tests.Contains(
        'Run("maneuver execution readiness is Engine-owned and explicit"')) {

    $anchor =
        '        Run("standalone maneuver-vector UDP path removed", StandaloneManeuverVectorUdpRemoved);'

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate 14.22.73 cleanup-test run anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("maneuver execution readiness is Engine-owned and explicit", ManeuverExecutionReadinessRegression);')
}

if (-not $tests.Contains(
        "private static void ManeuverExecutionReadinessRegression()")) {

    $methodAnchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($methodAnchor)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void ManeuverExecutionReadinessRegression()
    {
        string enginePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSystem.cs");

        string modelPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.Engine",
                "Guidance",
                "GuidanceSolutionModel.cs");

        string guidancePagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "GuidancePage.cs");

        string mapPagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "MapPage.cs");

        string maneuverPagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "ManeuverPage.cs");

        Check(
            File.Exists(enginePath) &&
            File.Exists(modelPath) &&
            File.Exists(guidancePagePath) &&
            File.Exists(mapPagePath) &&
            File.Exists(maneuverPagePath),
            "readiness regression source unavailable");

        string engine =
            File.ReadAllText(enginePath);

        string model =
            File.ReadAllText(modelPath);

        string guidancePage =
            File.ReadAllText(guidancePagePath);

        string mapPage =
            File.ReadAllText(mapPagePath);

        string maneuverPage =
            File.ReadAllText(maneuverPagePath);

        Check(
            model.IndexOf(
                "public string BurnReadiness",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "PopulateBurnReadiness(_latest);",
                StringComparison.Ordinal) >= 0,
            "burn readiness is not Engine-owned");

        Check(
            engine.IndexOf(
                "!solution.NodeVerified",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "!solution.ExecutionAuthorized",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "!solution.ManeuverVectorAvailable",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "AlignmentHoldToleranceDegrees",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "TimeToIgnitionSeconds",
                StringComparison.Ordinal) >= 0,
            "readiness does not use verified node/vector/alignment/timing state");

        Check(
            engine.IndexOf(
                "\"ALIGN REQUIRED\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"READY\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"FINAL COUNT\"",
                StringComparison.Ordinal) >= 0 &&
            engine.IndexOf(
                "\"IGNITION DUE\"",
                StringComparison.Ordinal) >= 0,
            "crew readiness states incomplete");

        Check(
            guidancePage.IndexOf(
                "\"BURN STATUS\"",
                StringComparison.Ordinal) >= 0 &&
            guidancePage.IndexOf(
                "guidance.BurnReadiness",
                StringComparison.Ordinal) >= 0,
            "GUID does not expose explicit burn status");

        Check(
            mapPage.IndexOf(
                "STATUS NODE CREATED / READY FOR GUIDANCE",
                StringComparison.Ordinal) >= 0,
            "MAP still claims burn readiness before GUID");

        Check(
            maneuverPage.IndexOf(
                "NODE VERIFIED / READY FOR GUIDANCE",
                StringComparison.Ordinal) >= 0,
            "MNV handoff does not identify guidance readiness");
    }


'@

    $tests =
        $tests.Replace(
            $methodAnchor,
            $method + $methodAnchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.74 Maneuver Execution Readiness."
Write-Host "MAP/MNV now hand off to GUIDANCE; GUID alone reports burn readiness."
Write-Host "No transfer math, node math, authoritative-vector math, or burn-start criteria changed."
