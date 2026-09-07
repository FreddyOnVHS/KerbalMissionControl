import importlib.util
from pathlib import Path
import re

SCRIPT = Path(__file__).resolve().parents[1] / "apply_14_21_9.py"

EXPECTED_NEW = {
    "PumpABreakerTripped": ("POWER - PUMP A BREAKER TRIPPED", "BRK_PUMP_A"),
    "CabinFanABreakerTripped": ("POWER - CABIN FAN A BREAKER TRIPPED", "BRK_CABIN_FAN_A"),
    "ThermalHeaterABreakerTripped": ("POWER - THERMAL HEATER A BREAKER TRIPPED", "BRK_THERMAL_HEATER_A"),
    "FlightComputerBreakerTripped": ("POWER - FLIGHT COMPUTER BREAKER TRIPPED", "BRK_FLIGHT_COMPUTER"),
    "InstrumentationEssBreakerTripped": ("POWER - INSTRUMENTATION ESS BREAKER TRIPPED", "BRK_INSTRUMENTATION_ESS"),
    "RcsControlBreakerTripped": ("POWER - RCS CONTROL BREAKER TRIPPED", "BRK_RCS_CONTROL"),
    "GuidBBreakerTripped": ("POWER - GUID B BREAKER TRIPPED", "BRK_GUID_B"),
    "PumpBBreakerTripped": ("POWER - PUMP B BREAKER TRIPPED", "BRK_PUMP_B"),
    "CabinFanBBreakerTripped": ("POWER - CABIN FAN B BREAKER TRIPPED", "BRK_CABIN_FAN_B"),
    "ThermalHeaterBBreakerTripped": ("POWER - THERMAL HEATER B BREAKER TRIPPED", "BRK_THERMAL_HEATER_B"),
}

ALL_BREAKER_IDS = {
    "BRK_GUID_A",
    "BRK_COMM_A",
    "BRK_PUMP_A",
    "BRK_CABIN_FAN_A",
    "BRK_THERMAL_HEATER_A",
    "BRK_FLIGHT_COMPUTER",
    "BRK_INSTRUMENTATION_ESS",
    "BRK_FLIGHT_CONTROL",
    "BRK_REACTION_WHEEL",
    "BRK_ENGINE_CONTROL",
    "BRK_STAGING_CONTROL",
    "BRK_BRAKE_CONTROL",
    "BRK_GEAR_CONTROL",
    "BRK_LIGHTING_ESS",
    "BRK_RCS_CONTROL",
    "BRK_GUID_B",
    "BRK_COMM_B",
    "BRK_PUMP_B",
    "BRK_CABIN_FAN_B",
    "BRK_THERMAL_HEATER_B",
}

BASE_MODEL = """public enum InstructorFailurePreset
{
    GuidABreakerTripped = 14,
    CommABreakerTripped = 36,
    CommBBreakerTripped = 37,
    FlightControlBreakerTripped = 39,
    ReactionWheelBreakerTripped = 40,
    EngineControlBreakerTripped = 41,
    StagingControlBreakerTripped = 42,
    BrakeControlBreakerTripped = 43,
    GearControlBreakerTripped = 44,
    LightingEssBreakerTripped = 45
}
public static string GetFailurePresetName(InstructorFailurePreset preset)
{
    switch (preset)
    {
        case InstructorFailurePreset.LightingEssBreakerTripped: return "POWER - LIGHTING ESS BREAKER TRIPPED";
        case InstructorFailurePreset.CommABreakerTripped: return "POWER - COMM A BREAKER TRIPPED";
        default: return preset.ToString();
    }
}
"""

BASE_FORM = """else if (
    preset == InstructorFailurePreset.GuidABreakerTripped ||
    preset == InstructorFailurePreset.FlightControlBreakerTripped ||
    preset == InstructorFailurePreset.ReactionWheelBreakerTripped ||
    preset == InstructorFailurePreset.EngineControlBreakerTripped ||
    preset == InstructorFailurePreset.StagingControlBreakerTripped ||
    preset == InstructorFailurePreset.BrakeControlBreakerTripped ||
    preset == InstructorFailurePreset.GearControlBreakerTripped ||
    preset == InstructorFailurePreset.LightingEssBreakerTripped ||
    preset == InstructorFailurePreset.CommABreakerTripped ||
    preset == InstructorFailurePreset.CommBBreakerTripped)
{
    string switchId;
    SyntheticElectricalSwitchFailureMode switchMode;
    switch (preset)
    {
        case InstructorFailurePreset.LightingEssBreakerTripped:
            switchId = "BRK_LIGHTING_ESS";
            switchMode =
                SyntheticElectricalSwitchFailureMode.TrippedOpen;
            break;

        case InstructorFailurePreset.GuidABreakerTripped:
            switchId = "BRK_GUID_A";
            switchMode =
                SyntheticElectricalSwitchFailureMode.TrippedOpen;
            break;

        case InstructorFailurePreset.CommABreakerTripped:
            switchId = "BRK_COMM_A";
            switchMode =
                SyntheticElectricalSwitchFailureMode.TrippedOpen;
            break;

        case InstructorFailurePreset.CommBBreakerTripped:
            switchId = "BRK_COMM_B";
            switchMode =
                SyntheticElectricalSwitchFailureMode.TrippedOpen;
            break;
    }

    success =
        InstructorElectricalSourceFailureBridge
            .InjectSwitchFailure(
                _receiver,
                switchId,
                switchMode,
                delay,
                out failureId,
                out result);
}
"""

def load_module():
    spec = importlib.util.spec_from_file_location("apply_14_21_9", SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod

def test_adds_ten_missing_presets_and_labels():
    mod = load_module()
    patched, changed = mod.patch_training_model(BASE_MODEL)
    assert changed
    for preset, (label, _) in EXPECTED_NEW.items():
        assert f"{preset} =" in patched
        assert label in patched

def test_routes_all_ten_new_presets_through_tripped_open():
    mod = load_module()
    patched, changed = mod.patch_console_form(BASE_FORM)
    assert changed
    for preset, (_, breaker_id) in EXPECTED_NEW.items():
        assert f"InstructorFailurePreset.{preset}" in patched
        pattern = (
            rf"case InstructorFailurePreset\.{preset}:.*?"
            rf'switchId\s*=\s*"{breaker_id}";.*?'
            rf"SyntheticElectricalSwitchFailureMode\.TrippedOpen"
        )
        assert re.search(pattern, patched, re.DOTALL), preset

def test_patch_is_idempotent():
    mod = load_module()
    model1, _ = mod.patch_training_model(BASE_MODEL)
    model2, changed_model = mod.patch_training_model(model1)
    form1, _ = mod.patch_console_form(BASE_FORM)
    form2, changed_form = mod.patch_console_form(form1)
    assert model1 == model2
    assert form1 == form2
    assert not changed_model
    assert not changed_form

def test_repository_has_f10_trip_coverage_for_all_20_breakers_when_applied():
    import pytest
    repo = Path(__file__).resolve().parents[3]
    model = repo / "KMC.MissionControl" / "Training" / "InstructorTrainingModel.cs"
    form = repo / "KMC.MissionControl" / "Training" / "InstructorConsoleForm.cs"
    if not model.exists() or not form.exists():
        pytest.skip("Repository source tree not present in package staging directory.")

    model_text = model.read_text(encoding="utf-8-sig")
    form_text = form.read_text(encoding="utf-8-sig")

    all_ids_found = set(
        re.findall(
            r'switchId\s*=\s*"(BRK_[A-Z0-9_]+)";.*?'
            r"SyntheticElectricalSwitchFailureMode\.TrippedOpen",
            form_text,
            re.DOTALL,
        )
    )
    assert ALL_BREAKER_IDS <= all_ids_found

    for preset, (label, breaker_id) in EXPECTED_NEW.items():
        assert preset in model_text
        assert label in model_text
        assert breaker_id in form_text

    assert "InstructorElectricalSourceFailureBridge" in form_text
    assert ".InjectSwitchFailure(" in form_text
