import importlib.util
from pathlib import Path

SCRIPT = Path(__file__).resolve().parents[1] / 'apply_14_21_7.py'
spec = importlib.util.spec_from_file_location('apply_14_21_7', SCRIPT)
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

OLD_MODEL = '''public enum InstructorFailurePreset\n{\n    GuidABreakerTripped = 14,\n    GenBContactorFalseOpenIndication = 38\n}\npublic static string GetFailurePresetName(InstructorFailurePreset preset)\n{\n    switch (preset)\n    {\n        case InstructorFailurePreset.GuidABreakerTripped: return \"POWER - GUID A BREAKER TRIPPED\";\n        default: return preset.ToString();\n    }\n}\n'''

OLD_FORM = '''else if (\n    preset == InstructorFailurePreset.GuidABreakerTripped ||\n    preset == InstructorFailurePreset.CommABreakerTripped ||\n    preset == InstructorFailurePreset.CommBBreakerTripped ||\n    preset == InstructorFailurePreset.GenAContactorFalseOpenIndication)\n{\n    string switchId;\n    SyntheticElectricalSwitchFailureMode switchMode;\n    switch (preset)\n    {\n        case InstructorFailurePreset.GuidABreakerTripped:\n            switchId = \"BRK_GUID_A\";\n            switchMode = SyntheticElectricalSwitchFailureMode.TrippedOpen;\n            break;\n        case InstructorFailurePreset.CommABreakerTripped:\n            switchId = \"BRK_COMM_A\";\n            switchMode = SyntheticElectricalSwitchFailureMode.TrippedOpen;\n            break;\n        case InstructorFailurePreset.CommBBreakerTripped:\n            switchId = \"BRK_COMM_B\";\n            switchMode = SyntheticElectricalSwitchFailureMode.TrippedOpen;\n            break;\n        default:\n            switchId = \"CONT_GEN_A\";\n            switchMode = SyntheticElectricalSwitchFailureMode.WeldedClosed;\n            break;\n    }\n}\n'''

EXPECTED = {
    'FlightControlBreakerTripped': ('POWER - FLIGHT CONTROL BREAKER TRIPPED', 'BRK_FLIGHT_CONTROL'),
    'ReactionWheelBreakerTripped': ('POWER - REACTION WHEEL BREAKER TRIPPED', 'BRK_REACTION_WHEEL'),
    'EngineControlBreakerTripped': ('POWER - ENGINE CONTROL BREAKER TRIPPED', 'BRK_ENGINE_CONTROL'),
    'StagingControlBreakerTripped': ('POWER - STAGING CONTROL BREAKER TRIPPED', 'BRK_STAGING_CONTROL'),
    'BrakeControlBreakerTripped': ('POWER - BRAKE CONTROL BREAKER TRIPPED', 'BRK_BRAKE_CONTROL'),
    'GearControlBreakerTripped': ('POWER - GEAR CONTROL BREAKER TRIPPED', 'BRK_GEAR_CONTROL'),
    'LightingEssBreakerTripped': ('POWER - LIGHTING ESS BREAKER TRIPPED', 'BRK_LIGHTING_ESS'),
}

def test_model_adds_all_presets_and_labels():
    patched, changed = mod.patch_training_model(OLD_MODEL)
    assert changed
    for preset, (label, _) in EXPECTED.items():
        assert preset in patched
        assert label in patched


def test_console_routes_all_new_presets_to_existing_switch_bridge_path():
    patched, changed = mod.patch_console_form(OLD_FORM)
    assert changed
    for preset, (_, switch_id) in EXPECTED.items():
        assert f'InstructorFailurePreset.{preset}' in patched
        assert f'\"{switch_id}\"' in patched
    assert patched.count('SyntheticElectricalSwitchFailureMode.TrippedOpen') >= 10


def test_patches_are_idempotent():
    model1, _ = mod.patch_training_model(OLD_MODEL)
    model2, changed_model = mod.patch_training_model(model1)
    form1, _ = mod.patch_console_form(OLD_FORM)
    form2, changed_form = mod.patch_console_form(form1)
    assert model1 == model2
    assert form1 == form2
    assert not changed_model
    assert not changed_form


def test_repository_sources_use_existing_switch_failure_bridge_when_available():
    import pytest
    repo = Path(__file__).resolve().parents[3]
    model = repo / 'KMC.MissionControl' / 'Training' / 'InstructorTrainingModel.cs'
    form = repo / 'KMC.MissionControl' / 'Training' / 'InstructorConsoleForm.cs'
    if not model.exists() or not form.exists():
        pytest.skip('Repository source tree not present in package staging directory.')
    model_text = model.read_text(encoding='utf-8-sig')
    form_text = form.read_text(encoding='utf-8-sig')
    for preset, (label, switch_id) in EXPECTED.items():
        assert preset in model_text
        assert label in model_text
        assert f'InstructorFailurePreset.{preset}' in form_text
        assert f'\"{switch_id}\"' in form_text
    assert 'InstructorElectricalSourceFailureBridge' in form_text
    assert '.InjectSwitchFailure(' in form_text
