from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"

def test_active_vessel_position_uses_same_canonical_orbit_frame_as_mission_control():
    text = SENDER.read_text(encoding="utf-8")
    assert "CanonicalPositionAtTrueAnomaly(vessel.orbit, vessel.orbit.trueAnomaly)" in text
    assert "BuildOrbit(vessel.orbit, vessel.orbit.getRelativePositionAtUT(ut))" not in text

def test_plugin_canonical_transform_matches_mission_control_conic_transform():
    text = SENDER.read_text(encoding="utf-8")
    for token in [
        "double p = a * (1.0 - e * e);",
        "double r = p / denom;",
        "double x = r * Math.Cos(trueAnomalyRadians);",
        "double y = r * Math.Sin(trueAnomalyRadians);",
        "double w = orbit.argumentOfPeriapsis * Math.PI / 180.0;",
        "double inc = orbit.inclination * Math.PI / 180.0;",
        "double lan = orbit.LAN * Math.PI / 180.0;",
        "return new Vector3d(co * x2 - so * y2, so * x2 + co * y2, z2);",
    ]:
        assert token in text

def test_fix_is_vsl_only_and_does_not_change_packet_contract():
    packet = (ROOT / "KMC.shared" / "OrbitMapPacket.cs").read_text(encoding="utf-8")
    assert 'ProtocolId = "KMC-ORBITMAP1"' in packet
