from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PACKET = ROOT / 'KMC.shared' / 'OrbitMapPacket.cs'


def test_hyperbolic_orbit_allows_missing_apoapsis_in_protocol():
    text = PACKET.read_text(encoding='utf-8')
    assert 'FO(orbit.ApoapsisMeters)' in text
    assert 'TryOptionalFinite(f[8], out d)' in text
