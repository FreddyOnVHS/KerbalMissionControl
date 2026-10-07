from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

SEARCH = (ROOT / "KMC.Engine" / "Navigation" / "ParkingOrbitAwareLambertSearch.cs").read_text(encoding="utf-8")
POLICY = (ROOT / "KMC.Engine" / "Navigation" / "FiniteSoiCandidateSelectionPolicy.cs").read_text(encoding="utf-8")
OPT = (ROOT / "KMC.Engine" / "Navigation" / "FiniteSoiDepartureOptimizer.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_finite_soi_is_constraint_not_primary_objective():
    block = SEARCH[SEARCH.index("private static bool IsBetter"):SEARCH.index("private static bool IsValidRequest")]
    assert block.index("EjectionScoreMetersPerSecond") < block.index("NormalizedStateError")
    assert "FiniteSoiCandidateSelectionPolicy.IsFeasible" in SEARCH


def test_bootstrap_constraint_is_dimensionless_and_generic():
    assert "MaximumPositionErrorFractionOfSoi = 0.10" in POLICY
    assert "MaximumVelocityErrorFractionOfDepartureExcess = 0.10" in POLICY
    assert "Dres" not in POLICY
    assert "Kerbin" not in POLICY


def test_local_finite_soi_correction_has_dv_guard():
    assert "MaximumDeltaVRatioFromSeed = 1.35" in OPT
    assert "trial.TotalDeltaVMetersPerSecond >" in OPT


def test_map_exposes_selected_seed_feasibility():
    assert '"SOI SEED POS "' in MAP
    assert '"%  FEASIBLE / MIN-DV"' in MAP
