from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
R=ROOT/'KMC.MissionControl'/'Rendering'/'OrbitMap'/'OrbitMapRenderer.cs'

def test_renderer_draws_child_orbits_and_bodies_before_vessel():
    t=R.read_text(encoding='utf-8')
    assert 'DrawChildBodyOrbits' in t
    assert 'DrawChildBodies' in t
    assert 'scene.ChildBodies' in t

def test_encounter_panel_can_show_child_patch_periapsis():
    t=R.read_text(encoding='utf-8')
    assert 'p.Orbit.PeriapsisMeters' in t
