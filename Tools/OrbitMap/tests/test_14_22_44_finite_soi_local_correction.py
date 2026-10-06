from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
OPT = (ROOT / "KMC.Engine" / "Navigation" / "FiniteSoiDepartureOptimizer.cs").read_text(encoding="utf-8")


def test_optimizer_is_compiled():
    assert r'Navigation\\FiniteSoiDepartureOptimizer.cs' in PROJECT
    assert r'Navigation\\FiniteSoiDepartureCorrectionResult.cs' in PROJECT


def test_optimizer_moves_only_generic_local_burn_variables():
    assert "BurnUniversalTimeSeconds" in OPT
    assert "ProgradeDeltaVMetersPerSecond" in OPT
    assert "NormalDeltaVMetersPerSecond" in OPT
    assert "RadialDeltaVMetersPerSecond" in OPT
    assert "FiniteSoiDepartureEvaluator.TryEvaluate" in OPT


def test_optimizer_has_no_body_specific_logic():
    for name in ["Kerbin", "Duna", "Eve", "Moho", "Jool", "Dres", "Eeloo"]:
        assert name not in OPT


def test_map_uses_corrected_ejection_for_production():
    assert "GetProductionLambertEjection" in MAP
    assert "_finiteSoiCorrection.CorrectedEjection" in MAP
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    assert "GetProductionLambertEjection()" in upload


def test_map_exposes_before_after_correction_diagnostics():
    assert '"FINITE-SOI LOCAL CORRECTION"' in MAP
    assert "_finiteSoiCorrection.InitialAssessment.NormalizedStateError" in MAP
    assert "_finiteSoiCorrection.CorrectedAssessment" in MAP
    assert "_finiteSoiCorrection.Iterations" in MAP
    assert "_finiteSoiCorrection.Evaluations" in MAP


def test_cleanup_and_legacy_fallback_survive():
    assert "CREATE LAMBERT TEST NODE" not in MAP
    assert '"CREATE KSP NODE"' in MAP
    assert '"LEGACY FALLBACK"' in MAP
    assert "BuildProductionNodePacket" in MAP
