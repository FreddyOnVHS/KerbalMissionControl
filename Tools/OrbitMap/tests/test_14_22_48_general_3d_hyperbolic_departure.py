from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLANNER = (ROOT / "KMC.Engine" / "Navigation" / "LambertParkingOrbitEjectionPlanner.cs").read_text(encoding="utf-8")
GENERAL = (ROOT / "KMC.Engine" / "Navigation" / "General3dHyperbolicDeparturePlanner.cs").read_text(encoding="utf-8")
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
TESTS = (ROOT / "Tools" / "NavigationTests" / "LambertEjectionProgram.cs").read_text(encoding="utf-8")


def test_general_3d_planner_is_primary():
    assert "General3dHyperbolicDeparturePlanner.TryCalculate" in PLANNER
    assert "Primary path: solve the true non-coplanar" in PLANNER


def test_general_solver_does_not_project_vinf_into_parking_plane():
    assert "projectedVinf" not in GENERAL
    assert "targetVInfinity" in GENERAL
    assert "RotateAroundAxis" in GENERAL


def test_normal_only_asymptote_is_supported():
    assert "NormalOnlyAsymptote" in TESTS
    assert "new Vector3d(0.0, 0.0, 1.0)" in TESTS


def test_engine_compiles_general_planner():
    assert r'Navigation\\General3dHyperbolicDeparturePlanner.cs' in PROJECT


def test_runtime_planner_contains_no_planet_names():
    for name in ["Moho", "Eve", "Duna", "Dres", "Jool", "Eeloo"]:
        assert name not in GENERAL
