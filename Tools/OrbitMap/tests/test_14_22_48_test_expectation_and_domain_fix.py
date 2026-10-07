from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GENERAL = (ROOT / "KMC.Engine" / "Navigation" / "General3dHyperbolicDeparturePlanner.cs").read_text(encoding="utf-8")
TESTS = (ROOT / "Tools" / "NavigationTests" / "LambertEjectionProgram.cs").read_text(encoding="utf-8")


def test_general_3d_solver_preserves_parking_eccentricity_domain():
    assert "MaximumParkingEccentricity = 0.05" in GENERAL
    assert "parkingOrbit.Eccentricity >" in GENERAL
    assert "MaximumParkingEccentricity" in GENERAL


def test_out_of_plane_test_no_longer_requires_zero_radial_dv():
    block = TESTS[TESTS.index("private static void OutOfPlane()"):TESTS.index("private static void InclinedPlane()")]
    assert "radial=0 assumption" in block
    assert "RadialDeltaVMetersPerSecond" in block
