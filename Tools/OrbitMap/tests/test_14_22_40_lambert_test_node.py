from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_separate_lambert_test_button_exists():
    assert "_createLambertTestNodeButton" in MAP
    assert '"CREATE LAMBERT TEST NODE"' in MAP
    assert "UploadLambertTestNode()" in MAP


def test_lambert_test_packet_uses_full_parking_aware_vector():
    assert "BuildLambertTestNodePacket" in MAP
    assert "ejection.BurnUniversalTimeSeconds" in MAP
    assert "ejection.ProgradeDeltaVMetersPerSecond" in MAP
    assert "ejection.NormalDeltaVMetersPerSecond" in MAP
    assert "ejection.RadialDeltaVMetersPerSecond" in MAP
    assert '"MAP-LAMBERT-TEST-"' in MAP


def test_normal_create_path_remains_legacy_zero_normal_radial():
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    upload = upload.split("private static ManeuverUplinkPacket BuildLambertTestNodePacket", 1)[0]
    assert "_transferNodeCandidate.NodeUniversalTimeSeconds" in upload
    assert "_transferNodeCandidate.ProgradeDeltaVMetersPerSecond" in upload
    assert "NormalDeltaVMetersPerSecond = 0.0" in upload
    assert "RadialDeltaVMetersPerSecond = 0.0" in upload


def test_lambert_test_refinement_is_disabled():
    assert "_submittedTransferWasLambertTest" in MAP
    assert '"LAMBERT TEST - NO REFINE"' in MAP


def test_no_plugin_or_protocol_change_required():
    assert "Operation = \"CREATE\"" in MAP
