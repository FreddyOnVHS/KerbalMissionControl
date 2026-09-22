from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_assessment_uses_explicit_24px_line_spacing():
    assert "const int assessmentLineSpacing = 24;" in MAP


def test_assessment_lines_use_spacing_constant():
    assert "assessmentY + assessmentLineSpacing" in MAP
    assert "assessmentY + (assessmentLineSpacing * 2)" in MAP
    assert "assessmentY + (assessmentLineSpacing * 3)" in MAP
    assert "assessmentY + (assessmentLineSpacing * 4)" in MAP


def test_transfer_assessment_content_is_unchanged():
    for token in [
        '"KSP TRANSFER ASSESSMENT  "',
        '"ENCOUNTER       "',
        '"CLOSEST APPROACH "',
        '"TARGET SOI       "',
        '"OUTSIDE SOI      "',
    ]:
        assert token in MAP
