from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiShadowResult.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_terminal_jacobian_retries_smaller_perturbations():
    assert "TryBuildRobustTerminalJacobian" in OPT
    assert "MaximumTerminalDerivativeRetries" in OPT
    assert "MinimumTerminalFiniteDifferenceStep" in OPT
    assert "h *= 0.5" in OPT


def test_unavailable_terminal_column_does_not_abort_whole_jacobian():
    assert "MinimumUsableTerminalJacobianColumns" in OPT
    assert "Leave an unavailable column at zero" in OPT
    assert "usableColumns >= MinimumUsableTerminalJacobianColumns" in OPT


def test_terminal_step_retries_with_smaller_trust_step():
    assert "MaximumTerminalStepRetries" in OPT
    assert "retryScale *= 0.5" in OPT
    assert "terminalRejectedSteps++" in OPT
    assert "terminalTrustRadius = Math.Max(0.005" in OPT


def test_terminal_diagnostics_expose_jacobian_rejections_and_trust_radius():
    assert "TerminalJacobianColumns" in RESULT
    assert "TerminalRejectedSteps" in RESULT
    assert "TerminalTrustRadius" in RESULT
    assert '"  JAC " + coupledResult.TerminalJacobianColumns' in MAP
    assert '"  REJ " + coupledResult.TerminalRejectedSteps' in MAP
    assert '"  TR " + coupledResult.TerminalTrustRadius' in MAP


def test_robustness_remains_destination_agnostic_with_guarded_production_authority():
    for body in ("Duna", "Eve", "Dres", "Moho", "Jool", "Eeloo"):
        assert body not in OPT
    assert "AUTH COUPLED" in MAP
    assert "_lambertEjectionPreview = coupledShadow.FinalEjection" not in MAP
