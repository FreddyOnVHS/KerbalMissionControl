from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_target_shooting_supersedes_finite_soi_display():
    assert "_targetSoiShooting == null" in MAP
    finite = MAP[MAP.index("FiniteSoiDepartureAssessment finiteSoi ="):]
    finite = finite[:finite.index('if (_targetSoiShooting != null')]
    assert "_targetSoiShooting == null" in finite


def test_target_shooting_block_remains_complete():
    assert '"TARGET-SOI SHOOTING"' in MAP
    assert '"PREDICT ENCOUNTER"' in MAP
    assert '"TARGET PE R "' in MAP
    assert '"PRED PE R "' in MAP
    assert '"PRED PE ALT "' in MAP
    assert '"SAFE FLYBY"' in MAP


def test_target_block_allows_exact_fit():
    assert "y + transferLineHeight * 6 <=" in MAP


def test_navigation_authority_unchanged():
    helper = MAP[MAP.index("private LambertParkingOrbitEjectionSolution GetProductionLambertEjection()"):]
    assert "_targetSoiShooting.CorrectedEjection" in helper
    assert '"CREATE KSP NODE"' in MAP
