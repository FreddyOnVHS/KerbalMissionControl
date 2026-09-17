from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_conic_sampler_keeps_math_out_of_paint_layer():
    text=(ROOT/'KMC.MissionControl/Rendering/OrbitMap/OrbitMapConicSampler.cs').read_text()
    for x in ['SampleClosedOrbit','SamplePatch','PositionAtTrueAnomaly','Math.Cos','Math.Sin']: assert x in text
    assert 'System.Drawing' not in text
