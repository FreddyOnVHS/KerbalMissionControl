from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_ejection_uses_origin_body_mu_and_current_parking_orbit():
    assert "originBody.GravParameter" in MAP_PAGE
    assert "packet.ActiveOrbit" in MAP_PAGE
    assert "parkingOrbit.SemiMajorAxisMeters" in MAP_PAGE
    assert "packet.ReferenceBodyRadiusMeters" in MAP_PAGE


def test_ejection_rejects_non_circular_or_high_inclination_parking_orbits():
    assert "parkingOrbit.Eccentricity > 0.05" in MAP_PAGE
    assert "normalizedInclination > 10.0" in MAP_PAGE
    assert "NEAR-CIRCULAR PROGRADE ORBIT" in MAP_PAGE


def test_vinf_is_parent_frame_hohmann_delta_v_magnitude():
    assert "Math.Abs(transfer.ParentFrameDeltaVMetersPerSecond)" in MAP_PAGE
    assert 'solution.ExcessDirection = transfer.ParentFrameDeltaVMetersPerSecond >= 0.0' in MAP_PAGE


def test_hyperbolic_injection_speed_uses_energy_equation():
    assert "Math.Sqrt((vinf * vinf) + (2.0 * mu / parkingRadius))" in MAP_PAGE
    assert "ejectionDeltaV = hyperbolicPeriapsisSpeed - parkingSpeed" in MAP_PAGE
    assert "Math.Sqrt(mu / parkingRadius)" in MAP_PAGE


def test_asymptote_turn_geometry_is_calculated():
    assert "hyperbolicEccentricity = 1.0 +" in MAP_PAGE
    assert "Math.Acos(-1.0 / hyperbolicEccentricity)" in MAP_PAGE
    assert "BEHIND VINF" in MAP_PAGE


def test_candidate_burn_pass_is_selected_near_transfer_window():
    assert "targetBurnRadiusDirection = NormalizeRadians(parentTangentDirection - asymptoteAngle)" in MAP_PAGE
    assert "vesselMeanMotion - originMeanMotion" in MAP_PAGE
    assert "Math.Abs(backwardSeconds) < Math.Abs(forwardSeconds)" in MAP_PAGE
    assert "solution.BurnUniversalTimeSeconds = transfer.DepartureUniversalTimeSeconds + windowOffset" in MAP_PAGE


def test_display_exposes_spacecraft_burn_solution_but_does_not_create_node():
    assert '"VESSEL DV      +"' in MAP_PAGE
    assert '"BURN UT        "' in MAP_PAGE
    assert '"SOLUTION ONLY - NO MANEUVER NODE CREATED"' in MAP_PAGE
    assert "CreateManeuver" not in MAP_PAGE
    assert "AddManeuver" not in MAP_PAGE


def test_existing_transfer_window_and_local_map_remain_present():
    assert "TryCalculateTransferWindow" in MAP_PAGE
    assert "_renderer.Draw(context, _viewport, scene, _camera, freshness);" in MAP_PAGE
