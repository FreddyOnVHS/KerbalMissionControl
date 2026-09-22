from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKET = (ROOT / "KMC.shared" / "ManeuverUplinkPacket.cs").read_text(encoding="utf-8")
PLUGIN = (ROOT / "KMC.Plugin" / "ManeuverUplinkReceiver.cs").read_text(encoding="utf-8")
STATUS = (ROOT / "KMC.MissionControl" / "Engineering" / "ManeuverUplinkStatusStore.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_uplink_carries_destination_body_with_backward_compatible_parse():
    assert "public string TargetBodyName" in PACKET
    assert "fields.Length != 7 && fields.Length != 8" in PACKET
    assert "TargetBodyName = fields.Length >= 8" in PACKET
    assert "TargetBodyName = destination.Name" in MAP


def test_node_state_packet_carries_structured_transfer_assessment():
    for token in [
        "TransferAssessmentAvailable",
        "TargetEncounter",
        "ClosestApproachMeters",
        "ClosestApproachUniversalTimeSeconds",
        "TargetSoiRadiusMeters",
    ]:
        assert token in PACKET
        assert token in STATUS
    assert "fields.Length != 10 && fields.Length != 16" in PACKET


def test_plugin_uses_ksp_stock_post_node_patch_chain():
    assert "tracked.Node.nextPatch" in PLUGIN
    assert "patch = patch.nextPatch" in PLUGIN
    assert "patch.getRelativePositionAtUT(ut)" in PLUGIN
    assert "target.orbit.getRelativePositionAtUT(ut)" in PLUGIN


def test_encounter_is_based_on_ksp_patch_reference_body():
    assert "if (patch.referenceBody == target)" in PLUGIN
    assert "encounteredTarget = true" in PLUGIN
    assert "tracked.TargetEncounter = encounteredTarget" in PLUGIN


def test_closest_approach_is_coarse_sampled_then_refined():
    assert "AssessmentSamplesPerPatch = 160" in PLUGIN
    assert "AssessmentRefineIterations = 24" in PLUGIN
    assert "TryFindClosestApproach" in PLUGIN
    assert "double m1 = left + (right - left) / 3.0" in PLUGIN


def test_assessment_recomputes_after_player_node_edit():
    assert "Math.Abs(actualUt - tracked.AssessedNodeUt)" in PLUGIN
    assert "Math.Abs(actualPrograde - tracked.AssessedPrograde)" in PLUGIN
    assert "UpdateTransferAssessment(tracked);" in PLUGIN


def test_transfer_page_displays_authoritative_result():
    assert '"KSP TRANSFER ASSESSMENT  "' in MAP
    assert '"ENCOUNTER       "' in MAP
    assert '"CLOSEST APPROACH "' in MAP
    assert '"TARGET SOI       "' in MAP
    assert '"OUTSIDE SOI      "' in MAP


def test_1430_duplicate_node_lock_remains_present():
    assert '"NODE CREATED"' in MAP
    assert "IsSameAsSubmittedTransferCandidate()" in MAP
    assert "_createTransferNodeButton = Rectangle.Empty;" in MAP
