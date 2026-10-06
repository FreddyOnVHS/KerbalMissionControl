from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

SEARCH = (ROOT / "KMC.Engine" / "Navigation" / "ParkingOrbitAwareLambertSearch.cs").read_text(encoding="utf-8")
SOLUTION = (ROOT / "KMC.Engine" / "Navigation" / "ParkingOrbitAwareTransferSolution.cs").read_text(encoding="utf-8")
ADAPTER = (ROOT / "KMC.MissionControl" / "Navigation" / "OrbitMapNavigationAdapter.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")
PROJECT = (ROOT / "KMC.Engine" / "KMC.Engine.csproj").read_text(encoding="utf-8")


def test_parking_aware_search_is_compiled():
    assert r'Navigation\ParkingOrbitAwareLambertSearch.cs' in PROJECT
    assert r'Navigation\ParkingOrbitAwareTransferSolution.cs' in PROJECT


def test_primary_score_is_actual_local_ejection_dv():
    assert "LambertParkingOrbitEjectionPlanner.TryCalculate" in SEARCH
    assert "candidate.EjectionScoreMetersPerSecond" in SEARCH
    assert "Ejection.TotalDeltaVMetersPerSecond" in SOLUTION


def test_arrival_vinf_is_secondary_tiebreaker():
    assert SEARCH.index("scoreDifference") < SEARCH.index("arrivalDifference")


def test_adapter_exposes_live_parking_aware_preview():
    assert "TryCalculateParkingAwareLambertPreview" in ADAPTER
    assert "ParkingOrbitAwareLambertSearch.TryFindBest" in ADAPTER


def test_map_keeps_no_node_authority():
    assert '"PARKING-AWARE LAMBERT / NO NODE AUTHORITY"' in MAP
    assert '"EJECT SCORE "' in MAP
    assert "NormalDeltaVMetersPerSecond = 0.0" in MAP
    assert "RadialDeltaVMetersPerSecond = 0.0" in MAP


def test_no_stock_body_specific_logic():
    combined = SEARCH + SOLUTION + ADAPTER
    for name in ["Kerbin", "Duna", "Eve", "Jool"]:
        assert name not in combined
