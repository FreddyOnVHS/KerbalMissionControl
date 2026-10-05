from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]

ENGINE = ROOT / "KMC.Engine"
SEARCH = (ENGINE / "Navigation" / "LambertTransferSearch.cs").read_text(encoding="utf-8")
REQUEST = (ENGINE / "Navigation" / "TransferSearchRequest.cs").read_text(encoding="utf-8")
SOLUTION = (ENGINE / "Navigation" / "TransferSearchSolution.cs").read_text(encoding="utf-8")
PROJECT = (ENGINE / "KMC.Engine.csproj").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_transfer_search_is_compiled_into_engine():
    assert r'Navigation\LambertTransferSearch.cs' in PROJECT
    assert r'Navigation\TransferSearchRequest.cs' in PROJECT
    assert r'Navigation\TransferSearchSolution.cs' in PROJECT


def test_search_combines_propagation_and_lambert_without_ui_dependencies():
    combined = SEARCH + REQUEST + SOLUTION
    assert "KeplerPropagator.TryPropagate" in SEARCH
    assert "LambertSolver.TrySolve" in SEARCH
    assert "UnityEngine" not in combined
    assert "OrbitMap" not in combined
    assert "ManeuverUplink" not in combined


def test_search_is_generic_and_has_no_stock_body_constants():
    combined = SEARCH + REQUEST + SOLUTION
    for body in ["Kerbin", "Mun", "Minmus", "Duna", "Eve", "Jool"]:
        assert body not in combined
    assert "ParentGravParameter" in REQUEST
    assert "OriginBody" in REQUEST
    assert "DestinationBody" in REQUEST


def test_search_scores_parent_frame_endpoint_velocity_mismatch():
    assert "lambert.DepartureVelocity -" in SEARCH
    assert "originState.Velocity" in SEARCH
    assert "lambert.ArrivalVelocity -" in SEARCH
    assert "destinationState.Velocity" in SEARCH
    assert "CombinedExcessSpeedMetersPerSecond" in SOLUTION


def test_search_bounds_are_explicit_and_bounded():
    assert "DepartureSamples" in REQUEST
    assert "TimeOfFlightSamples" in REQUEST
    assert "MaximumSamplesPerAxis = 256" in SEARCH
    assert "EarliestDepartureUniversalTimeSeconds" in REQUEST
    assert "MaximumTimeOfFlightSeconds" in REQUEST


def test_existing_map_still_does_not_consume_lambert_search():
    assert "LambertTransferSearch" not in MAP
    assert "TransferSearchRequest" not in MAP
    assert "TransferSearchSolution" not in MAP
