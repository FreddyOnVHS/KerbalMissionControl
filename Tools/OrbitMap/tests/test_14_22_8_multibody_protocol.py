from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
PACKET=ROOT/'KMC.shared'/'OrbitMapPacket.cs'

def test_packet_exposes_bounded_body_collection():
    t=PACKET.read_text(encoding='utf-8')
    assert 'public const int MaxBodies = 16;' in t
    assert 'public List<OrbitMapBody> Bodies' in t
    assert 'public sealed class OrbitMapBody' in t

def test_body_protocol_serializes_and_rejects_duplicates():
    t=PACKET.read_text(encoding='utf-8')
    assert 'SerializeBody' in t and 'TryParseBody' in t
    assert 'HashSet<string> bodyNames' in t
    assert 'StringComparer.OrdinalIgnoreCase' in t

def test_body_contains_parent_orbit_and_authoritative_position():
    t=PACKET.read_text(encoding='utf-8')
    for token in ['ParentName','RadiusMeters','SoiRadiusMeters','GravParameter','public OrbitMapOrbit Orbit','PositionX','PositionY','PositionZ']:
        assert token in t
