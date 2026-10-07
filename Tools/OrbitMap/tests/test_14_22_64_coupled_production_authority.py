from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_coupled_solution_is_first_production_authority_when_guard_passes():
    assert "GetProductionCoupledEjection" in MAP
    assert "IsCoupledProductionReady" in MAP
    assert '"B-PLANE / PE SOLVED"' in MAP
    assert "result.FeasibilityPassSucceeded" in MAP
    assert "result.SourceOutbound" in MAP
    assert "result.TargetInbound" in MAP
    assert "result.TargetAssessment.PredictedEncounter" in MAP
    assert "result.TargetAssessment.PredictedCollision" in MAP
    assert "coupledEjection ?? lambertEjection" in MAP


def test_coupled_node_uses_full_pnr_and_live_vessel_id():
    assert "authoritativeEjection.ProgradeDeltaVMetersPerSecond" in MAP
    assert "authoritativeEjection.NormalDeltaVMetersPerSecond" in MAP
    assert "authoritativeEjection.RadialDeltaVMetersPerSecond" in MAP
    assert "_currentTransferVesselId" in MAP
    assert "packet.VesselId ?? string.Empty" in MAP


def test_legacy_paths_remain_fallbacks():
    assert "GetProductionLambertEjection" in MAP
    assert "legacyCandidate == null" in MAP
    assert '"LEGACY FALLBACK UPLINK SENT"' in MAP
    assert '"LAMBERT AUTHORITY UPLINK SENT"' in MAP


def test_ui_reports_production_authority_and_disables_refine():
    assert '"COUPLED FINITE-SOI OPTIMIZER / PRODUCTION"' in MAP
    assert '"  MANEUVER AUTHORITY"' in MAP
    assert '"COUPLED AUTHORITY UPLINK SENT"' in MAP
    assert '"COUPLED AUTHORITY - NO REFINE"' in MAP
    assert '"COUPLED NODE REFINEMENT DISABLED"' in MAP


def test_guard_is_generic_not_body_specific():
    guard = MAP[MAP.index("private static bool IsCoupledProductionReady"):MAP.index("private LambertParkingOrbitEjectionSolution GetProductionLambertEjection")]
    for body in ("Duna", "Eve", "Dres", "Moho", "Jool", "Eeloo"):
        assert body not in guard
    assert "desiredPe * 0.002" in guard
    assert "desiredB * 0.002" in guard
