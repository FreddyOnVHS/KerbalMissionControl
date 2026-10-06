from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_temporary_lambert_test_ui_is_removed():
    assert "_createLambertTestNodeButton" not in MAP
    assert "CREATE LAMBERT TEST NODE" not in MAP
    assert "UploadLambertTestNode" not in MAP
    assert "BuildLambertTestNodePacket" not in MAP
    assert "MAP-LAMBERT-TEST-" not in MAP


def test_single_create_button_remains():
    assert '"CREATE KSP NODE"' in MAP
    assert "_createTransferNodeButton" in MAP


def test_production_lambert_authority_is_unchanged():
    assert "BuildProductionNodePacket" in MAP
    assert "lambertEjection.BurnUniversalTimeSeconds" in MAP
    assert "lambertEjection.ProgradeDeltaVMetersPerSecond" in MAP
    assert "lambertEjection.NormalDeltaVMetersPerSecond" in MAP
    assert "lambertEjection.RadialDeltaVMetersPerSecond" in MAP
    assert "_submittedTransferUsedLambertAuthority" in MAP


def test_legacy_fallback_remains_but_is_silent_when_lambert_exists():
    assert '"LEGACY FALLBACK"' in MAP
    assert "_parkingAwareLambertPreview == null" in MAP
    builder_start = MAP.index("private static ManeuverUplinkPacket BuildProductionNodePacket")
    builder_end = MAP.index("private void UploadTransferNode()", builder_start)
    builder = MAP[builder_start:builder_end]
    assert "legacyCandidate.NodeUniversalTimeSeconds" in builder
    assert "legacyCandidate.ProgradeDeltaVMetersPerSecond" in builder
    assert "NormalDeltaVMetersPerSecond = 0.0" in builder
    assert "RadialDeltaVMetersPerSecond = 0.0" in builder


def test_labels_are_production_facing():
    assert '"LAMBERT SEARCH  COARSE 9x9"' in MAP
    assert '"LAMBERT EJECTION / PRODUCTION"' in MAP
    assert '"FINITE-SOI MATCH"' in MAP
    assert "NO NODE AUTHORITY" not in MAP


def test_font_safe_layout_is_preserved():
    assert "transferFontHeight" in MAP
    assert "transferContentBottom" in MAP
    assert "separatorPen" in MAP
