from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")


def test_coupled_solver_keeps_finite_difference_jacobian_and_damped_solve():
    assert "double[,] jacobian" in OPT
    assert "FiniteDifferenceStep" in OPT
    assert "TrySolveDampedNormalEquations" in OPT
    assert "trustRadius" in OPT
    assert "MaximumIterations" in OPT


def test_residual_vector_still_includes_physical_target_miss():
    assert "targetPositionResidual.X / targetTolerance" in OPT
    assert "targetPositionResidual.Y / targetTolerance" in OPT
    assert "targetPositionResidual.Z / targetTolerance" in OPT


def test_shadow_solver_keeps_departure_dv_as_tie_break_objective():
    assert "ScoreTieTolerance" in OPT
    assert "candidate.Candidate.TotalDeltaVMetersPerSecond" in OPT
    assert "current.Candidate.TotalDeltaVMetersPerSecond" in OPT


def test_stage1_does_not_touch_production_shooting_solver():
    assert "TargetSoiShootingSolver" not in OPT
