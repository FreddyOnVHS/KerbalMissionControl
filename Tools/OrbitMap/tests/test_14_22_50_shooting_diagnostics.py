from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROP = (ROOT / 'KMC.Engine' / 'CelestialMechanics' / 'StateVectorPropagator.cs').read_text(encoding='utf-8')
SOLVER = (ROOT / 'KMC.Engine' / 'Navigation' / 'TargetSoiShootingSolver.cs').read_text(encoding='utf-8')
MAP = (ROOT / 'KMC.MissionControl' / 'Pages' / 'MapPage.cs').read_text(encoding='utf-8')
TESTS = (ROOT / 'Tools' / 'NavigationTests' / 'Program.cs').read_text(encoding='utf-8')


def test_unsafe_global_segmented_recovery_is_removed():
    assert 'MaximumRecoverySegments' not in PROP
    assert 'TryPropagateSegmented' not in PROP
    assert 'TryPropagateSingleStep' not in PROP


def test_shooting_solver_reports_seed_failure_stage():
    assert 'out string failureReason' in SOLVER
    for stage in [
        'PARKING STATE PROPAGATION',
        'PARKING P/N/R FRAME',
        'SOURCE SOI EXIT',
        'ARRIVAL BEFORE SOURCE SOI EXIT',
        'ORIGIN PARENT STATE AT EXIT',
        'PARENT-FRAME COAST TO ARRIVAL',
        'DESTINATION STATE AT ARRIVAL',
        'NONFINITE ARRIVAL ASSESSMENT',
    ]:
        assert stage in SOLVER


def test_map_surfaces_shooting_rejection_instead_of_silent_fallback():
    assert '_targetSoiShootingFailureText' in MAP
    assert 'TARGET-SOI SHOOTING REJECTED' in MAP
    assert 'out shootingFailure' in MAP
    assert 'out directShootingFailure' in MAP


def test_navigation_regression_exercises_diagnostic_api():
    assert 'target SOI shooting reports rejection diagnostics' in TESTS
    assert 'INVALID INPUT / MISSING BODY DATA' in TESTS
