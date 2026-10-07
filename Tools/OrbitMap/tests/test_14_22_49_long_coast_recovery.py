from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROP = (ROOT / "KMC.Engine" / "CelestialMechanics" / "StateVectorPropagator.cs").read_text(encoding="utf-8")
TESTS = (ROOT / "Tools" / "NavigationTests" / "StateVectorPropagationProgram.cs").read_text(encoding="utf-8")


def test_long_coast_has_segmented_recovery():
    assert "TryPropagateSingleStep" in PROP
    assert "TryPropagateSegmented" in PROP
    assert "MaximumRecoverySegments = 256" in PROP


def test_primary_single_step_path_is_preserved():
    public = PROP[PROP.index("public static bool TryPropagate"):PROP.index("private static bool TryPropagateSegmented")]
    assert "TryPropagateSingleStep" in public
    assert "return true;" in public


def test_near_parabolic_regression_exists():
    assert "LongNearParabolic" in TESTS
    assert "-0.603317734943164" in TESTS
    assert "100.0" in TESTS
