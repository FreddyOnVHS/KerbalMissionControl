from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ADAPTER = (ROOT / "KMC.MissionControl" / "Navigation" / "OrbitMapNavigationAdapter.cs").read_text(encoding="utf-8")
MAP = (ROOT / "KMC.MissionControl" / "Pages" / "MapPage.cs").read_text(encoding="utf-8")


def test_map_adapter_calls_real_lambert_search():
    assert "TryCalculateLambertPreview" in ADAPTER
    assert "LambertTransferSearch.TryFindBest" in ADAPTER
    assert "DepartureSamples = 9" in ADAPTER
    assert "TimeOfFlightSamples = 9" in ADAPTER


def test_parent_mu_prefers_body_telemetry_with_data_driven_fallback():
    assert '"PARENT BODY TELEMETRY"' in ADAPTER
    assert '"ORBIT PERIOD"' in ADAPTER
    assert "DeriveGravParameterFromOrbit" in ADAPTER
    for stock_name in ["Kerbol", "Kerbin", "Duna", "Jool"]:
        assert stock_name not in ADAPTER


def test_map_labels_lambert_as_preview_only():
    assert '"LAMBERT PREVIEW  COARSE 9x9 / NO NODE AUTHORITY"' in MAP
    assert '"DEP VINF       "' in MAP
    assert '"ARR VINF       "' in MAP
    assert '"SCORE          "' in MAP


def test_create_node_path_remains_legacy_handoff():
    assert "TryCalculateParkingOrbitEjection(packet, originBody, solution, out ejection)" in MAP
    assert "ProgradeDeltaVMetersPerSecond = ejection.EjectionDeltaVMetersPerSecond" in MAP
    assert "LambertSolution" not in MAP
    assert "LambertTransferSearch" not in MAP


def test_preview_is_cached_instead_of_recomputed_every_draw():
    assert "_lambertPreviewAttempted" in MAP
    assert "EnsureLambertPreview(" in MAP
    assert "sameAnchor && previewStillFuture" in MAP
