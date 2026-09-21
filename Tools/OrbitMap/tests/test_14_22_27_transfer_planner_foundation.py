from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PLUGIN = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")


def test_map_has_local_and_transfer_subpages():
    assert 'DrawTab(context, _localTab, "LOCAL", _subpage == 0)' in MAP_PAGE
    assert 'DrawTab(context, _transferTab, "TRANSFER", _subpage == 1)' in MAP_PAGE
    assert 'if (_subpage == 1)' in MAP_PAGE


def test_transfer_page_uses_ksp_packet_body_catalog_not_hardcoded_stock_names():
    assert 'for (int i = 0; i < packet.Bodies.Count; i++)' in MAP_PAGE
    assert 'SELECT DESTINATION' in MAP_PAGE
    assert '_selectedTransferBodyName' in MAP_PAGE
    for hardcoded in ['"Duna"', '"Eve"', '"Jool"', '"Eeloo"']:
        assert hardcoded not in MAP_PAGE


def test_plugin_catalog_comes_from_flightglobals_bodies():
    assert 'for (int i = 0; i < FlightGlobals.Bodies.Count; i++)' in PLUGIN
    assert 'CelestialBody candidate = FlightGlobals.Bodies[i];' in PLUGIN
    assert 'candidate.referenceBody != primary' not in PLUGIN


def test_catalog_preserves_local_primary_origin_and_allows_nonlocal_orbits():
    assert 'bool isPrimary = candidate == primary;' in PLUGIN
    assert 'body.Orbit = BuildOrbit(candidate.orbit, position);' in PLUGIN
    assert 'body.PositionX = isPrimary ? 0.0 : position.x;' in PLUGIN


def test_transfer_foundation_displays_real_orbital_metadata():
    assert 'destination.Orbit.SemiMajorAxisMeters' in MAP_PAGE
    assert 'destination.Orbit.Eccentricity' in MAP_PAGE
    assert 'destination.Orbit.InclinationDegrees' in MAP_PAGE
    assert 'destination.Orbit.PeriodSeconds' in MAP_PAGE


def test_142227_does_not_create_or_calculate_maneuver_yet():
    assert 'CALCULATION NOT ENABLED IN 14.22.27' in MAP_PAGE
    assert 'CreateManeuver' not in MAP_PAGE
    assert 'AddManeuver' not in MAP_PAGE


def test_existing_local_map_and_projected_geometry_paths_are_preserved():
    assert '_renderer.Draw(context, _viewport, scene, _camera, freshness);' in MAP_PAGE
    assert 'BuildAuthoritativePatchSamples(patch, item, packet.ReferenceBodyName, ref remainingAuthoritativeSamples);' in PLUGIN
    assert 'canonicalParent = canonicalLocal + canonicalBody;' in PLUGIN


def test_body_catalog_change_does_not_change_telemetry_rate_or_patch_protocol():
    assert 'private const float SendIntervalSeconds = 0.2f;' in PLUGIN
    assert 'OrbitMapPacket.MaxPatches' in PLUGIN
    assert 'OrbitMapPacket.MaxTotalPatchSamples' in PLUGIN
