from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_transfer_planner_uses_compact_summary_rows():
    assert '"PHASE " + solution.CurrentPhaseDegrees' in MAP
    assert '"VINF DEP " + _lambertPreview.DepartureExcessSpeedMetersPerSecond' in MAP
    assert '"DV P/N/R "' in MAP
    assert '"POS ERR " + FormatSystemDistance' in MAP


def test_legacy_ejection_is_compact():
    assert '"LEGACY PARKING EJECTION / CREATE KSP NODE"' in MAP
    assert '"ALT " + FormatSystemDistance(ejection.ParkingAltitudeMeters)' in MAP
    assert '"BURN UT " + ejection.BurnUniversalTimeSeconds' in MAP
    assert '"PARK SPEED     "' not in MAP
    assert '"EJECT SPEED    "' not in MAP


def test_node_status_remains_bottom_anchored_with_separator():
    assert "int statusBlockBottom =" in MAP
    assert "int statusY =" in MAP
    assert "separatorY" in MAP
    assert "DrawLine(" in MAP


def test_navigation_authority_unchanged():
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    upload = upload.split("private static ManeuverUplinkPacket BuildLambertTestNodePacket", 1)[0]
    assert "NormalDeltaVMetersPerSecond = 0.0" in upload
    assert "RadialDeltaVMetersPerSecond = 0.0" in upload
    assert '"CREATE LAMBERT TEST NODE"' in MAP
