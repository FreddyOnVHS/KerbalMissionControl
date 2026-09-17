from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_main_form_enables_real_map_page_and_receiver():
    text=(ROOT/'KMC.MissionControl/MainForm.cs').read_text()
    assert '"MAP",\n                _mapPage' in text
    assert 'OrbitMapTelemetryReceiver' in text
    assert '_orbitMapReceiver.Start()' in text
    assert '_orbitMapReceiver.Dispose()' in text
def test_map_redraw_is_requested_only_when_map_is_active():
    text=(ROOT/'KMC.MissionControl/MainForm.cs').read_text()
    assert '_mapPageActive' in text
    assert 'if (_mapPageActive' in text
    assert '_missionDisplay.RequestRender()' in text
