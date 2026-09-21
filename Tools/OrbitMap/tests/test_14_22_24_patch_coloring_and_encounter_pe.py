from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CACHE = (ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapSceneCache.cs").read_text(encoding="utf-8")
RENDERER = (ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapRenderer.cs").read_text(encoding="utf-8")

def test_scene_tracks_patch_render_kinds():
    assert "PatchRenderKinds" in CACHE
    assert "PrimaryInitial" in CACHE
    assert "Encounter" in CACHE
    assert "PrimaryLater" in CACHE

def test_first_primary_is_orange_later_primary_is_green():
    assert "primaryPassCount == 0 ? OrbitMapPatchRenderKind.PrimaryInitial : OrbitMapPatchRenderKind.PrimaryLater" in CACHE
    assert "case OrbitMapPatchRenderKind.PrimaryLater" in RENDERER
    assert "greenPatchColor" in RENDERER

def test_child_patch_has_distinct_encounter_color():
    assert "OrbitMapPatchRenderKind.Encounter" in CACHE
    assert "encounterPatchColor" in RENDERER

def test_encounter_panel_uses_child_patch_periapsis():
    assert "FindEncounterPatch(s)" in RENDERER
    assert "p.Orbit.PeriapsisMeters" in RENDERER
    assert 'string text = "SOI: " + p.Orbit.ReferenceBodyName + "\\nENCOUNTER";' in RENDERER
