from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_normal_create_prefers_lambert_authority():
    assert "BuildProductionNodePacket" in MAP
    assert "lambertEjection.BurnUniversalTimeSeconds" in MAP
    assert "lambertEjection.ProgradeDeltaVMetersPerSecond" in MAP
    assert "lambertEjection.NormalDeltaVMetersPerSecond" in MAP
    assert "lambertEjection.RadialDeltaVMetersPerSecond" in MAP
    assert "_submittedTransferUsedLambertAuthority" in MAP


def test_legacy_handoff_remains_available_as_fallback():
    start = MAP.index("private static ManeuverUplinkPacket BuildProductionNodePacket")
    end = MAP.index("private void UploadTransferNode()", start)
    builder = MAP[start:end]
    assert "if (legacyCandidate == null)" in builder
    assert "legacyCandidate.NodeUniversalTimeSeconds" in builder
    assert "legacyCandidate.ProgradeDeltaVMetersPerSecond" in builder
    assert "NormalDeltaVMetersPerSecond = 0.0" in builder
    assert "RadialDeltaVMetersPerSecond = 0.0" in builder


def test_lambert_nodes_cannot_use_legacy_refiner():
    assert '"LAMBERT AUTHORITY - NO REFINE"' in MAP
    refine = MAP[MAP.index("private void RefineTransferNode()"):]
    assert "_submittedTransferUsedLambertAuthority" in refine
    assert '"LAMBERT NODE REFINEMENT DISABLED"' in refine


def test_test_button_remains_for_side_by_side_validation():
    assert '"CREATE LAMBERT TEST NODE"' in MAP
    assert "UploadLambertTestNode()" in MAP


def test_finite_soi_source_is_presented_as_production():
    assert '"FINITE-SOI MATCH / PRODUCTION SOURCE"' in MAP


def test_font_safe_layout_is_preserved():
    assert "transferFontHeight" in MAP
    assert "transferContentBottom" in MAP
    assert "separatorPen" in MAP
