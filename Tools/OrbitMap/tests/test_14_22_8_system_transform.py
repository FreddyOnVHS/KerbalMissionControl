from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
TRANSFORM=ROOT/'KMC.MissionControl'/'Rendering'/'OrbitMap'/'OrbitMapSystemTransform.cs'
CSPROJ=ROOT/'KMC.MissionControl'/'KMC.MissionControl.csproj'

def test_system_transform_exists_and_translates_child_patch():
    t=TRANSFORM.read_text(encoding='utf-8')
    assert 'BodyPositionAtUniversalTime' in t
    assert 'Translate' in t
    assert 'SamplePatchInPrimaryFrame' in t
    assert 'OrbitMapConicSampler.SamplePatch' in t

def test_project_includes_system_transform():
    assert 'OrbitMapSystemTransform.cs' in CSPROJ.read_text(encoding='utf-8')
