from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_scene_cache_has_separate_snapshot_and_geometry_counters():
    text=(ROOT/'KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs').read_text()
    assert 'GeometryRebuildCount' in text
    assert 'SnapshotUpdateCount' in text
    assert 'TrajectoryFingerprint' in text
    section=text.split('private static string TrajectoryFingerprint',1)[1]
    assert 'TimestampUtc' not in section.split('private static void AppendOrbit',1)[0]
