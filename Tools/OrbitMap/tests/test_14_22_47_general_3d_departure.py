from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
TESTS = (ROOT / "Tools" / "NavigationTests" / "Program.cs").read_text(encoding="utf-8")


def test_plain_lambert_ejection_can_bootstrap_target_shooting():
    assert "General 3D bootstrap fallback" in MAP
    assert "preview," in MAP
    assert "ejectionPreview," in MAP
    assert '"TARGET-SOI SHOOTING / 3D BOOTSTRAP"' in MAP


def test_target_shooting_has_wide_3d_authority():
    assert "dvScale * 1.50" in SOLVER
    assert "dvScale * 0.025" in SOLVER
    assert "MaximumIterations = 96" in SOLVER


def test_regression_matrix_covers_four_geometry_classes():
    assert "inner / high-inclination class" in TESTS
    assert "eccentric inclined outer class" in TESTS
    assert "giant-planet / huge-SOI class" in TESTS
    assert "distant eccentric outer class" in TESTS


def test_runtime_has_no_stock_body_specific_logic():
    for name in ["Moho", "Dres", "Jool", "Eeloo"]:
        assert name not in SOLVER
