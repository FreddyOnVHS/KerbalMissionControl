param([string]$RepoRoot = (Get-Location).Path)
$ErrorActionPreference = "Stop"

function ReadText([string]$path) {
    if (-not (Test-Path $path)) { throw "Missing file: $path" }
    [System.IO.File]::ReadAllText($path)
}
function WriteText([string]$path,[string]$text) {
    [System.IO.File]::WriteAllText($path,$text,[System.Text.UTF8Encoding]::new($false))
}

# GUID: post-burn next action
$guidPath = Join-Path $RepoRoot "KMC.MissionControl\Pages\GuidancePage.cs"
$guid = ReadText $guidPath

if (-not $guid.Contains("private static string BuildPostBurnNextAction(")) {
    $marker = "        private static void DrawCrewCommandGroup("
    if (-not $guid.Contains($marker)) { throw "Could not locate DrawCrewCommandGroup." }
    $helper = @'
        private static string BuildPostBurnNextAction(
            GuidanceSolutionModel guidance,
            QueueDirectorState queue)
        {
            if (guidance == null || !guidance.BurnComplete)
            {
                return "---";
            }

            if (!guidance.ReacquisitionReady)
            {
                return "REMOVE COMPLETED NODE";
            }

            if (queue != null && queue.NextKmcPlanAvailable)
            {
                return
                    "ACTIVATE KMC #" +
                    Math.Max(1, queue.NextKmcSequence).ToString();
            }

            return "CLEANUP COMPLETE";
        }

'@
    $guid = $guid.Replace($marker,$helper + $marker)
}

$oldExec = @'
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
                });
'@
$newExec = @'
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
                                    : "---"),

                    new FieldPair(
                        "NEXT ACTION",
                        guidance != null &&
                        guidance.BurnComplete
                            ? BuildPostBurnNextAction(
                                guidance,
                                queue)
                            : "---")
                });
'@
if ($guid.Contains($oldExec)) {
    $guid = $guid.Replace($oldExec,$newExec)
}
elseif (-not $guid.Contains('"NEXT ACTION"')) {
    throw "Could not locate GUID execution fields."
}

if (-not $guid.Contains("bool postBurnCleanup =")) {
    $marker = @'
            bool ignitionStandby =
                guidance != null &&
'@
    if (-not $guid.Contains($marker)) { throw "Could not locate ignition state block." }
    $block = @'
            bool postBurnCleanup =
                guidance != null &&
                guidance.BurnComplete &&
                !guidance.ReacquisitionReady;

            bool postBurnNext =
                guidance != null &&
                guidance.BurnComplete &&
                guidance.ReacquisitionReady &&
                queue != null &&
                queue.NextKmcPlanAvailable;

            bool postBurnDone =
                guidance != null &&
                guidance.BurnComplete &&
                guidance.ReacquisitionReady &&
                (queue == null ||
                 !queue.NextKmcPlanAvailable);

            if (postBurnCleanup)
            {
                command = "REMOVE COMPLETED NODE";
                color = Color.Orange;
            }
            else if (postBurnNext)
            {
                command =
                    "ACTIVATE KMC #" +
                    Math.Max(1, queue.NextKmcSequence).ToString();
                color = Color.LimeGreen;
            }
            else if (postBurnDone)
            {
                command = "QUEUE COMPLETE";
                color = Color.LimeGreen;
            }

'@
    $guid = $guid.Replace($marker,$block + $marker)
}

$guid = $guid.Replace(
    "            if (ignitionStandby)`r`n            {",
    "            if (!postBurnCleanup && !postBurnNext && !postBurnDone && ignitionStandby)`r`n            {")
$guid = $guid.Replace(
    "            else if (ignitionDue)`r`n            {",
    "            else if (!postBurnCleanup && !postBurnNext && !postBurnDone && ignitionDue)`r`n            {")

WriteText $guidPath $guid

# MainForm: select completed node, contextual cleanup button/feedback
$mainPath = Join-Path $RepoRoot "KMC.MissionControl\MainForm.cs"
$main = ReadText $mainPath

$old = @'
            RefreshKmcManeuverLifecycle(
                snapshot);

            UpdateManeuverNodeActionState();
'@
$new = @'
            RefreshKmcManeuverLifecycle(
                snapshot);

            SelectCompletedActiveNodeForCleanup(
                snapshot);

            UpdateManeuverNodeActionState();
'@
if ($main.Contains($old)) { $main = $main.Replace($old,$new) }

if (-not $main.Contains("private void SelectCompletedActiveNodeForCleanup(")) {
    $marker = "        private void OnManeuverNodeSelectionChanged("
    if (-not $main.Contains($marker)) { throw "Could not locate node selection handler." }
    $helper = @'
        private void SelectCompletedActiveNodeForCleanup(
            ManeuverInventorySnapshot snapshot)
        {
            if (snapshot == null ||
                snapshot.Nodes == null ||
                _maneuverNodeSelector == null ||
                _maneuverNodeSelector.Items.Count == 0)
            {
                return;
            }

            string activePlanId =
                ManeuverPlanPromotionStore.GetActivePlanId();

            if (string.IsNullOrWhiteSpace(activePlanId) ||
                !IsManeuverBurnComplete(activePlanId))
            {
                return;
            }

            for (int index = 0;
                 index < snapshot.Nodes.Count &&
                 index < _maneuverNodeSelector.Items.Count;
                 index++)
            {
                KmcQueuedManeuverPlan retained =
                    KmcManeuverPlanStore.FindForNode(
                        snapshot.Nodes[index],
                        snapshot.VesselId);

                if (retained != null &&
                    string.Equals(
                        retained.PlanId,
                        activePlanId,
                        StringComparison.Ordinal))
                {
                    _maneuverNodeSelector.SelectedIndex = index;
                    return;
                }
            }
        }

'@
    $main = $main.Replace($marker,$helper + $marker)
}

$old = @'
            _maneuverDeleteButton.Enabled =
                selected != null;

            KmcQueuedManeuverPlan plan =
'@
$new = @'
            _maneuverDeleteButton.Enabled =
                selected != null;

            _maneuverDeleteButton.Text =
                "DELETE";

            KmcQueuedManeuverPlan plan =
'@
if ($main.Contains($old)) { $main = $main.Replace($old,$new) }

$old = @'
            bool alreadyActive =
                plan != null &&
                string.Equals(
                    plan.PlanId,
                    activePlanId,
                    StringComparison.Ordinal);
'@
$new = @'
            bool alreadyActive =
                plan != null &&
                string.Equals(
                    plan.PlanId,
                    activePlanId,
                    StringComparison.Ordinal);

            bool completedSelected =
                plan != null &&
                IsManeuverBurnComplete(
                    plan.PlanId);

            if (completedSelected)
            {
                _maneuverDeleteButton.Text =
                    "REMOVE COMPLETE NODE";
            }
'@
if ($main.Contains($old)) { $main = $main.Replace($old,$new) }

# Rebuild delete method by local substitutions
$methodStart = $main.IndexOf("        private void OnManeuverDeleteNodeClick(")
$methodEnd = $main.IndexOf("        private void OnManeuverAcknowledgmentReceived(", $methodStart)
if ($methodStart -lt 0 -or $methodEnd -lt 0) { throw "Could not locate delete method." }
$method = $main.Substring($methodStart,$methodEnd-$methodStart)

if (-not $method.Contains("bool completedCleanup =")) {
    $needle = @'
            DialogResult confirm =
                MessageBox.Show(
'@
    $insert = @'
            KmcQueuedManeuverPlan retainedPlan =
                ResolveSelectedRetainedPlan(
                    selected);

            bool completedCleanup =
                retainedPlan != null &&
                IsManeuverBurnComplete(
                    retainedPlan.PlanId);

'@
    $method = $method.Replace($needle,$insert + $needle)

    $method = $method.Replace(
        '"Delete " + selected.Text +`r`n                    " from the active KSP vessel?",`r`n                    "Delete Maneuver Node",',
        'completedCleanup`r`n                        ? "Remove completed " + selected.Text +`r`n                          " from the active KSP vessel?"`r`n                        : "Delete " + selected.Text +`r`n                          " from the active KSP vessel?",`r`n                    completedCleanup`r`n                        ? "Remove Completed Maneuver Node"`r`n                        : "Delete Maneuver Node",')

    $duplicate = @'
            KmcQueuedManeuverPlan retainedPlan =
                ResolveSelectedRetainedPlan(
                    selected);

'@
    $first = $method.IndexOf($duplicate)
    $second = $method.IndexOf($duplicate,$first+$duplicate.Length)
    if ($second -ge 0) {
        $method = $method.Remove($second,$duplicate.Length)
    }
}

$send = @'
                _maneuverQueueTransport.SendDelete(
                    snapshot.VesselId,
                    selected.NodeId);
'@
if ($method.Contains($send) -and
    -not $method.Contains("REMOVE COMMAND SENT - AWAITING NODE REMOVED")) {
    $method = $method.Replace($send,$send + @'

                ManeuverOperatorFeedbackStore.Publish(
                    completedCleanup
                        ? "REMOVE COMMAND SENT - AWAITING NODE REMOVED"
                        : "DELETE COMMAND SENT - AWAITING NODE REMOVED",
                    true);
'@)
}

$main = $main.Substring(0,$methodStart) + $method + $main.Substring($methodEnd)
WriteText $mainPath $main

# MNV footer language
$mnvPath = Join-Path $RepoRoot "KMC.MissionControl\Pages\ManeuverPage.cs"
$mnv = ReadText $mnvPath
$mnv = $mnv.Replace(
    'if (manualComplete) footer = nodeRemoved ? "MANUAL MANEUVER COMPLETE - NODE REMOVED" : "MANUAL MANEUVER COMPLETE - REMOVE NODE";',
    'if (manualComplete) footer = nodeRemoved ? (nextAvailableKmcSequence > 0 ? "CLEANUP COMPLETE - ACTIVATE KMC #" + nextAvailableKmcSequence.ToString() : "CLEANUP COMPLETE - QUEUE CLEAR") : "MANEUVER COMPLETE - REMOVE COMPLETED NODE";')
$mnv = $mnv.Replace(
    'else if (nodeRemoved) footer = "NODE REMOVED - UPLOAD REQUIRED";',
    'else if (nodeRemoved) footer = "NODE REMOVED - CLEANUP COMPLETE";')
WriteText $mnvPath $mnv

# Regression
$testPath = Join-Path $RepoRoot "Tools\NavigationTests\Program.cs"
$tests = ReadText $testPath
if (-not $tests.Contains('Run("post-burn cleanup and queue handoff are explicit"')) {
    $anchor = '        Run("manual maneuver nodes survive unavailable burn estimate", ManualManeuverBurnEstimateDecouplingRegression);'
    if (-not $tests.Contains($anchor)) { throw "Could not locate 14.22.75b2 test anchor." }
    $tests = $tests.Replace($anchor,$anchor + "`r`n" + '        Run("post-burn cleanup and queue handoff are explicit", PostBurnCleanupQueueHandoffRegression);')
}
if (-not $tests.Contains("private static void PostBurnCleanupQueueHandoffRegression()")) {
    $anchor = "    private static void ManeuverUplinkDesiredPeriapsis()"
    if (-not $tests.Contains($anchor)) { throw "Could not locate test method anchor." }
    $method = @'
    private static void PostBurnCleanupQueueHandoffRegression()
    {
        string main = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "MainForm.cs"));
        string mnv = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "Pages", "ManeuverPage.cs"));
        string guid = File.ReadAllText(
            Path.Combine(Environment.CurrentDirectory, "KMC.MissionControl", "Pages", "GuidancePage.cs"));

        Check(
            guid.IndexOf("\"NEXT ACTION\"", StringComparison.Ordinal) >= 0 &&
            guid.IndexOf("\"REMOVE COMPLETED NODE\"", StringComparison.Ordinal) >= 0 &&
            guid.IndexOf("\"CLEANUP COMPLETE\"", StringComparison.Ordinal) >= 0,
            "GUID post-burn next action missing");

        Check(
            main.IndexOf("SelectCompletedActiveNodeForCleanup", StringComparison.Ordinal) >= 0 &&
            main.IndexOf("\"REMOVE COMPLETE NODE\"", StringComparison.Ordinal) >= 0,
            "MNV completed-node cleanup focus missing");

        Check(
            main.IndexOf("\"REMOVE COMMAND SENT - AWAITING NODE REMOVED\"", StringComparison.Ordinal) >= 0,
            "completed-node removal feedback missing");

        Check(
            mnv.IndexOf("\"CLEANUP COMPLETE - ACTIVATE KMC #\"", StringComparison.Ordinal) >= 0 &&
            mnv.IndexOf("\"MANEUVER COMPLETE - REMOVE COMPLETED NODE\"", StringComparison.Ordinal) >= 0,
            "MNV cleanup/next-plan handoff language missing");

        Check(
            main.IndexOf("ManeuverPlanPromotionStore.Publish", StringComparison.Ordinal) >= 0 &&
            main.IndexOf("SendDelete", StringComparison.Ordinal) >= 0,
            "crew-controlled queue actions unexpectedly removed");
    }


'@
    $tests = $tests.Replace($anchor,$method + $anchor)
}
WriteText $testPath $tests

Write-Host "Applied KMC 14.22.76 Post-Burn Cleanup / Queue Handoff."
Write-Host "No automatic deletion or plan activation was added."
