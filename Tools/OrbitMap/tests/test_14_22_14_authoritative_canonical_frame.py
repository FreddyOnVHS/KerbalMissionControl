
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SRC = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_authoritative_samples_use_state_derived_canonical_position():
    assert "CanonicalPositionAtUniversalTimeFromState" in SRC
    assert "Vector3d local = CanonicalPositionAtUniversalTimeFromState(patch, ut);" in SRC

def test_child_center_uses_same_canonical_conversion():
    assert "CanonicalPositionAtUniversalTimeFromState(referenceBody.orbit, ut)" in SRC

def test_authoritative_builder_no_longer_uses_xzy_as_render_frame():
    block = SRC.split("private static void BuildAuthoritativePatchSamples", 1)[1]
    block = block.split("private static bool IsSerializablePatchOrbit", 1)[0]
    assert ".xzy" not in block

def test_state_conversion_uses_radius_and_radial_velocity_to_choose_branch():
    assert "double radialDot =" in SRC
    assert "if (radialDot < 0.0) trueAnomaly = -trueAnomaly;" in SRC
    assert "return CanonicalPositionAtTrueAnomaly(orbit, trueAnomaly);" in SRC
