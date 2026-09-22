from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

PACKET = (ROOT / "KMC.shared" / "ManeuverUplinkPacket.cs").read_text(encoding="utf-8")
PLUGIN = (ROOT / "KMC.Plugin" / "ManeuverUplinkReceiver.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_protocol_adds_backward_compatible_create_refine_operation():
    assert "public string Operation { get; set; }" in PACKET
    assert 'Operation = "CREATE";' in PACKET
    assert "fields.Length != 7 && fields.Length != 8 && fields.Length != 9" in PACKET
    assert 'Operation = fields.Length >= 9' in PACKET


def test_create_path_remains_explicit():
    assert 'Operation = "CREATE"' in MAP
    assert '"CREATE KSP NODE"' in MAP


def test_refinement_is_operator_triggered_from_transfer_page():
    assert '"REFINE KSP NODE"' in MAP
    assert "_refineTransferNodeButton.Contains(q)" in MAP
    assert "RefineTransferNode();" in MAP
    assert 'Operation = "REFINE"' in MAP


def test_refinement_reuses_same_tracked_plan_in_plugin():
    assert 'string.Equals(operation, "REFINE"' in PLUGIN
    assert '_trackedPlans.TryGetValue(packet.PlanId, out existing)' in PLUGIN
    assert "RefineTrackedManeuver(packet, vessel, existing);" in PLUGIN


def test_refinement_search_is_bounded_to_ut_and_prograde():
    assert "RefinementRounds = 6" in PLUGIN
    assert "InitialRefinementUtStepSeconds = 1800.0" in PLUGIN
    assert "InitialRefinementProgradeStepMetersPerSecond = 40.0" in PLUGIN
    assert "candidateUtOffsets" in PLUGIN
    assert "candidateProgradeOffsets" in PLUGIN
    assert "bestDeltaV.x" in PLUGIN
    assert "bestDeltaV.y" in PLUGIN


def test_every_trial_uses_ksp_patched_conic_solver_and_assessment():
    assert "tracked.Node.UT = candidateUt;" in PLUGIN
    assert "vessel.patchedConicSolver.UpdateFlightPlan();" in PLUGIN
    assert "UpdateTransferAssessment(tracked);" in PLUGIN
    assert "tracked.TargetEncounter" in PLUGIN


def test_search_keeps_best_solution_and_can_stop_on_encounter():
    assert "candidateDistance + 1.0 < bestDistance" in PLUGIN
    assert "if (bestEncounter)" in PLUGIN
    assert "tracked.Node.UT = bestUt;" in PLUGIN
    assert "tracked.Node.DeltaV = bestDeltaV;" in PLUGIN


def test_system_reauthorizes_refined_node_for_normal_verification():
    assert "tracked.PlannedNodeUt = tracked.Node.UT;" in PLUGIN
    assert "tracked.PlannedPrograde = tracked.Node.DeltaV.z;" in PLUGIN
    assert 'SendAck(packet, "NODE REFINED"' in PLUGIN
    assert "PublishTrackedNodeState(vessel, tracked);" in PLUGIN


def test_encounter_disables_further_refine_action():
    assert '"ENCOUNTER ACHIEVED"' in MAP
    assert "status.TargetEncounter" in MAP
    assert "_refineTransferNodeButton = Rectangle.Empty;" in MAP


def test_refinement_does_not_run_in_background():
    publish_block = PLUGIN.split("private void PublishTrackedNodeStates()", 1)[1]
    assert "RefineTrackedManeuver(" not in publish_block
