from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_transfer_rows_use_rendered_font_height():
    assert "transferFontHeight" in MAP
    assert "context.SmallFont.GetHeight" in MAP
    assert "transferLineHeight" in MAP
    assert "transferSectionGap" in MAP


def test_upper_content_has_hard_status_boundary():
    assert "transferContentBottom" in MAP
    assert "plannerPanel.Bottom - 235" in MAP
    assert "y + transferLineHeight" in MAP


def test_diagnostics_are_compact_but_complete():
    assert '"HOHMANN WINDOW  CIRCULAR / COPLANAR APPROX"' in MAP
    assert '"LAMBERT PREVIEW  COARSE 9x9 / NO NODE AUTHORITY"' in MAP
    assert '"PARKING-AWARE LAMBERT / NO NODE AUTHORITY"' in MAP
    assert '"FINITE-SOI MATCH / TEST NODE SOURCE"' in MAP
    assert '"POS ERR "' in MAP
    assert '"DV P/N/R "' in MAP


def test_status_block_has_separator():
    assert "separatorPen" in MAP
    assert "statusY - 10" in MAP


def test_legacy_candidate_is_built_even_if_text_is_hidden():
    assert "_transferNodeCandidate =" in MAP
    assert '"LEGACY EJECTION / CREATE KSP NODE"' in MAP


def test_authority_paths_unchanged():
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    upload = upload.split("private static ManeuverUplinkPacket BuildLambertTestNodePacket", 1)[0]
    assert "NormalDeltaVMetersPerSecond = 0.0" in upload
    assert "RadialDeltaVMetersPerSecond = 0.0" in upload
    assert '"CREATE LAMBERT TEST NODE"' in MAP
