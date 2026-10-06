from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
TESTS = (ROOT / "Tools" / "NavigationTests" / "Program.cs").read_text(encoding="utf-8")


def test_missing_target_mu_does_not_kill_encounter_solver():
    assert "FinitePositive(destinationBody.GravParameter)" in SOLVER
    assert "TryFindTargetSoiEntry(" in SOLVER
    # Periapsis shaping must be conditional, not a mandatory return-false gate.
    block = SOLVER[SOLVER.index("if (predictedEncounter &&"):SOLVER.index("assessment =", SOLVER.index("if (predictedEncounter &&"))]
    assert "return false;" not in block


def test_safe_periapsis_fixture_supplies_target_mu():
    assert "destination.GravParameter = 2.5e8;" in TESTS
