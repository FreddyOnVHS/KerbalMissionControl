from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_coupled_shadow_is_rendered_before_legacy_target_shooting():
    shadow = MAP.index('"COUPLED FINITE-SOI OPTIMIZER / SHADOW"')
    shooting = MAP.index('"TARGET-SOI SHOOTING REJECTED"')
    assert shadow < shooting


def test_coupled_shadow_keeps_priority_budget_and_no_authority():
    assert "y + transferLineHeight * 6 <=" in MAP
    assert '"DV " +' in MAP
    assert '"SOURCE IF " +' in MAP
    assert '"TARGET IF " +' in MAP
    assert '"B MAG ERR " +' in MAP
    assert '"  NO NODE AUTHORITY"' in MAP
