from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
CAMERA = ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapCamera.cs"
RENDERER = ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapRenderer.cs"

def test_camera_exposes_view_angles_and_body_occlusion():
    text = CAMERA.read_text(encoding="utf-8")
    assert "public double YawDegrees" in text
    assert "public double PitchDegrees" in text
    assert "IsOccludedBySphere" in text

def test_renderer_skips_body_occluded_orbit_segments_and_markers():
    text = RENDERER.read_text(encoding="utf-8")
    assert "camera.IsOccludedBySphere(midpoint, bodyRadiusMeters)" in text
    assert "camera.IsOccludedBySphere(world, bodyRadiusMeters)" in text

def test_renderer_draws_camera_orientation_indicator():
    text = RENDERER.read_text(encoding="utf-8")
    assert "DrawViewIndicator" in text
    assert "VIEW AZ" in text
    assert "EL" in text
    assert "ProjectDirection" in text

def test_reset_uses_operator_requested_zero_elevation_view():
    text = CAMERA.read_text(encoding="utf-8")
    assert "_yawRadians = 0.0;" in text
    assert "_pitchRadians = 0.0;" in text
