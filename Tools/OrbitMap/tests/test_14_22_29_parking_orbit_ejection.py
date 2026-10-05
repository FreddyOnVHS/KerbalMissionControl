from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLANNER = (ROOT / "KMC.Engine" / "Navigation" / "HohmannTransferPlanner.cs").read_text(encoding="utf-8")
ADAPTER = (ROOT / "KMC.MissionControl" / "Navigation" / "OrbitMapNavigationAdapter.cs").read_text(encoding="utf-8")
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_ejection_uses_origin_body_mu_and_current_parking_orbit():
    assert "originBody.GravParameter" in PLANNER
    assert "packet.ActiveOrbit" in ADAPTER
    assert "parkingOrbit.SemiMajorAxisMeters" in PLANNER
    assert "packet.ReferenceBodyRadiusMeters" in ADAPTER


def test_ejection_rejects_non_circular_or_high_inclination_parking_orbits():
    assert "parkingOrbit.Eccentricity > 0.05" in PLANNER
    assert "normalizedInclination > 10.0" in PLANNER
    assert "NEAR-CIRCULAR PROGRADE ORBIT" in MAP_PAGE


def test_vinf_is_parent_frame_hohmann_delta_v_magnitude():
    assert "Math.Abs(transfer.ParentFrameDeltaVMetersPerSecond)" in PLANNER
    assert 'solution.ExcessDirection = transfer.ParentFrameDeltaVMetersPerSecond >= 0.0' in PLANNER


def test_hyperbolic_injection_speed_uses_energy_equation():
    assert "Math.Sqrt((vinf * vinf) + (2.0 * mu / parkingRadius))" in PLANNER
    assert "ejectionDeltaV = hyperbolicPeriapsisSpeed - parkingSpeed" in PLANNER
    assert "Math.Sqrt(mu / parkingRadius)" in PLANNER


def test_asymptote_turn_geometry_is_calculated():
    assert "hyperbolicEccentricity = 1.0 +" in PLANNER
    assert "Math.Acos(-1.0 / hyperbolicEccentricity)" in PLANNER
    assert "BEHIND VINF" in MAP_PAGE


def test_candidate_burn_pass_is_selected_near_transfer_window():
    assert "targetBurnRadiusDirection = NormalizeRadians(parentTangentDirection - asymptoteAngle)" in PLANNER
    assert "vesselMeanMotion - originMeanMotion" in PLANNER
    assert "Math.Abs(backwardSeconds) < Math.Abs(forwardSeconds)" in PLANNER
    assert "solution.BurnUniversalTimeSeconds = transfer.DepartureUniversalTimeSeconds + windowOffset" in PLANNER


def test_display_exposes_spacecraft_burn_solution_but_does_not_create_node():
    assert '"VESSEL DV      +"' in MAP_PAGE
    assert '"BURN UT        "' in MAP_PAGE
    assert '"SOLUTION ONLY - NO MANEUVER NODE CREATED"' in MAP_PAGE
    assert "CreateManeuver" not in MAP_PAGE
    assert "AddManeuver" not in MAP_PAGE


def test_existing_transfer_window_and_local_map_remain_present():
    assert "TryCalculateTransferWindow" in MAP_PAGE
    assert "_renderer.Draw(context, _viewport, scene, _camera, freshness);" in MAP_PAGE
