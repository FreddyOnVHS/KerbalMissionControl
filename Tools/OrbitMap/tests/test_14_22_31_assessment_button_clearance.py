from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / 'KMC.MissionControl' / 'Pages' / 'MapPage.cs').read_text(encoding='utf-8')


def test_status_block_is_anchored_above_button_bottom():
    assert 'int statusBlockBottom = buttonTop - 12;' in MAP
    assert 'int statusY =' in MAP
    assert '((totalLineCount - 1) * statusLineSpacing)' in MAP


def test_line_count_accounts_for_detail_and_assessment_rows():
    assert 'int detailLineCount =' in MAP
    assert 'int assessmentLineCount = 0;' in MAP
    assert 'status.TargetEncounter' in MAP
    assert 'int totalLineCount =' in MAP


def test_text_rows_advance_with_next_lineY_so_button_stays_clear():
    assert 'int nextLineY = statusY;' in MAP
    assert 'nextLineY += statusLineSpacing;' in MAP
    for token in [
        '"NODE STATUS  "',
        '"KSP TRANSFER ASSESSMENT  "',
        '"ENCOUNTER       "',
        '"CLOSEST APPROACH "',
        '"TARGET SOI       "',
        '"OUTSIDE SOI      "',
    ]:
        assert token in MAP
