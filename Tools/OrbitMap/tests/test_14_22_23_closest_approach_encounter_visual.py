from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PLUGIN = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")
CACHE = (ROOT / "KMC.MissionControl" / "Rendering" / "OrbitMap" / "OrbitMapSceneCache.cs").read_text(encoding="utf-8")

def test_child_samples_use_moving_reference_body_at_each_ut():
    assert "referenceBody.orbit.TrueAnomalyAtT(referenceBody.orbit.getObtAtUT(ut))" in PLUGIN
    assert "CanonicalPositionAtTrueAnomaly(referenceBody.orbit, bodyTrueAnomaly)" in PLUGIN
    assert "canonicalParent = canonicalLocal + canonicalBody;" in PLUGIN
    assert "encounterBodyAnchor" not in PLUGIN

def test_encounter_visual_uses_closest_approach_sample():
    assert "FindClosestApproachSample(patch)" in CACHE
    assert "DistanceToReferenceBodySquared" in CACHE

def test_encounter_marker_uses_closest_sample_body_position():
    assert "closest.ReferenceBodyPositionX" in CACHE
    assert "closest.ReferenceBodyPositionY" in CACHE
    assert "closest.ReferenceBodyPositionZ" in CACHE

def test_encounter_ut_is_closest_approach_ut():
    assert "encounterBody.EncounterUniversalTimeSeconds = closest.UniversalTimeSeconds;" in CACHE
