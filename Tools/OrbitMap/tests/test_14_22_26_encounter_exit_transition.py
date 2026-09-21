from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
RENDERER = (ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapRenderer.cs").read_text(encoding="utf-8")


def test_encounter_panel_uses_patch_end_ut_for_end_timing():
    assert "FormatPatchEndText(p, s.UniversalTimeSeconds)" in RENDERER
    assert "p.EndUniversalTimeSeconds - currentUniversalTimeSeconds" in RENDERER
    assert "double.IsNaN(p.EndUniversalTimeSeconds)" in RENDERER
    assert "double.IsInfinity(p.EndUniversalTimeSeconds)" in RENDERER


def test_actual_escape_transition_is_labeled_exit():
    assert 'string.Equals(transition, "ESCAPE", StringComparison.OrdinalIgnoreCase)' in RENDERER
    assert 'text = "EXIT " + offset;' in RENDERER


def test_non_escape_patch_end_is_not_invented_as_exit():
    assert 'text = "END " + transition.ToUpperInvariant() + " " + offset;' in RENDERER
    assert 'text = "END " + offset;' in RENDERER


def test_next_reference_body_is_shown_when_ksp_supplies_it():
    assert "p.NextBodyName" in RENDERER
    assert 'text += " -> " + nextBody;' in RENDERER


def test_existing_entry_pe_and_closest_presentation_remains():
    assert 'text += "\\nENTRY " + FormatEventOffset' in RENDERER
    assert 'text += "\\nPE " + D(p.Orbit.PeriapsisMeters);' in RENDERER
    assert 'text += "\\nCLOSEST " + FormatEventOffset' in RENDERER
