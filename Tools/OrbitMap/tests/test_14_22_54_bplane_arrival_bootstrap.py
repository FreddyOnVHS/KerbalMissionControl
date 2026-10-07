from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

BOOT = (ROOT / "KMC.Engine" / "Navigation" / "TargetBPlaneBootstrapPlanner.cs").read_text(encoding="utf-8")
SOLVER = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingSolver.cs").read_text(encoding="utf-8")
RESULT = (ROOT / "KMC.Engine" / "Navigation" / "TargetSoiShootingResult.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_bootstrap_uses_desired_periapsis_bplane_endpoint():
    assert "desiredPeriapsisRadiusMeters" in BOOT
    assert "desiredBPlaneRadiusMeters" in BOOT
    assert "destinationArrival.Position +" in BOOT
    assert "LambertSolver.TrySolve" in BOOT


def test_bootstrap_samples_bplane_azimuth_and_minimizes_departure_dv():
    assert "AzimuthSamples = 12" in BOOT
    assert "trial.TotalDeltaVMetersPerSecond <" in BOOT


def test_shooting_applies_bootstrap_only_inside_dv_trust_region():
    assert "TargetBPlaneBootstrapPlanner.TryCreateEjection" in SOLVER
    assert "IsWithinDepartureTrustRegion" in SOLVER
    assert "bPlaneBootstrapApplied" in SOLVER


def test_map_reports_bootstrap_usage():
    assert "BPlaneBootstrapApplied" in RESULT
    assert '"  BPLANE SEED"' in MAP
