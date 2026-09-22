from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
SENDER = (ROOT / "KMC.MissionControl" / "Transport" / "TransferPlannerManeuverUplink.cs").read_text(encoding="utf-8")
PLUGIN_PATH = ROOT / "KMC.Plugin" / "ManeuverUplinkReceiver.cs"
PLUGIN = PLUGIN_PATH.read_text(encoding="utf-8") if PLUGIN_PATH.exists() else ""


def test_transfer_page_exposes_deliberate_create_node_button():
    assert '"CREATE KSP NODE"' in MAP_PAGE
    assert "_createTransferNodeButton.Contains(q)" in MAP_PAGE
    assert "UploadTransferNode();" in MAP_PAGE


def test_node_candidate_uses_142229_burn_ut_and_prograde_spacecraft_dv():
    assert "NodeUniversalTimeSeconds = ejection.BurnUniversalTimeSeconds" in MAP_PAGE
    assert "ProgradeDeltaVMetersPerSecond = ejection.EjectionDeltaVMetersPerSecond" in MAP_PAGE
    assert "NormalDeltaVMetersPerSecond = 0.0" in MAP_PAGE
    assert "RadialDeltaVMetersPerSecond = 0.0" in MAP_PAGE


def test_sender_reuses_existing_maneuver_command_protocol_only():
    assert "ManeuverUplinkPacket.CommandPort" in SENDER
    assert "packet.Serialize()" in SENDER
    assert "AckPort" not in SENDER
    assert "NodeStatePort" not in SENDER


def test_sender_publishes_pending_status_for_existing_ack_receiver():
    assert "ManeuverUplinkStatusStore.PublishPending" in SENDER
    assert "ManeuverUplinkStatusStore.PublishRejected" in SENDER
    assert "ManeuverUplinkStatusStore.GetForPlan" in MAP_PAGE


def test_each_click_gets_unique_plan_identity():
    assert '"MAP-XFER-"' in MAP_PAGE
    assert "Guid.NewGuid()" in MAP_PAGE


def test_existing_plugin_remains_authoritative_for_stock_node_creation():
    if not PLUGIN:
        return
    assert ".AddManeuverNode(" in PLUGIN
    assert "node.DeltaV =" in PLUGIN
    assert ".UpdateFlightPlan();" in PLUGIN
    assert '"NODE VERIFIED"' in PLUGIN


def test_ui_states_ksp_authority_and_keeps_approximation_visible():
    assert "NODE CANDIDATE READY - KSP WILL BE AUTHORITATIVE" in MAP_PAGE
    assert "PARKING EJECTION  CIRCULAR / PROGRADE APPROX" in MAP_PAGE


def test_local_map_and_transfer_math_remain_present():
    assert "_renderer.Draw(context, _viewport, scene, _camera, freshness);" in MAP_PAGE
    assert "TryCalculateTransferWindow" in MAP_PAGE
    assert "TryCalculateParkingOrbitEjection" in MAP_PAGE
