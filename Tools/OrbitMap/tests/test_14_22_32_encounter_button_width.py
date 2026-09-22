from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_paired_buttons_are_measured_from_actual_font_text():
    assert 'MeasureButtonWidth(' in MAP
    assert '"NODE CREATED"' in MAP
    assert '"NODE UPLINKED"' in MAP
    assert '"REFINE KSP NODE"' in MAP
    assert '"ENCOUNTER ACHIEVED"' in MAP


def test_old_220px_hard_cap_is_removed():
    assert "int pairedButtonWidth = Math.Min(220" not in MAP


def test_encounter_button_gets_its_own_width():
    assert "int secondaryRequiredWidth =" in MAP
    assert "int secondaryButtonWidth =" in MAP
    assert "secondaryButtonWidth," in MAP


def test_widths_fallback_proportionally_only_when_panel_is_too_narrow():
    assert "if (primaryButtonWidth + secondaryButtonWidth > pairedAvailableWidth)" in MAP
    assert "requestedTotal" in MAP
    assert "pairedAvailableWidth -" in MAP
