from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"

def _build_orbit_body():
    text = SENDER.read_text(encoding="utf-8")
    start = text.index("private static OrbitMapOrbit BuildOrbit")
    end = text.index("private static OrbitMapTarget BuildTarget", start)
    return text[start:end]

def test_build_orbit_does_not_call_ksp_apsis_or_period_convenience_getters():
    body = _build_orbit_body()
    assert "orbit.ApA" not in body
    assert "orbit.PeA" not in body
    assert "orbit.period" not in body

def test_build_orbit_derives_apsides_from_elements_and_reference_body_radius():
    body = _build_orbit_body()
    assert "orbit.semiMajorAxis * (1.0 + orbit.eccentricity)" in body
    assert "orbit.semiMajorAxis * (1.0 - orbit.eccentricity)" in body
    assert "referenceRadius" in body

def test_future_patch_build_still_avoids_ksp_position_propagation():
    text = SENDER.read_text(encoding="utf-8")
    start = text.index("private static void BuildPatches")
    end = text.index("public void OnDestroy", start)
    body = text[start:end]
    assert "getRelativePositionAtUT" not in body
