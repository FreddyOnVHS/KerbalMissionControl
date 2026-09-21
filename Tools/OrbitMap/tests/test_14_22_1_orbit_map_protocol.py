from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'KMC.shared/OrbitMapPacket.cs'
PLUGIN = ROOT / 'KMC.Plugin/KMC.Plugin.csproj'
SHARED = ROOT / 'KMC.shared/KMC.shared.csproj'

def test_orbit_map_protocol_contract_is_bounded_and_versioned():
    text = PACKET.read_text(encoding='utf-8')
    assert 'ProtocolId = "KMC-ORBITMAP1"' in text
    assert 'TelemetryPort = 5110' in text
    assert 'MaxManeuverNodes = 8' in text
    assert 'MaxPatches = 12' in text
    assert 'bool TryParse(' in text
    assert 'bool IsFinite(' in text
    assert 'ManeuverNodes.Count > MaxManeuverNodes' in text
    assert 'Patches.Count > MaxPatches' in text

def test_shared_packet_is_compiled_and_linked_into_plugin():
    plugin = PLUGIN.read_text(encoding='utf-8')
    shared = SHARED.read_text(encoding='utf-8')
    assert '..\\KMC.shared\\OrbitMapPacket.cs' in plugin
    assert 'OrbitMapPacket.cs' in shared

def test_plugin_sender_is_5hz_and_uses_ksp_authoritative_sources():
    sender = (ROOT / 'KMC.Plugin/OrbitMapTelemetrySender.cs').read_text(encoding='utf-8')
    assert 'SendIntervalSeconds = 0.2f' in sender
    assert 'FlightGlobals.ActiveVessel' in sender
    assert 'Planetarium.GetUniversalTime()' in sender
    assert 'patchedConicSolver' in sender
    assert 'maneuverNodes' in sender
    assert 'FlightGlobals.fetch.VesselTarget' in sender
    assert 'OrbitMapPacket.TelemetryPort' in sender


def test_patch_builder_does_not_propagate_ksp_orbit_at_patch_start():
    sender = (ROOT / 'KMC.Plugin/OrbitMapTelemetrySender.cs').read_text(encoding='utf-8')
    build_patches = sender.split('private static void BuildPatches', 1)[1].split('private static void BuildAuthoritativePatchSamples', 1)[0]
    assert 'getRelativePositionAtUT' not in build_patches
    assert 'new Vector3d(0.0, 0.0, 0.0)' in build_patches
    assert 'BuildAuthoritativePatchSamples' in build_patches
