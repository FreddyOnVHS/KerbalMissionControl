from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_package_readme_declares_plugin_dll_and_runtime_acceptance():
    readme=(ROOT/'README_14.22.1.txt').read_text()
    assert 'KSP Plugin DLL Required? YES' in readme
    assert 'RUNTIME ACCEPTANCE' in readme
    assert 'GEOMETRY REBUILD' in readme.upper()
    assert 'KSP MAP VIEW' in readme.upper()
def test_patcher_targets_only_14_22_1_files():
    patcher=(ROOT/'Tools/OrbitMap/apply_14_22_1.py').read_text()
    assert '14.22.1' in patcher
    assert 'KMC.shared/OrbitMapPacket.cs' in patcher
    assert 'KMC.Plugin/OrbitMapTelemetrySender.cs' in patcher
    assert 'KMC.MissionControl/Pages/MapPage.cs' in patcher
