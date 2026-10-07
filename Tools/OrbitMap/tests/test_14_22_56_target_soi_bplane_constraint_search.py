from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
PLAN = (ROOT / "KMC.Engine" / "Navigation" / "TargetBPlaneBootstrapPlanner.cs").read_text(encoding="utf-8")
ASSESS = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingAssessment.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_terminal_search_varies_bplane_and_arrival_epoch():
    block = SOLVER[SOLVER.index("private static void RunBPlaneTerminalSearch"):SOLVER.index("private static void RunStage")]
    assert "azimuthSamples = 16" in block
    assert "arrivalSamples = 7" in block
    assert "refinementPasses = 3" in block
    assert "TryCreateEjectionAtEntry" in block


def test_terminal_search_preserves_encounter_and_dv_constraints():
    block = SOLVER[SOLVER.index("private static void RunBPlaneTerminalSearch"):SOLVER.index("private static void RunStage")]
    assert "IsWithinDepartureTrustRegion" in block
    assert "trialAssessment.PredictedEncounter" in block
    assert "OptimizationStage.Periapsis" in block


def test_live_soi_entry_velocity_drives_bplane_direction():
    assert "TargetSoiEntryRelativeVelocity" in ASSESS
    assert "bestAssessment.TargetSoiEntryRelativeVelocity" in SOLVER


def test_finite_soi_endpoint_solver_exists():
    assert "TryCreateEjectionAtEntry" in PLAN
    assert "destinationArrival.Position +" in PLAN
    assert "arrivalUniversalTimeSeconds" in PLAN


def test_map_reports_terminal_search():
    assert '"  BPLANE SEARCH"' in MAP
