
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = (ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs").read_text(encoding="utf-8")

def test_build_patches_rejects_unserializable_orbit_before_adding():
    assert "IsSerializablePatchOrbit" in SENDER
    assert "if (!IsSerializablePatchOrbit(item.Orbit))" in SENDER
    guard = SENDER.index("if (!IsSerializablePatchOrbit(item.Orbit))")
    add = SENDER.index("packet.Patches.Add(item);")
    assert guard < add

def test_patch_orbit_guard_checks_required_orbit_fields():
    required = [
        "SemiMajorAxisMeters",
        "Eccentricity",
        "InclinationDegrees",
        "LongitudeOfAscendingNodeDegrees",
        "ArgumentOfPeriapsisDegrees",
        "EpochUniversalTimeSeconds",
        "MeanAnomalyAtEpochRadians",
        "PeriapsisMeters",
        "PositionX",
        "PositionY",
        "PositionZ",
    ]
    start = SENDER.index("private static bool IsSerializablePatchOrbit")
    block = SENDER[start:start+2600]
    for name in required:
        assert name in block
    assert "ApoapsisMeters" not in block
    assert "PeriodSeconds" not in block

def test_invalid_future_patch_stops_chain_without_poisoning_packet():
    start = SENDER.index("if (!IsSerializablePatchOrbit(item.Orbit))")
    block = SENDER[start:start+500]
    assert "break;" in block
    assert "Debug.LogWarning" in block
