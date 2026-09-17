from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"

def test_vessel_target_position_uses_canonical_orbit_frame():
    text = SENDER.read_text(encoding="utf-8")
    assert "CanonicalPositionAtTrueAnomaly(targetOrbit, targetOrbit.trueAnomaly)" in text
    assert "targetOrbit.getRelativePositionAtUT(ut)" not in text

def test_maneuver_node_position_uses_canonical_orbit_frame():
    text = SENDER.read_text(encoding="utf-8")
    assert "double nodeTrueAnomaly = TrueAnomalyAtUT(active.orbit, node.UT);" in text
    assert "CanonicalPositionAtTrueAnomaly(active.orbit, nodeTrueAnomaly)" in text
    assert "active.orbit.getRelativePositionAtUT(node.UT)" not in text
    assert "getTrueAnomalyAtUT" not in text

def test_packet_contract_remains_unchanged():
    packet = (ROOT / "KMC.shared" / "OrbitMapPacket.cs").read_text(encoding="utf-8")
    assert 'ProtocolId = "KMC-ORBITMAP1"' in packet
