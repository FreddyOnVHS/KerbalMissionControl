from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
CACHE=ROOT/'KMC.MissionControl'/'Rendering'/'OrbitMap'/'OrbitMapSceneCache.cs'

def test_scene_snapshot_contains_child_bodies():
    t=CACHE.read_text(encoding='utf-8')
    assert 'public List<OrbitMapSceneBody> ChildBodies' in t
    assert 'public sealed class OrbitMapSceneBody' in t
    assert 'OrbitPoints' in t

def test_scene_cache_transforms_child_referenced_patches():
    t=CACHE.read_text(encoding='utf-8')
    assert 'FindBody(packet.Bodies, patch.Orbit.ReferenceBodyName)' in t
    assert 'OrbitMapSystemTransform.AuthoritativePatchPoints' in t
    assert 'string.Equals(patch.Orbit.ReferenceBodyName, packet.ReferenceBodyName' in t

def test_body_metadata_participates_in_geometry_fingerprint():
    t=CACHE.read_text(encoding='utf-8')
    assert 'b.Append("|B|")' in t
