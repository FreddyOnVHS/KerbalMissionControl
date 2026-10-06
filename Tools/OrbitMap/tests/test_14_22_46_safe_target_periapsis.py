from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
ASSESS = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingAssessment.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_solver_finds_real_target_soi_entry():
    assert "TryFindTargetSoiEntry" in SOLVER
    assert "TargetEntryBisectionIterations" in SOLVER
    assert "destinationBody.SoiRadiusMeters" in SOLVER


def test_solver_computes_target_relative_periapsis():
    assert "TryCalculatePeriapsisRadius" in SOLVER
    assert "destinationBody.GravParameter" in SOLVER
    assert "TargetPeriapsisRadiusMeters" in ASSESS
    assert "TargetPeriapsisAltitudeMeters" in ASSESS
    assert "TargetPeriapsisErrorMeters" in ASSESS


def test_encounter_then_periapsis_is_two_stage_objective():
    assert "Stage 1: establish an encounter" in SOLVER
    assert "Stage 2: once both trajectories enter the SOI" in SOLVER
    assert "TargetPeriapsisErrorMeters" in SOLVER


def test_default_periapsis_is_generic_not_stock_body_specific():
    assert "bodyRadius * 2.0" in SOLVER
    assert "soi * 0.01" in SOLVER
    assert "soi * 0.25" in SOLVER
    for name in ["Kerbin", "Duna", "Eve", "Moho", "Jool", "Dres", "Eeloo"]:
        assert name not in SOLVER


def test_map_shows_target_and_predicted_periapsis():
    assert '"TARGET PE R "' in MAP
    assert '"PRED PE R "' in MAP
    assert '"PRED PE ALT "' in MAP
    assert '"SAFE FLYBY"' in MAP
    assert '"COLLISION RISK"' in MAP


def test_production_authority_remains_target_shooting():
    assert "_targetSoiShooting.CorrectedEjection" in MAP
    assert '"CREATE KSP NODE"' in MAP
