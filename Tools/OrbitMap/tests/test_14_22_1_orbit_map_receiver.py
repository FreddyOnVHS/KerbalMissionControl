from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
def test_receiver_store_contract_has_live_stale_unavailable_windows():
    store=(ROOT/'KMC.MissionControl/Telemetry/OrbitMapSnapshotStore.cs').read_text()
    assert 'TimeSpan.FromSeconds(1.0)' in store
    assert 'TimeSpan.FromSeconds(4.0)' in store
    for x in ['OrbitMapFreshness.Live','OrbitMapFreshness.Stale','OrbitMapFreshness.Unavailable']: assert x in store
def test_receiver_rejects_bad_packets_without_clearing_store():
    receiver=(ROOT/'KMC.MissionControl/Telemetry/OrbitMapTelemetryReceiver.cs').read_text()
    assert 'OrbitMapPacket.TryParse' in receiver
    assert 'OrbitMapSnapshotStore.SetLatest' in receiver
    assert 'OrbitMapSnapshotStore.RecordRejected' in receiver
