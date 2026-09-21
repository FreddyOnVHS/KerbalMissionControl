from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_probe_logs_canonical_local_and_body():
    assert "canonicalLocal=" in TEXT
    assert "canonicalBody=" in TEXT
    assert "canonicalParent=" in TEXT

def test_probe_compares_same_ut_true_anomaly():
    assert "patch.TrueAnomalyAtT(patch.getObtAtUT(ut))" in TEXT
    assert "patch.referenceBody.orbit.TrueAnomalyAtT(patch.referenceBody.orbit.getObtAtUT(ut))" in TEXT

def test_probe_logs_frame_angles():
    assert "angleLocalDeg=" in TEXT
    assert "angleBodyDeg=" in TEXT
    assert "angleParentDeg=" in TEXT

def test_vector_angle_helper_present():
    assert "private static double VectorAngleDegrees" in TEXT
