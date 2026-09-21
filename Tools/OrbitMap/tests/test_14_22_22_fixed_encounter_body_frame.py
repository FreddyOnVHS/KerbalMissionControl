from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
TEXT = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_child_patch_has_fixed_encounter_anchor():
    assert "Vector3d encounterBodyAnchor" in TEXT
    assert "double encounterAnchorUt" in TEXT

def test_encounter_anchor_is_computed_once_before_sample_loop():
    anchor = TEXT.index("Vector3d encounterBodyAnchor")
    loop = TEXT.index("for (int i = 0; i < sampleCount; i++)", anchor)
    assert anchor < loop

def test_child_parent_points_use_fixed_anchor():
    assert "canonicalParent = canonicalLocal + encounterBodyAnchor;" in TEXT

def test_reference_body_position_is_fixed_anchor():
    assert "canonicalBody = encounterBodyAnchor;" in TEXT
