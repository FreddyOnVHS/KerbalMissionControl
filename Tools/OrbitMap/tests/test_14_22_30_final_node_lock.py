from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
CSPROJ = (ROOT / "KMC.MissionControl" / "KMC.MissionControl.csproj").read_text(encoding="utf-8")
SENDER = (ROOT / "KMC.MissionControl" / "Transport" / "TransferPlannerManeuverUplink.cs").read_text(encoding="utf-8")


def test_legacy_project_compiles_transfer_sender():
    assert '<Compile Include="Transport\\TransferPlannerManeuverUplink.cs" />' in CSPROJ


def test_submitted_candidate_identity_is_recorded_after_successful_send():
    assert "if (sent)" in MAP_PAGE
    assert "_submittedTransferDestinationName =" in MAP_PAGE
    assert "_submittedTransferNodeUt =" in MAP_PAGE
    assert "_submittedTransferProgradeDv =" in MAP_PAGE


def test_same_candidate_is_locked_against_duplicate_clicks():
    assert "IsSameAsSubmittedTransferCandidate()" in MAP_PAGE
    assert "candidateLocked" in MAP_PAGE
    assert "_createTransferNodeButton = Rectangle.Empty;" in MAP_PAGE
    assert '"NODE UPLINKED"' in MAP_PAGE
    assert '"NODE CREATED"' in MAP_PAGE


def test_verified_state_renders_node_created_not_create_button():
    assert 'string.Equals(state, "NODE VERIFIED"' in MAP_PAGE
    assert '? "NODE CREATED"' in MAP_PAGE


def test_materially_new_solution_can_unlock():
    assert "<= 120.0" in MAP_PAGE
    assert "<= 5.0" in MAP_PAGE
    assert '"NEW SOLUTION READY"' in MAP_PAGE


def test_destination_change_resets_submitted_identity():
    assert "_submittedTransferDestinationName = string.Empty;" in MAP_PAGE
    assert "_submittedTransferNodeUt = double.NaN;" in MAP_PAGE
    assert "_submittedTransferProgradeDv = double.NaN;" in MAP_PAGE


def test_sender_still_uses_existing_maneuver_protocol():
    assert "ManeuverUplinkPacket.CommandPort" in SENDER
    assert "ManeuverUplinkStatusStore.PublishPending" in SENDER
    assert "AckPort" not in SENDER
    assert "NodeStatePort" not in SENDER


def test_node_creation_remains_explicit_and_ksp_authoritative():
    assert '"CREATE KSP NODE"' in MAP_PAGE
    assert "NODE CANDIDATE READY - KSP WILL BE AUTHORITATIVE" in MAP_PAGE
