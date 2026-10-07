from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiShadowResult.cs").read_text(encoding="utf-8")


def test_active_api_uses_production_names_and_retains_compatibility():
    assert "public class CoupledFiniteSoiResult" in RESULT
    assert "class CoupledFiniteSoiShadowResult : CoupledFiniteSoiResult" in RESULT
    assert "public static bool TrySolve(" in OPT
    assert "public static bool TrySolveShadow(" in OPT
    assert "private CoupledFiniteSoiResult _coupledFiniteSoiResult" in MAP
    assert "MapCoupledFiniteSoiAdapter.TrySolve" in MAP
    assert "_coupledFiniteSoiShadow" not in MAP


def test_production_diagnostics_show_terminal_target_and_authority_source():
    assert '"TARGET PE " +' in MAP
    assert '"  PRED PE " +' in MAP
    assert '"  AUTH COUPLED"' in MAP
    assert '"  AUTH FALLBACK"' in MAP
    assert '"COUPLED FINITE-SOI OPTIMIZER / CANDIDATE"' in MAP


def test_proven_fallback_chain_is_still_present():
    assert "GetProductionCoupledEjection" in MAP
    assert "GetProductionLambertEjection" in MAP
    assert "coupledEjection ?? lambertEjection" in MAP
    assert '"LEGACY FALLBACK UPLINK SENT"' in MAP
    assert '"LAMBERT AUTHORITY UPLINK SENT"' in MAP
