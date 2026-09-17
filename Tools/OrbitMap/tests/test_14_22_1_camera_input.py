from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_mission_display_routes_pointer_input_only_to_opt_in_pages():
    display=(ROOT/'KMC.MissionControl/Controls/MissionDisplay.cs').read_text()
    for x in ['IMissionPagePointerInput','TryClientToVirtual','OnMouseDown','OnMouseMove','OnMouseWheel','RequestRender']: assert x in display
def test_camera_is_independent_of_scene_cache():
    text=(ROOT/'KMC.MissionControl/Rendering/OrbitMap/OrbitMapCamera.cs').read_text()
    assert 'OrbitMapSceneCache' not in text
    for x in ['Rotate','Zoom','Reset','Fit','TryProject']: assert x in text
