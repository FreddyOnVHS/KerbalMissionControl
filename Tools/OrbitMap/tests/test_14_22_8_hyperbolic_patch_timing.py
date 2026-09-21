from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SAMPLER = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapConicSampler.cs').read_text(encoding='utf-8')
TRANSFORM = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapSystemTransform.cs').read_text(encoding='utf-8')
SCENE = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapSceneCache.cs').read_text(encoding='utf-8')


def test_patch_sampler_has_gravity_aware_time_bounded_overload():
    assert 'SamplePatch(OrbitMapPatch patch, int sampleCount, double gravParameter)' in SAMPLER
    assert 'endUt > startUt' in SAMPLER
    assert 'PositionAtUniversalTime(patch.Orbit, ut, gravParameter)' in SAMPLER


def test_universal_time_propagation_supports_hyperbolic_orbits():
    assert 'PositionAtUniversalTime(OrbitMapOrbit orbit, double universalTimeSeconds, double gravParameter)' in SAMPLER
    assert 'SolveHyperbolicAnomaly' in SAMPLER
    assert 'Math.Tanh' in SAMPLER


def test_child_patch_transform_uses_same_gravity_aware_timed_sampling():
    assert 'OrbitMapConicSampler.SamplePatch(patch, sampleCount, referenceBody.GravParameter)' in TRANSFORM


def test_primary_patch_sampling_resolves_primary_gravity_parameter():
    assert 'FindBody(packet.Bodies, packet.ReferenceBodyName)' in SCENE
    assert 'primaryBody.GravParameter' in SCENE
    assert 'OrbitMapConicSampler.SamplePatch(patch, Math.Min(256, 192), primaryBody != null ? primaryBody.GravParameter : 0.0)' in SCENE
