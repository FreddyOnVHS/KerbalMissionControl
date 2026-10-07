from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")


def test_bplane_init_preserves_encounter_feasibility():
    start = SOLVER.index("TargetBPlaneBootstrapPlanner.TryCreateEjection")
    end = SOLVER.index("PASS 2 -- TERMINAL GEOMETRY", start)
    block = SOLVER[start:end]
    assert "bPlaneAssessment.PredictedEncounter" in block


def test_preterminal_incumbent_is_saved_and_restored_if_needed():
    assert "encounterIncumbentEjection" in SOLVER
    assert "encounterIncumbentAssessment" in SOLVER
    marker = SOLVER.index("The B-plane state is an optimizer initialization")
    fallback = SOLVER[marker:]
    assert "encounterIncumbentEjection" in fallback
    assert "encounterIncumbentAssessment" in fallback
    assert "IsBetter(" in fallback
