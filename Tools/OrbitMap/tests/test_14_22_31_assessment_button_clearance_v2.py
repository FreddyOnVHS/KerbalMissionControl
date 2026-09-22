from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / 'KMC.MissionControl' / 'Pages' / 'MapPage.cs').read_text(encoding='utf-8')


def test_status_layout_uses_actual_rendered_font_height():
    assert 'context.SmallFont.GetHeight(' in MAP
    assert 'int statusFontHeight =' in MAP


def test_status_block_reserves_real_gap_above_button():
    assert 'const int statusButtonGap = 10;' in MAP
    assert 'buttonTop -' in MAP
    assert 'statusButtonGap' in MAP


def test_status_block_height_includes_last_lines_font_height():
    assert 'int statusBlockHeight =' in MAP
    assert '(totalLineCount - 1) * statusLineSpacing +' in MAP
    assert 'statusFontHeight;' in MAP


def test_all_assessment_rows_still_render_before_button():
    for token in [
        '"NODE STATUS  "',
        '"KSP TRANSFER ASSESSMENT  "',
        '"ENCOUNTER       "',
        '"CLOSEST APPROACH "',
        '"TARGET SOI       "',
        '"OUTSIDE SOI      "',
        '"NODE CREATED"',
        '"NODE UPLINKED"',
    ]:
        assert token in MAP
