from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

EXIT = (ROOT / "KMC.Engine" / "Navigation" / "HyperbolicSoiExitSolver.cs").read_text(encoding="utf-8")
TARGET = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
FINITE = (ROOT / "KMC.Engine" / "Navigation" / "FiniteSoiDepartureEvaluator.cs").read_text(encoding="utf-8")
PROGRAM = (ROOT / "Tools" / "NavigationTests" / "Program.cs").read_text(encoding="utf-8")


def test_soi_exit_is_analytic_not_segmented_recovery():
    assert "Math.Sinh" in EXIT
    assert "Math.Acos" in EXIT
    assert "StateVectorPropagator.TryPropagate" not in EXIT
    assert "MaximumRecoverySegments" not in EXIT


def test_target_shooting_prefers_analytic_source_exit():
    block = TARGET[TARGET.index("private static bool TryFindSoiExit"):]
    assert "HyperbolicSoiExitSolver.TrySolve" in block


def test_finite_soi_evaluator_prefers_same_shared_exit_solver():
    block = FINITE[FINITE.index("private static bool TryFindSoiExit"):]
    assert "HyperbolicSoiExitSolver.TrySolve" in block


def test_3d_exit_regression_is_registered():
    assert 'Run("analytic hyperbolic SOI exit handles arbitrary 3D escape", HyperbolicSoiExit);' in PROGRAM
