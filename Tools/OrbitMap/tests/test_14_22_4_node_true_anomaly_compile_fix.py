from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"

def test_maneuver_node_does_not_call_unavailable_ksp_api():
    text = SENDER.read_text(encoding="utf-8")
    assert "getTrueAnomalyAtUT" not in text

def test_maneuver_node_uses_local_true_anomaly_solver():
    text = SENDER.read_text(encoding="utf-8")
    assert "TrueAnomalyAtUT(active.orbit, node.UT)" in text
    assert "private static double TrueAnomalyAtUT(Orbit orbit, double ut)" in text

def test_local_solver_handles_elliptic_and_hyperbolic_orbits():
    text = SENDER.read_text(encoding="utf-8")
    assert "SolveEccentricAnomaly" in text
    assert "SolveHyperbolicAnomaly" in text
    assert "orbit.referenceBody.gravParameter" in text
