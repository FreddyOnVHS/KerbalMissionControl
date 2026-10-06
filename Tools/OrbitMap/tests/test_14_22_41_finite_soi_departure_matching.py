from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")
SEARCH = (ROOT / "KMC.Engine" / "Navigation" / "ParkingOrbitAwareLambertSearch.cs").read_text(encoding="utf-8")
EVAL = (ROOT / "KMC.Engine" / "Navigation" / "FiniteSoiDepartureEvaluator.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_finite_soi_foundation_is_compiled():
    assert r'CelestialMechanics\StateVectorPropagator.cs' in PROJECT
    assert r'Navigation\FiniteSoiDepartureAssessment.cs' in PROJECT
    assert r'Navigation\FiniteSoiDepartureEvaluator.cs' in PROJECT


def test_actual_and_desired_boundary_states_are_both_propagated():
    assert "StateVectorPropagator.TryPropagate" in EVAL
    assert "originBody.SoiRadiusMeters" in EVAL
    assert "actualPosition" in EVAL
    assert "desiredTransferState" in EVAL


def test_search_scores_finite_soi_before_local_dv():
    assert SEARCH.index("stateDifference") < SEARCH.index("dvDifference")
    assert "FiniteSoiDepartureEvaluator.TryEvaluate" in SEARCH


def test_map_exposes_finite_soi_diagnostics():
    assert '"FINITE-SOI MATCH / TEST NODE SOURCE"' in MAP
    assert "PositionErrorMeters" in MAP
    assert "VelocityErrorMetersPerSecond" in MAP
    assert "NormalizedStateError" in MAP


def test_lambert_test_node_still_uses_ranked_ejection():
    assert "_parkingAwareLambertPreview.Ejection" in MAP
    assert '"CREATE LAMBERT TEST NODE"' in MAP


def test_normal_create_remains_legacy():
    upload = MAP[MAP.index("private void UploadTransferNode()"):]
    upload = upload.split("private static ManeuverUplinkPacket BuildLambertTestNodePacket", 1)[0]
    assert "NormalDeltaVMetersPerSecond = 0.0" in upload
    assert "RadialDeltaVMetersPerSecond = 0.0" in upload
