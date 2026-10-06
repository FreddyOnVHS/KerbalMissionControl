from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

ADAPTER = (ROOT / "KMC.MissionControl" / "Navigation" / "OrbitMapNavigationAdapter.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_adapter_connects_lambert_transfer_to_ejection_planner():
    assert "TryCalculateLambertParkingOrbitEjectionPreview" in ADAPTER
    assert "LambertParkingOrbitEjectionPlanner.TryCalculate" in ADAPTER
    assert "transfer.DepartureExcessVelocity" in ADAPTER
    assert "transfer.DepartureUniversalTimeSeconds" in ADAPTER


def test_map_displays_full_lambert_ejection_preview():
    assert '"LAMBERT EJECTION PREVIEW / NO NODE AUTHORITY"' in MAP
    assert "ProgradeDeltaVMetersPerSecond" in MAP
    assert "NormalDeltaVMetersPerSecond" in MAP
    assert "RadialDeltaVMetersPerSecond" in MAP
    assert "TotalDeltaVMetersPerSecond" in MAP
    assert "GeometryResidualDegrees" in MAP


def test_preview_is_cached_with_lambert_transfer():
    assert "_lambertEjectionPreview" in MAP
    assert "_lambertEjectionPreview = null;" in MAP
    assert "TryCalculateLambertParkingOrbitEjectionPreview" in MAP


def test_create_node_still_uses_legacy_candidate_only():
    assert "_transferNodeCandidate.NodeUniversalTimeSeconds" in MAP
    assert "_transferNodeCandidate.ProgradeDeltaVMetersPerSecond" in MAP
    assert "NormalDeltaVMetersPerSecond = 0.0" in MAP
    assert "RadialDeltaVMetersPerSecond = 0.0" in MAP
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    assert "_lambertEjectionPreview" not in upload.split("private void RefineTransferNode()", 1)[0]


def test_no_runtime_plugin_dependency_added():
    assert "KMC.Plugin" not in ADAPTER
    assert "ManeuverUplinkPacket" not in ADAPTER
