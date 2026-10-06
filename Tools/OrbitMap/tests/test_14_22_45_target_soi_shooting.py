from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")


def test_target_shooting_engine_is_compiled():
    assert r'Navigation\\TargetSoiShootingAssessment.cs' in PROJECT
    assert r'Navigation\\TargetSoiShootingResult.cs' in PROJECT
    assert r'Navigation\\TargetSoiShootingSolver.cs' in PROJECT


def test_shooting_objective_is_actual_target_miss():
    assert "MissDistanceMeters" in SOLVER
    assert "destinationArrival.Position" in SOLVER
    assert "actualArrival.Position" in SOLVER
    assert "PredictedEncounter" in SOLVER
    assert "destinationBody.SoiRadiusMeters" in SOLVER


def test_shooting_vars_cover_burn_and_arrival_epoch():
    assert "BurnUniversalTimeSeconds" in SOLVER
    assert "ProgradeDeltaVMetersPerSecond" in SOLVER
    assert "NormalDeltaVMetersPerSecond" in SOLVER
    assert "RadialDeltaVMetersPerSecond" in SOLVER
    assert "ArrivalUniversalTimeSeconds" in SOLVER


def test_solver_has_no_stock_body_logic():
    for name in ["Kerbin", "Duna", "Eve", "Moho", "Jool", "Dres", "Eeloo"]:
        assert name not in SOLVER


def test_production_prefers_target_shooting_solution():
    assert "_targetSoiShooting.CorrectedEjection" in MAP
    helper = MAP[MAP.index("private LambertParkingOrbitEjectionSolution GetProductionLambertEjection()"):]
    assert "_targetSoiShooting" in helper


def test_map_reports_target_prediction():
    assert '"TARGET-SOI SHOOTING"' in MAP
    assert '"PREDICT ENCOUNTER"' in MAP
    assert '"PREDICT MISS"' in MAP


def test_lambert_remains_seed_not_final_authority():
    assert "LambertSolver.TrySolve" in SOLVER
    assert "SourceLambertVelocityMismatchMetersPerSecond" in SOLVER
