from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text()


def test_terminal_stage_follows_split_feasibility():
    assert "if (splitFeasible)" in OPT
    assert "TryEvaluateTerminalConstraints" in OPT
    assert "IsTerminalFeasible" in OPT


def test_terminal_constraint_remains_coupled_not_coordinate_search():
    assert "TerminalResidualCount" in OPT
    assert "midpoint X/Y/Z" in OPT
    assert "for (double" not in OPT.lower()


def test_terminal_keeps_body_scaled_tolerance():
    assert "DesiredPeriapsisRadiusMeters * 0.002" in OPT
    assert "Math.Max(1000.0" in OPT


def test_shadow_authority_remains_disabled():
    map_text = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text()
    assert "NO NODE AUTHORITY" in map_text
