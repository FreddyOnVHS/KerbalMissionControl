from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_rotation_helpers_exist():
    assert "private static double SignedAngleAroundAxis" in TEXT
    assert "private static Vector3d RotateAboutAxis" in TEXT

def test_child_samples_compare_ksp_body_to_canonical_body():
    assert "Vector3d canonicalBody = CanonicalPositionAtTrueAnomaly(referenceBody.orbit, bodyTrueAnomaly);" in TEXT
    assert "Vector3d rotationAxis = Vector3d.Cross(bodyCenter, canonicalBody);" in TEXT

def test_child_samples_apply_same_rotation_to_vessel_and_body():
    assert "parentFrame = RotateAboutAxis(parentFrame, rotationAxis, frameRotationRadians);" in TEXT
    assert "bodyCenter = RotateAboutAxis(bodyCenter, rotationAxis, frameRotationRadians);" in TEXT

def test_rotation_is_child_patch_only():
    assert "if (!primaryReferenced && referenceBody.orbit != null)" in TEXT
