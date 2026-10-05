from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ENGINE = ROOT / "KMC.Engine" / "CelestialMechanics"
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
SOLVER = (ENGINE / "LambertSolver.cs").read_text(encoding="utf-8")
SOLUTION = (ENGINE / "LambertSolution.cs").read_text(encoding="utf-8")
PATH = (ENGINE / "LambertTransferPath.cs").read_text(encoding="utf-8")


def test_lambert_foundation_is_compiled_by_engine():
    assert r'CelestialMechanics\LambertSolver.cs' in PROJECT
    assert r'CelestialMechanics\LambertSolution.cs' in PROJECT
    assert r'CelestialMechanics\LambertTransferPath.cs' in PROJECT


def test_solver_is_engine_only_and_ksp_independent():
    combined = SOLVER + SOLUTION + PATH
    assert "UnityEngine" not in combined
    assert "KSP" not in combined.replace("KSP/Unity", "")
    assert "ManeuverNode" not in combined
    assert "OrbitMap" not in combined


def test_zero_revolution_short_and_long_paths_are_explicit():
    assert "ShortWay" in PATH
    assert "LongWay" in PATH
    assert "zero-revolution" in SOLVER
    assert "LambertTransferPath path" in SOLVER


def test_invalid_collinear_geometry_is_rejected_instead_of_guessed():
    assert "sineMagnitude <= GeometryTolerance" in SOLVER
    assert "transfer plane" in SOLVER and "not uniquely" in SOLVER


def test_existing_map_does_not_consume_lambert_yet():
    assert "LambertSolver" not in MAP
    assert "LambertSolution" not in MAP
