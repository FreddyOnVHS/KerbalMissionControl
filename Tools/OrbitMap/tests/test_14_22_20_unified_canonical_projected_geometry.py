from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_primary_projected_patch_uses_canonical_position():
    assert "Vector3d canonicalLocal = CanonicalPositionAtTrueAnomaly(patch, patchTrueAnomaly);" in TEXT
    assert "Vector3d canonicalParent = canonicalLocal;" in TEXT

def test_child_projected_patch_adds_canonical_body_position():
    assert "canonicalParent += canonicalBody;" in TEXT
    assert "canonicalBody = CanonicalPositionAtTrueAnomaly(referenceBody.orbit, bodyTrueAnomaly);" in TEXT

def test_transmitted_projected_points_are_canonical():
    assert "sample.PositionX = canonicalParent.x;" in TEXT
    assert "sample.PositionY = canonicalParent.y;" in TEXT
    assert "sample.PositionZ = canonicalParent.z;" in TEXT

def test_temporary_rotation_workaround_is_removed():
    assert "SignedAngleAroundAxis" not in TEXT
    assert "RotateAboutAxis" not in TEXT
    assert "frameRotationRadians" not in TEXT
