from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKET = (ROOT / 'KMC.shared' / 'OrbitMapPacket.cs').read_text(encoding='utf-8')
SENDER = (ROOT / 'KMC.Plugin' / 'OrbitMapTelemetrySender.cs').read_text(encoding='utf-8')
CACHE = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapSceneCache.cs').read_text(encoding='utf-8')
TRANSFORM = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapSystemTransform.cs').read_text(encoding='utf-8')


def test_protocol_carries_bounded_authoritative_patch_samples_and_body_centers():
    assert 'MaxPatchSamples' in PACKET
    assert 'MaxTotalPatchSamples' in PACKET
    assert 'List<OrbitMapPatchSample>' in PACKET
    assert 'ReferenceBodyPositionX' in PACKET
    assert 'ReferenceBodyPositionY' in PACKET
    assert 'ReferenceBodyPositionZ' in PACKET
    assert 'SerializePatchSample' in PACKET
    assert 'TryParsePatchSample' in PACKET


def test_plugin_samples_ksp_child_patch_and_body_directly_in_primary_frame():
    assert 'BuildAuthoritativePatchSamples' in SENDER
    assert 'patch.getRelativePositionAtUT' in SENDER
    assert 'referenceBody.orbit.getRelativePositionAtUT' in SENDER
    assert 'primaryReferenced' in SENDER
    assert '.xzy' in SENDER
    assert 'OrbitMapPacket.MaxPatchSamples' in SENDER
    assert 'OrbitMapPacket.MaxTotalPatchSamples' in SENDER
    assert 'frame.Transform(localKsp + bodyCenterKsp)' in SENDER


def test_mission_control_prefers_authoritative_primary_frame_points_for_child_patch():
    assert 'patch.Samples.Count > 1' in CACHE
    assert 'patch.Samples.Count > 1' in CACHE
    assert 'AuthoritativePatchPoints' in CACHE
    assert 'AuthoritativeReferenceBodyPosition' in CACHE


def test_authoritative_points_are_not_reconstructed_from_future_patch_elements():
    assert 'AuthoritativePatchPoints' in TRANSFORM
    assert 'sample.PositionX' in TRANSFORM
    assert 'sample.ReferenceBodyPositionX' in TRANSFORM
