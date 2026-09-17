from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SENDER = ROOT / "KMC.Plugin" / "OrbitMapTelemetrySender.cs"

def _build_patches_body():
    text = SENDER.read_text(encoding="utf-8")
    start = text.index("private static void BuildPatches")
    end = text.index("public void OnDestroy", start)
    return text[start:end]

def test_projected_patch_chain_starts_from_first_maneuver_node_next_patch():
    body = _build_patches_body()
    assert "firstNode.nextPatch" in body
    assert "patch = firstNode.nextPatch;" in body

def test_without_maneuver_only_future_natural_patch_is_sent():
    body = _build_patches_body()
    assert "patch = active.orbit.nextPatch;" in body

def test_current_active_orbit_is_not_duplicated_as_projected_patch():
    body = _build_patches_body()
    assert "Orbit patch = active.orbit;" not in body

def test_maneuver_nodes_are_sorted_before_projected_patch_source_is_selected():
    body = _build_patches_body()
    assert "nodes.Sort" in body
    assert "ManeuverNode firstNode = nodes[0];" in body
