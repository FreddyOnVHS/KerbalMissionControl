from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]

def test_expected_14_22_8_files_exist():
    expected=['KMC.shared/OrbitMapPacket.cs','KMC.Plugin/OrbitMapTelemetrySender.cs','KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs','KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs','KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs','README_14.22.8.txt']
    for rel in expected:
        assert (ROOT/rel).exists(), rel
