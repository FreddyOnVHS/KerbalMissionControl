from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CACHE = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapSceneCache.cs').read_text(encoding='utf-8')
RENDERER = (ROOT / 'KMC.MissionControl' / 'Rendering' / 'OrbitMap' / 'OrbitMapRenderer.cs').read_text(encoding='utf-8')


def test_scene_snapshot_tracks_future_encounter_body_at_patch_start():
    assert 'public sealed class OrbitMapSceneEncounterBody' in CACHE
    assert 'public List<OrbitMapSceneEncounterBody> EncounterBodies' in CACHE
    assert 'OrbitMapSystemTransform.AuthoritativeReferenceBodyPosition(patch)' in CACHE
    assert 'scene.EncounterBodies.Add(encounterBody);' in CACHE


def test_renderer_draws_encounter_body_disk_label_and_soi_ring():
    assert 'DrawEncounterBodies(context, viewport, scene, camera, patchColor);' in RENDERER
    assert 'private static void DrawEncounterBodies' in RENDERER
    assert 'entry.Body.SoiRadiusMeters' in RENDERER
    assert 'FillEllipse' in RENDERER
    assert '" ENC"' in RENDERER


def test_encounter_body_visual_is_distinct_from_current_child_body_marker():
    assert 'Math.Max(6.0' in RENDERER
    assert 'Color.FromArgb(30, color)' in RENDERER
    assert 'new Pen(Color.FromArgb(90, color), 1.0f)' in RENDERER
