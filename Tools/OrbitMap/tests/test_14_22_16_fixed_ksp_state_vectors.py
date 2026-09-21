from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_authoritative_samples_use_ksp_true_anomaly_at_ut():
    assert "orbit.TrueAnomalyAtT(orbit.getObtAtUT(ut))" in TEXT

def test_authoritative_samples_disable_future_rotation():
    assert "GetOrbitalStateVectorsAtTrueAnomaly(trueAnomaly, ut, false" in TEXT

def test_authoritative_samples_convert_with_planetarium_zup():
    assert "Planetarium.Zup.WorldToLocal(position)" in TEXT

def test_fitted_frame_calibration_removed():
    assert "BuildKspToCanonicalFrame" not in TEXT
    assert "KspToCanonicalFrame" not in TEXT
