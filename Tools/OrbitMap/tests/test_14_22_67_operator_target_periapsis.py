from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
OPT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
TRAJ = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiTrajectoryEvaluator.cs").read_text(encoding="utf-8")


def test_operator_controls_are_surface_altitude_based():
    assert '"TARGET PE ALT  "' in MAP
    assert '"-100 KM"' in MAP
    assert '"-10 KM"' in MAP
    assert '"+10 KM"' in MAP
    assert '"+100 KM"' in MAP
    assert '"AUTO"' in MAP


def test_manual_target_is_passed_into_coupled_solver():
    assert 'ResolveRequestedPeriapsisRadius(destination)' in MAP
    assert 'double desiredPeriapsisRadiusMeters' in OPT
    assert 'desiredPeriapsisRadiusOverrideMeters' in TRAJ


def test_manual_target_cannot_silently_fall_back():
    assert 'CUSTOM PE REQUIRES COUPLED SOLUTION' in MAP
    assert 'if (IsManualTargetPeriapsis())\n                return false;' in MAP
