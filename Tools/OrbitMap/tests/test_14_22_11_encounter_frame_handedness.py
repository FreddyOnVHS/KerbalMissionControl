from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TRANSFORM = (ROOT / 'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs').read_text(encoding='utf-8')
CACHE = (ROOT / 'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs').read_text(encoding='utf-8')


def test_cross_soi_transform_preserves_continuity_without_geometric_mirroring():
    assert 'SamplePatchInPrimaryFrameAligned' in TRANSFORM
    assert 'continuityAnchor' in TRANSFORM
    assert 'MirrorAcrossPeriapsisAxis' not in TRANSFORM
    assert 'DistanceSquared' in TRANSFORM


def test_alignment_compares_soi_entry_branches_to_previous_patch_end():
    assert 'positiveDistance = DistanceSquared(positive[0], continuityAnchor)' in TRANSFORM
    assert 'negativeDistance = DistanceSquared(negative[0], continuityAnchor)' in TRANSFORM
    assert 'positiveDistance < negativeDistance ? positive : negative' in TRANSFORM


def test_scene_cache_carries_previous_patch_endpoint_into_child_patch_transform():
    assert 'OrbitMapVector3 previousPatchEnd' in CACHE
    assert 'bool havePreviousPatchEnd' in CACHE
    assert 'OrbitMapSystemTransform.AuthoritativePatchPoints(patch)' in CACHE
    assert 'previousPatchEnd = authoritative[authoritative.Length - 1]' in CACHE


def test_parent_patch_endpoint_is_recorded_before_child_encounter_patch():
    assert 'previousPatchEnd = sampled[sampled.Length - 1]' in CACHE
