from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_clearance_diagnostic_helper_exists():
    assert "private static void LogEncounterClearanceDiagnostics" in TEXT

def test_clearance_logs_body_radius_and_expected_periapsis():
    assert "bodyRadius=" in TEXT
    assert "expectedPeRadius=" in TEXT

def test_clearance_logs_min_ksp_and_transmitted_distance():
    assert "minKspLocalRadius=" in TEXT
    assert "minTxRadius=" in TEXT

def test_clearance_logs_surface_clearance_and_ut():
    assert "clearanceAboveSurface=" in TEXT
    assert "closestUT=" in TEXT
