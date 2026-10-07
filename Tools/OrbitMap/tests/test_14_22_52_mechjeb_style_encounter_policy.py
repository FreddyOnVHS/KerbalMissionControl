from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
POLICY = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiOptimizationPolicy.cs").read_text(encoding="utf-8")
BPLANE = (ROOT / "KMC.Engine" / "Navigation" / "TargetBPlanePlanner.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PROGRAM = (ROOT / "Tools" / "NavigationTests" / "Program.cs").read_text(encoding="utf-8")


def test_solver_is_explicitly_two_pass():
    assert "OptimizationStage.Encounter" in SOLVER
    assert "OptimizationStage.Periapsis" in SOLVER
    assert "PASS 1 -- FEASIBILITY" in SOLVER
    assert "PASS 2 -- TERMINAL GEOMETRY" in SOLVER


def test_departure_dv_is_bounded_from_seed():
    assert "MaximumDeltaVRatioFromSeed = 1.50" in POLICY
    assert "IsWithinDepartureTrustRegion" in SOLVER
    assert "runaway encounter DV accepted" in PROGRAM


def test_bplane_geometry_is_live_in_assessment():
    assert "TargetBPlanePlanner.TryCalculate" in SOLVER
    assert "TargetBPlaneErrorFractionOfSoi" in SOLVER
    assert "impact parameter" in BPLANE


def test_map_exposes_dv_guard():
    assert '"DV SEED "' in MAP
    assert '" m/s  CAP "' in MAP
