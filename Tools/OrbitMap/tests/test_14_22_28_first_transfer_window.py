from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_same_parent_transfer_window_is_calculated_from_live_body_orbits():
    assert "TryCalculateTransferWindow(originBody, destination, packet.UniversalTimeSeconds" in MAP_PAGE
    assert "origin.Orbit.SemiMajorAxisMeters" in MAP_PAGE
    assert "destination.Orbit.SemiMajorAxisMeters" in MAP_PAGE
    assert "origin.ParentName, destination.ParentName" in MAP_PAGE


def test_parent_mu_is_derived_from_ksp_orbital_period_not_hardcoded():
    assert "DeriveParentGravParameter" in MAP_PAGE
    assert "orbit.PeriodSeconds * orbit.PeriodSeconds" in MAP_PAGE
    assert "Kerbol" not in MAP_PAGE
    assert "1.1723328" not in MAP_PAGE


def test_hohmann_transfer_time_and_required_phase_are_present():
    assert "transferSemiMajorAxis = 0.5 * (r1 + r2)" in MAP_PAGE
    assert "transferTime = Math.PI * Math.Sqrt" in MAP_PAGE
    assert "requiredPhase = NormalizeRadians(Math.PI - (destinationMeanMotion * transferTime))" in MAP_PAGE


def test_current_phase_is_propagated_from_orbit_elements_at_packet_ut():
    assert "OrbitalLongitudeAtUniversalTime(origin.Orbit, currentUniversalTimeSeconds, parentMu)" in MAP_PAGE
    assert "orbit.MeanAnomalyAtEpochRadians" in MAP_PAGE
    assert "universalTimeSeconds - orbit.EpochUniversalTimeSeconds" in MAP_PAGE
    assert "SolveEllipticEccentricAnomaly" in MAP_PAGE


def test_window_wait_uses_relative_angular_rate():
    assert "relativeRate = destinationMeanMotion - originMeanMotion" in MAP_PAGE
    assert "NormalizeRadians(requiredPhase - currentPhase) / relativeRate" in MAP_PAGE
    assert "NormalizeRadians(currentPhase - requiredPhase) / (-relativeRate)" in MAP_PAGE


def test_display_is_explicit_about_approximation_and_no_maneuver_creation():
    assert "HOHMANN WINDOW  CIRCULAR / COPLANAR APPROX" in MAP_PAGE
    assert "WINDOW ONLY - NO MANEUVER NODE CREATED" in MAP_PAGE
    assert "PARENT DV" in MAP_PAGE
    assert "CreateManeuver" not in MAP_PAGE
    assert "AddManeuver" not in MAP_PAGE


def test_hierarchy_change_route_is_rejected_for_now():
    assert "REQUIRES HIERARCHY-CHANGE PLANNING" in MAP_PAGE
    assert "!string.Equals(origin.ParentName, destination.ParentName" in MAP_PAGE


def test_existing_local_map_path_remains_present():
    assert "_renderer.Draw(context, _viewport, scene, _camera, freshness);" in MAP_PAGE
    assert 'DrawTab(context, _localTab, "LOCAL", _subpage == 0)' in MAP_PAGE
    assert 'DrawTab(context, _transferTab, "TRANSFER", _subpage == 1)' in MAP_PAGE
