from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiShadowResult.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_terminal_uses_two_component_bplane_geometry():
    assert "TerminalResidualCount = 11" in OPT
    assert "TryComputeBPlaneRadialErrors" in OPT
    assert "residuals[9] = bTError / bTolerance" in OPT
    assert "residuals[10] = bRError / bTolerance" in OPT
    assert "nearest point on" in OPT and "desired-B circle" in OPT


def test_bplane_terminal_is_destination_agnostic():
    for body in ("Duna", "Eve", "Dres", "Moho", "Jool", "Eeloo"):
        assert body not in OPT
    assert "destinationBody.GravParameter" in OPT
    assert "destinationBody.SoiRadiusMeters" in OPT


def test_terminal_finite_difference_can_use_one_sided_boundary_derivatives():
    assert "if (plusOk && minusOk)" in OPT
    assert "else if (plusOk)" in OPT
    assert "else if (minusOk)" in OPT
    assert "one-sided" in OPT


def test_terminal_acceptance_requires_bplane_and_periapsis():
    assert "TargetBPlaneToleranceMeters" in OPT
    assert "TargetPeriapsisToleranceMeters" in OPT
    assert "TargetBPlaneMagnitudeErrorMeters <=" in OPT


def test_production_result_reports_bplane_components_and_guarded_authority():
    assert "TargetBPlaneMagnitudeErrorMeters" in RESULT
    assert "TargetBPlaneTErrorMeters" in RESULT
    assert "TargetBPlaneRErrorMeters" in RESULT
    assert '"B MAG ERR " +' in MAP
    assert '"  B.T " +' in MAP
    assert '"  B.R " +' in MAP
    assert "AUTH COUPLED" in MAP
    assert "_lambertEjectionPreview = coupledShadow.FinalEjection" not in MAP
