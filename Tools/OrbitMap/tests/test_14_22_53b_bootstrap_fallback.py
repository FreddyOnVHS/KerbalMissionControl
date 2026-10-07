from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SEARCH = (ROOT / "KMC.Engine" / "Navigation" / "ParkingOrbitAwareLambertSearch.cs").read_text(encoding="utf-8")

def test_tracks_feasible_and_bootstrap_separately():
    assert "bestFeasible" in SEARCH
    assert "bestBootstrap" in SEARCH
    assert "bestFeasible ?? bestBootstrap" in SEARCH

def test_infeasible_bootstrap_is_ranked_by_dv_first():
    block = SEARCH[SEARCH.index("private static bool IsLowerDepartureDv"):SEARCH.index("private static bool PreferDeterministicTieBreak")]
    assert block.index("EjectionScoreMetersPerSecond") < block.index("NormalizedStateError")

def test_feasible_candidate_keeps_dv_as_objective():
    block = SEARCH[SEARCH.index("private static bool IsBetterFeasible"):SEARCH.index("private static bool IsLowerDepartureDv")]
    assert "FiniteSoiCandidateSelectionPolicy.IsFeasible" in block
    assert "IsLowerDepartureDv" in block
