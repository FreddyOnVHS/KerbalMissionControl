import importlib.util
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / "apply_14_21_10.py"

SOURCE = r"""
            AddLoad(
                distribution,
                "LIGHTING_ESS",
                "EXTERNAL / EMERGENCY LIGHTING",
                "BUS_ESS",
                0.5,
                1);

            return distribution;
        }

        private static void ApplyCrewControls(
"""

def load_module():
    spec = importlib.util.spec_from_file_location("apply_14_21_10", SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod

def test_rcs_load_is_promoted_into_nominal_distribution():
    mod = load_module()
    patched, changed = mod.patch_source(SOURCE)

    assert changed
    assert '"RCS_CONTROL"' in patched
    assert '"RCS CONTROL / VALVE POWER"' in patched
    assert '"BUS_ESS"' in patched

    # Critical ordering: the load/breaker must exist in the nominal
    # distribution before BuildAndApply applies controls and switch failures.
    assert patched.index('"RCS_CONTROL"') < patched.index(
        "private static void ApplyCrewControls"
    )

def test_rcs_load_is_priority_one_one_amp():
    mod = load_module()
    patched, _ = mod.patch_source(SOURCE)

    block_start = patched.index('"RCS_CONTROL"')
    block = patched[block_start:block_start + 220]

    assert "1.0" in block
    assert "1);" in block

def test_patch_is_idempotent():
    mod = load_module()
    once, changed_once = mod.patch_source(SOURCE)
    twice, changed_twice = mod.patch_source(once)

    assert changed_once
    assert not changed_twice
    assert once == twice

def test_patch_targets_engine_distribution_only():
    # The production patcher for this final fix must only target the Engine
    # electrical distribution source. The repository itself legitimately
    # contains a KMC.Plugin project, so testing for the absence of that folder
    # would be invalid.
    mod = load_module()

    expected = (
        Path("KMC.Engine")
        / "SpacecraftSystems"
        / "ElectricalDistributionSystem.cs"
    )

    actual = mod.TARGET.relative_to(mod.ROOT)

    assert actual == expected
