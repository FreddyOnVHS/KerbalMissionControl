from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_bplane_bootstrap_is_terminal_initialization_not_optional_candidate():
    start = SOLVER.index("TargetBPlaneBootstrapPlanner.TryCreateEjection")
    end = SOLVER.index("PASS 2 -- TERMINAL GEOMETRY", start)
    block = SOLVER[start:end]

    assert "bestEjection =" in block
    assert "bestAssessment =" in block
    assert "bPlaneBootstrapApplied =" in block
    assert "IsBetter(" not in block


def test_bplane_initialization_still_respects_dv_trust_region():
    start = SOLVER.index("TargetBPlaneBootstrapPlanner.TryCreateEjection")
    end = SOLVER.index("PASS 2 -- TERMINAL GEOMETRY", start)
    block = SOLVER[start:end]

    assert "IsWithinDepartureTrustRegion" in block


def test_map_reports_bplane_initialization():
    assert '"  BPLANE INIT"' in MAP
