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
# 1. ADD OPERATOR FEEDBACK STORE
# ===========================================================================

$feedbackPath =
    Join-Path $RepoRoot "KMC.MissionControl\Engineering\ManeuverOperatorFeedbackStore.cs"

if (-not (Test-Path $feedbackPath)) {
    $feedbackSource = @'
using System;

namespace KMC.MissionControl.Engineering
{
    public sealed class ManeuverOperatorFeedbackSnapshot
    {
        public string Text { get; set; }
        public bool Success { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    /// <summary>
    /// Short-lived operator feedback for MNV header actions.
    /// This is presentation state only; it does not own maneuver planning
    /// or uplink truth.
    /// </summary>
    public static class ManeuverOperatorFeedbackStore
    {
        private static readonly object SyncRoot =
            new object();

        private static ManeuverOperatorFeedbackSnapshot _latest;

        public static void Publish(
            string text,
            bool success)
        {
            lock (SyncRoot)
            {
                _latest =
                    new ManeuverOperatorFeedbackSnapshot
                    {
                        Text =
                            text ??
                            string.Empty,

                        Success =
                            success,

                        TimestampUtc =
                            DateTime.UtcNow
                    };
            }
        }

        public static bool TryGetLatest(
            out ManeuverOperatorFeedbackSnapshot snapshot)
        {
            lock (SyncRoot)
            {
                if (_latest == null)
                {
                    snapshot = null;
                    return false;
                }

                /*
                 * Keep action feedback visible long enough for the operator
                 * to see it, but never let stale UI text become maneuver truth.
                 */
                if (DateTime.UtcNow - _latest.TimestampUtc >
                    TimeSpan.FromSeconds(8.0))
                {
                    snapshot = null;
                    return false;
                }

                snapshot =
                    new ManeuverOperatorFeedbackSnapshot
                    {
                        Text =
                            _latest.Text ??
                            string.Empty,

                        Success =
                            _latest.Success,

                        TimestampUtc =
                            _latest.TimestampUtc
                    };

                return true;
            }
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest =
                    null;
            }
        }
    }
}
'@

    WriteText $feedbackPath $feedbackSource
}

# ===========================================================================
# 2. ADD FILE TO OLD-STYLE CSPROJ
# ===========================================================================

$projectPath =
    Join-Path $RepoRoot "KMC.MissionControl\KMC.MissionControl.csproj"

$project =
    ReadText $projectPath

if (-not $project.Contains(
        'Compile Include="Engineering\ManeuverOperatorFeedbackStore.cs"')) {

    $needle =
        '<Compile Include="Engineering\EngineeringSnapshotStore.cs" /><Compile Include="Engineering\ElectricalLoadLeaseSender.cs" /><Compile Include="Engineering\ManeuverUplinkStatusStore.cs" />'

    $replacement =
        '<Compile Include="Engineering\EngineeringSnapshotStore.cs" /><Compile Include="Engineering\ElectricalLoadLeaseSender.cs" /><Compile Include="Engineering\ManeuverUplinkStatusStore.cs" /><Compile Include="Engineering\ManeuverOperatorFeedbackStore.cs" />'

    if (-not $project.Contains($needle)) {
        throw "Could not locate MissionControl Engineering compile list."
    }

    $project =
        $project.Replace(
            $needle,
            $replacement)

    WriteText $projectPath $project
}

# ===========================================================================
# 3. MAINFORM: PUBLISH COMPUTE / UPLOAD RESULTS
# ===========================================================================

$mainPath =
    Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"

$main =
    ReadText $mainPath

# Compute action feedback immediately after request publication.
$computeNeedle = @'
            ManeuverRequestStore.Set(
                new ManeuverRequestModel
                {
'@

if (-not $main.Contains($computeNeedle)) {
    throw "Could not locate OnManeuverComputeClick ManeuverRequestStore.Set."
}

# Add only once by inserting after complete Set(...) block using the known RequestedUtc tail.
$computeTail = @'
                    RequestedUtc =
                        DateTime.UtcNow
                });

            _missionDisplay.RequestRender();
'@

$computeReplacement = @'
                    RequestedUtc =
                        DateTime.UtcNow
                });

            ManeuverOperatorFeedbackStore.Publish(
                "COMPUTE REQUESTED - WAITING FOR ENGINE PLAN",
                true);

            _missionDisplay.RequestRender();
'@

if ($main.Contains($computeTail)) {
    $main =
        $main.Replace(
            $computeTail,
            $computeReplacement)
}
elseif (-not $main.Contains(
        '"COMPUTE REQUESTED - WAITING FOR ENGINE PLAN"')) {
    throw "Could not locate compute request tail."
}

# Replace upload handler's discarded result with explicit operator feedback.
$oldUpload = @'
            string resultText;

            _receiver.UploadLatestManeuver(
                out resultText);

            _missionDisplay.RequestRender();
'@

$newUpload = @'
            string resultText;

            bool uploadAccepted =
                _receiver.UploadLatestManeuver(
                    out resultText);

            ManeuverOperatorFeedbackStore.Publish(
                string.IsNullOrWhiteSpace(
                    resultText)
                    ? uploadAccepted
                        ? "UPLINK REQUEST ACCEPTED"
                        : "UPLINK REQUEST REJECTED"
                    : resultText,
                uploadAccepted);

            _missionDisplay.RequestRender();
'@

if ($main.Contains($oldUpload)) {
    $main =
        $main.Replace(
            $oldUpload,
            $newUpload)
}
elseif (-not $main.Contains("bool uploadAccepted =")) {
    throw "Could not locate OnManeuverUploadClick result handling."
}

# Clear UI-only state at startup and shutdown.
$loadNeedle =
    "                ManeuverRequestStore.Reset();"

$loadReplacement =
    "                ManeuverRequestStore.Reset();`r`n" +
    "                ManeuverOperatorFeedbackStore.Clear();"

if ($main.Contains($loadNeedle) -and
    -not $main.Contains(
        "ManeuverRequestStore.Reset();`r`n                ManeuverOperatorFeedbackStore.Clear();")) {
    $main =
        $main.Replace(
            $loadNeedle,
            $loadReplacement)
}

$closeNeedle =
    "            ManeuverPlanPromotionStore.Clear();"

# There are multiple occurrences; add clear to the FormClosing occurrence only
# by replacing the sequence around the existing store cleanup.
$closeBlock = @'
            KmcManeuverPlanStore.Clear();
            ManeuverPlanPromotionStore.Clear();

            _maneuverQueueTransport.Dispose();
'@

$closeReplacement = @'
            KmcManeuverPlanStore.Clear();
            ManeuverPlanPromotionStore.Clear();
            ManeuverOperatorFeedbackStore.Clear();

            _maneuverQueueTransport.Dispose();
'@

if ($main.Contains($closeBlock)) {
    $main =
        $main.Replace(
            $closeBlock,
            $closeReplacement)
}

WriteText $mainPath $main

# ===========================================================================
# 4. MNV PAGE: SURFACE FEEDBACK IN FOOTER, EVEN WITH NO PLAN
# ===========================================================================

$pagePath =
    Join-Path $RepoRoot "KMC.MissionControl\Pages\ManeuverPage.cs"

$page =
    ReadText $pagePath

# Capture feedback near maneuver state lookup.
$stateNeedle = @'
            ManeuverPlanModel plan;
            GuidanceSolutionModel guidance;
            GetLatestManeuverState(out plan, out guidance);

            if (plan == null)
'@

$stateReplacement = @'
            ManeuverPlanModel plan;
            GuidanceSolutionModel guidance;
            GetLatestManeuverState(out plan, out guidance);

            ManeuverOperatorFeedbackSnapshot operatorFeedback;
            bool hasOperatorFeedback =
                ManeuverOperatorFeedbackStore.TryGetLatest(
                    out operatorFeedback);

            if (plan == null)
'@

if ($page.Contains($stateNeedle)) {
    $page =
        $page.Replace(
            $stateNeedle,
            $stateReplacement)
}
elseif (-not $page.Contains("hasOperatorFeedback")) {
    throw "Could not locate ManeuverPage state lookup."
}

# No-plan path: show operator action instead of generic silence.
$noPlanOld = @'
                DrawActiveManeuvers(context, emptyRegion, null);
                DrawReviewBand(context, "NO ENGINE-OWNED MANEUVER PLAN AVAILABLE", false);
                return;
'@

$noPlanNew = @'
                DrawActiveManeuvers(context, emptyRegion, null);

                DrawReviewBand(
                    context,
                    hasOperatorFeedback &&
                    operatorFeedback != null &&
                    !string.IsNullOrWhiteSpace(
                        operatorFeedback.Text)
                        ? operatorFeedback.Text
                        : "NO ENGINE-OWNED MANEUVER PLAN AVAILABLE",
                    hasOperatorFeedback &&
                    operatorFeedback != null &&
                    operatorFeedback.Success);

                return;
'@

if ($page.Contains($noPlanOld)) {
    $page =
        $page.Replace(
            $noPlanOld,
            $noPlanNew)
}
elseif (-not $page.Contains("operatorFeedback.Text")) {
    throw "Could not locate ManeuverPage no-plan review band."
}

# Existing normal footer remains authoritative, but fresh button feedback
# temporarily takes presentation precedence.
$footerNeedle = @'
            bool healthy = manualComplete || (plan.Available && plan.NodeUniversalTimeAvailable && !crewModified && !nodeRemoved && !vesselNotActive);
            DrawReviewBand(context, footer, healthy);
'@

$footerReplacement = @'
            bool healthy = manualComplete || (plan.Available && plan.NodeUniversalTimeAvailable && !crewModified && !nodeRemoved && !vesselNotActive);

            if (hasOperatorFeedback &&
                operatorFeedback != null &&
                !string.IsNullOrWhiteSpace(
                    operatorFeedback.Text))
            {
                footer =
                    operatorFeedback.Text;

                healthy =
                    operatorFeedback.Success;
            }

            DrawReviewBand(context, footer, healthy);
'@

if ($page.Contains($footerNeedle)) {
    $page =
        $page.Replace(
            $footerNeedle,
            $footerReplacement)
}
elseif (-not $page.Contains("operatorFeedback.Success")) {
    throw "Could not locate ManeuverPage footer draw."
}

WriteText $pagePath $page

# ===========================================================================
# 5. REGRESSION
# ===========================================================================

$testPath =
    Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"

$tests =
    ReadText $testPath

if (-not $tests.Contains(
        'Run("MNV compute and upload actions expose operator feedback"')) {

    # 14.22.75 should already be present locally; anchor after its new test.
    $anchor =
        '        Run("GUID final count and ignition cues are explicit", GuidanceFinalCountCueRegression);'

    if (-not $tests.Contains($anchor)) {
        # Allow repair on 14.22.74 if 14.22.75 test name differs/not yet applied.
        $anchor =
            '        Run("maneuver execution readiness is Engine-owned and explicit", ManeuverExecutionReadinessRegression);'
    }

    if (-not $tests.Contains($anchor)) {
        throw "Could not locate NavigationTests insertion anchor."
    }

    $tests =
        $tests.Replace(
            $anchor,
            $anchor + "`r`n" +
            '        Run("MNV compute and upload actions expose operator feedback", ManeuverOperatorFeedbackRegression);')
}

if (-not $tests.Contains(
        "private static void ManeuverOperatorFeedbackRegression()")) {

    $methodAnchor =
        "    private static void ManeuverUplinkDesiredPeriapsis()"

    if (-not $tests.Contains($methodAnchor)) {
        throw "Could not locate NavigationTests method insertion point."
    }

    $method = @'
    private static void ManeuverOperatorFeedbackRegression()
    {
        string mainPath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "MainForm.cs");

        string pagePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Pages",
                "ManeuverPage.cs");

        string storePath =
            Path.Combine(
                Environment.CurrentDirectory,
                "KMC.MissionControl",
                "Engineering",
                "ManeuverOperatorFeedbackStore.cs");

        Check(
            File.Exists(mainPath) &&
            File.Exists(pagePath) &&
            File.Exists(storePath),
            "MNV operator-feedback source unavailable");

        string main =
            File.ReadAllText(
                mainPath);

        string page =
            File.ReadAllText(
                pagePath);

        string store =
            File.ReadAllText(
                storePath);

        Check(
            main.IndexOf(
                "\"COMPUTE REQUESTED - WAITING FOR ENGINE PLAN\"",
                StringComparison.Ordinal) >= 0,
            "COMPUTE action does not publish immediate feedback");

        Check(
            main.IndexOf(
                "bool uploadAccepted =",
                StringComparison.Ordinal) >= 0 &&
            main.IndexOf(
                "ManeuverOperatorFeedbackStore.Publish",
                StringComparison.Ordinal) >= 0,
            "UPLOAD MNV still discards receiver result");

        Check(
            page.IndexOf(
                "ManeuverOperatorFeedbackStore.TryGetLatest",
                StringComparison.Ordinal) >= 0 &&
            page.IndexOf(
                "operatorFeedback.Text",
                StringComparison.Ordinal) >= 0,
            "MNV page does not surface action feedback");

        Check(
            store.IndexOf(
                "TimeSpan.FromSeconds(8.0)",
                StringComparison.Ordinal) >= 0,
            "operator feedback is not bounded / short-lived");
    }


'@

    $tests =
        $tests.Replace(
            $methodAnchor,
            $method + $methodAnchor)
}

WriteText $testPath $tests

Write-Host "Applied KMC 14.22.75a MNV Action Feedback Fix."
Write-Host "COMPUTE and UPLOAD MNV now expose their action/result in the MNV footer."
Write-Host "No maneuver planning math or uplink protocol changed."
