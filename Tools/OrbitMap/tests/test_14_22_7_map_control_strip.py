from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PAGE = ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs"

def _page():
    return PAGE.read_text(encoding="utf-8")

def test_map_buttons_are_measured_from_text_with_padding():
    text = _page()
    assert "MeasureButtonWidth(context.Graphics, context.SmallFont, \"RESET VIEW\")" in text
    assert "MeasureButtonWidth(context.Graphics, context.SmallFont, \"FIT ORBIT\")" in text
    assert "MeasureButtonWidth(context.Graphics, context.SmallFont, \"FIT MANEUVER\")" in text
    assert "MeasureButtonWidth(context.Graphics, context.SmallFont, \"TARGET\")" in text
    assert "ButtonHorizontalPadding" in text

def test_map_buttons_use_consistent_non_overlapping_gap():
    text = _page()
    assert "const int ButtonGap = 16;" in text
    assert "_fitOrbitButton = new Rectangle(_resetButton.Right + effectiveGap" in text
    assert "_fitManeuverButton = new Rectangle(_fitOrbitButton.Right + effectiveGap" in text
    assert "_targetButton = new Rectangle(_fitManeuverButton.Right + effectiveGap" in text

def test_button_text_is_centered_inside_each_rectangle():
    text = _page()
    assert "format.Alignment = StringAlignment.Center;" in text
    assert "format.LineAlignment = StringAlignment.Center;" in text

def test_control_strip_guards_against_viewport_overflow():
    text = _page()
    assert "availableWidth" in text
    assert "totalWidth" in text
    assert "effectiveGap" in text
