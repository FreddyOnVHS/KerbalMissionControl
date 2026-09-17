from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CAMERA = ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapCamera.cs'
PAGE = ROOT / 'KMC.MissionControl' / 'Pages' / 'MapPage.cs'
CONIC = ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapConicSampler.cs'
SENDER = ROOT / 'KMC.Plugin' / 'OrbitMapTelemetrySender.cs'


def test_reset_is_az_360_el_zero():
    text = CAMERA.read_text(encoding='utf-8')
    assert '_yawRadians = 0.0;' in text
    assert '_pitchRadians = 0.0;' in text
    assert 'return normalized < 1e-9 ? 360.0 : normalized;' in text


def test_map_buttons_have_deliberate_horizontal_spacing():
    text = PAGE.read_text(encoding='utf-8')
    assert 'const int ButtonGap = 16;' in text
    assert '_fitOrbitButton = new Rectangle(_resetButton.Right + effectiveGap' in text
    assert '_fitManeuverButton = new Rectangle(_fitOrbitButton.Right + effectiveGap' in text
    assert '_targetButton = new Rectangle(_fitManeuverButton.Right + effectiveGap' in text


def test_projected_closed_patch_is_sampled_by_patch_time_bounds():
    text = CONIC.read_text(encoding='utf-8')
    start = text.index('public static OrbitMapVector3[] SamplePatch')
    end = text.index('public static OrbitMapVector3 PositionAtTrueAnomaly', start)
    body = text[start:end]
    assert 'patch.StartUniversalTimeSeconds' in body
    assert 'patch.EndUniversalTimeSeconds' in body
    assert 'PositionAtUniversalTime(patch.Orbit, ut)' in body
    assert 'return SampleClosedOrbit(patch.Orbit, sampleCount);' not in body


def test_sender_derives_safe_elliptic_period_without_orbit_period_getter():
    text = SENDER.read_text(encoding='utf-8')
    start = text.index('private static OrbitMapOrbit BuildOrbit')
    end = text.index('private static Vector3d CanonicalPositionAtTrueAnomaly', start)
    body = text[start:end]
    assert 'orbit.period' not in body
    assert 'orbit.referenceBody.gravParameter' in body
    assert '2.0 * Math.PI * Math.Sqrt' in body
