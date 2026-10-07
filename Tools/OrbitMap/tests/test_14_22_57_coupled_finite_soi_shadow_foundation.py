from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ENGINE = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiOptimizer.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "CoupledFiniteSoiShadowResult.cs").read_text(encoding="utf-8")


def test_coupled_solver_is_compiled_and_preserves_shadow_compatibility():
    assert r'Navigation\CoupledFiniteSoiOptimizer.cs' in PROJECT
    assert 'CoupledFiniteSoiOptimizer.TrySolve(' in MAP
    assert 'TrySolveShadow' in ENGINE
    assert 'AUTH COUPLED' in MAP
    assert '_lambertEjectionPreview = coupledShadow.FinalEjection' not in MAP
    assert '_lambertEjectionPreview = directCoupledShadow.FinalEjection' not in MAP


def test_coupled_formulation_keeps_dv_objective_and_interface_constraints_separate():
    assert 'TrySolveDampedNormalEquations' in ENGINE
    assert 'TotalDeltaVMetersPerSecond' in ENGINE
    assert 'SourceVelocityToleranceMetersPerSecond' in ENGINE
    assert 'SourceOutbound' in RESULT
    assert 'TargetInbound' in RESULT
