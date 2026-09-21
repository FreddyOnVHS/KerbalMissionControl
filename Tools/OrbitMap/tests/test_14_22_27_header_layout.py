from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP_PAGE = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_subpage_tabs_are_below_orbit_map_title():
    assert "int y = bounds.Top + 42;" in MAP_PAGE
    assert "int y = bounds.Top + 34;" not in MAP_PAGE


def test_local_viewport_starts_below_subpage_tabs():
    assert "int header = 82;" in MAP_PAGE


def test_transfer_panels_start_below_subpage_tabs():
    assert "int top = bounds.Top + 84;" in MAP_PAGE


def test_142227_transfer_behavior_is_unchanged():
    assert 'DrawTab(context, _localTab, "LOCAL", _subpage == 0)' in MAP_PAGE
    assert 'DrawTab(context, _transferTab, "TRANSFER", _subpage == 1)' in MAP_PAGE
    assert 'CALCULATION NOT ENABLED IN 14.22.27' in MAP_PAGE
