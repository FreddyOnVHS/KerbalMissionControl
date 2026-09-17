from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_renderer_draws_cached_scene_without_orbit_propagation():
    renderer=(ROOT/'KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs').read_text()
    assert 'OrbitMapConicSampler' not in renderer
    for x in ['TryProject','CURRENT ORBIT','TARGET','MANEUVER','ENCOUNTER']: assert x in renderer
def test_map_page_exposes_live_stale_unavailable_and_view_controls():
    page=(ROOT/'KMC.MissionControl/Pages/MapPage.cs').read_text()
    assert 'Name { get { return "ORBIT MAP"; } }' in page
    for x in ['RESET VIEW','FIT ORBIT','FIT MANEUVER']: assert x in page
    renderer=(ROOT/'KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs').read_text()
    assert 'ORBIT DATA UNAVAILABLE' in renderer
