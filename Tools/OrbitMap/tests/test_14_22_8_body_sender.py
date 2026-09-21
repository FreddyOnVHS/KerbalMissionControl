from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
SENDER=ROOT/'KMC.Plugin'/'OrbitMapTelemetrySender.cs'

def test_sender_builds_primary_and_immediate_children():
    t=SENDER.read_text(encoding='utf-8')
    assert 'BuildBodies(vessel, packet, ut);' in t
    assert 'private static void BuildBodies' in t
    assert 'FlightGlobals.Bodies' in t
    assert 'candidate.referenceBody != primary' in t
    assert 'packet.Bodies.Count >= OrbitMapPacket.MaxBodies' in t

def test_child_body_position_uses_canonical_orbit_frame():
    t=SENDER.read_text(encoding='utf-8')
    assert 'CanonicalPositionAtTrueAnomaly(candidate.orbit, candidate.orbit.trueAnomaly)' in t
    assert 'body.ParentName = primary.bodyName' in t
