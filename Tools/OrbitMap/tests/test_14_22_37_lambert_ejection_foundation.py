from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

ENGINE = ROOT / "KMC.Engine"
PLANNER = (ENGINE / "Navigation" / "LambertParkingOrbitEjectionPlanner.cs").read_text(encoding="utf-8")
SOLUTION = (ENGINE / "Navigation" / "LambertParkingOrbitEjectionSolution.cs").read_text(encoding="utf-8")
PROJECT = (ENGINE / "KMC.Engine.csproj").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_lambert_ejection_is_compiled_into_engine():
    assert r'Navigation\LambertParkingOrbitEjectionPlanner.cs' in PROJECT
    assert r'Navigation\LambertParkingOrbitEjectionSolution.cs' in PROJECT


def test_ejection_uses_full_departure_excess_vector():
    assert "Vector3d departureExcessVelocity" in PLANNER
    assert "departureExcessVelocity.Magnitude" in PLANNER
    assert "vinfDirection" in PLANNER
    assert "projectedVinf" in PLANNER


def test_ejection_outputs_full_local_node_vector():
    assert "ProgradeDeltaVMetersPerSecond" in SOLUTION
    assert "NormalDeltaVMetersPerSecond" in SOLUTION
    assert "RadialDeltaVMetersPerSecond" in SOLUTION
    assert "TotalDeltaVMetersPerSecond" in SOLUTION


def test_ejection_is_ksp_and_ui_independent():
    combined = PLANNER + SOLUTION
    assert "UnityEngine" not in combined
    assert "OrbitMap" not in combined
    assert "ManeuverUplink" not in combined
    for body in ["Kerbin", "Duna", "Eve", "Jool"]:
        assert body not in combined


def test_runtime_map_still_has_no_lambert_ejection_authority():
    assert "LambertParkingOrbitEjectionPlanner" not in MAP
    assert "LambertParkingOrbitEjectionSolution" not in MAP


def test_geometry_is_explicitly_preview_scoped():
    assert "MaximumParkingEccentricity = 0.05" in PLANNER
    assert "GeometryResidualDegrees" in SOLUTION
