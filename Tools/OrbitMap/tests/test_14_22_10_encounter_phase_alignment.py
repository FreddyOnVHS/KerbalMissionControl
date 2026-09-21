from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TRANSFORM = (ROOT / 'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs').read_text(encoding='utf-8')
CACHE = (ROOT / 'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs').read_text(encoding='utf-8')


def test_future_body_position_is_anchored_to_current_authoritative_body_position():
    assert 'BodyPositionAtUniversalTime(OrbitMapBody body, double stateUniversalTimeSeconds, double targetUniversalTimeSeconds)' in TRANSFORM
    assert 'TrueAnomalyFromCanonicalPosition(body.Orbit, current)' in TRANSFORM
    assert 'targetUniversalTimeSeconds - stateUniversalTimeSeconds' in TRANSFORM


def test_cross_soi_patch_translation_uses_packet_ut_as_body_phase_anchor():
    assert 'SamplePatchInPrimaryFrame(OrbitMapPatch patch, OrbitMapBody referenceBody, double stateUniversalTimeSeconds, int sampleCount)' in TRANSFORM
    assert 'BodyPositionAtUniversalTime(referenceBody, stateUniversalTimeSeconds, ut)' in TRANSFORM
    assert 'OrbitMapSystemTransform.AuthoritativePatchPoints(patch)' in CACHE


def test_encounter_body_marker_uses_same_phase_anchor_as_patch_translation():
    assert 'OrbitMapSystemTransform.AuthoritativeReferenceBodyPosition(patch)' in CACHE


def test_current_child_marker_uses_authoritative_transmitted_position_directly():
    assert 'sceneBody.Position = new OrbitMapVector3(body.PositionX, body.PositionY, body.PositionZ);' in CACHE
