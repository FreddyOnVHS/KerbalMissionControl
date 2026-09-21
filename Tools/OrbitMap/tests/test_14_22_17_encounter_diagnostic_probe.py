from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_child_patch_diagnostic_probe_exists():
    assert "LogEncounterDiagnostics(patch, item, primaryBodyName, samples);" in TEXT

def test_probe_logs_raw_and_fixed_frames():
    assert "rawLocal=" in TEXT
    assert "rawBody=" in TEXT
    assert "fixedLocal=" in TEXT
    assert "fixedBody=" in TEXT
    assert "fixedParent=" in TEXT

def test_probe_logs_soi_error():
    assert "soiErr=" in TEXT
    assert "sphereOfInfluence" in TEXT

def test_probe_is_limited_to_child_referenced_patches():
    assert "if (!childReferenced) return;" in TEXT
