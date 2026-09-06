from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / 'KMC.MissionControl' / 'Training' / 'InstructorTrainingModel.cs'
FORM = ROOT / 'KMC.MissionControl' / 'Training' / 'InstructorConsoleForm.cs'

PRESETS = [
    ('FlightControlBreakerTripped', 39, 'POWER - FLIGHT CONTROL BREAKER TRIPPED', 'BRK_FLIGHT_CONTROL'),
    ('ReactionWheelBreakerTripped', 40, 'POWER - REACTION WHEEL BREAKER TRIPPED', 'BRK_REACTION_WHEEL'),
    ('EngineControlBreakerTripped', 41, 'POWER - ENGINE CONTROL BREAKER TRIPPED', 'BRK_ENGINE_CONTROL'),
    ('StagingControlBreakerTripped', 42, 'POWER - STAGING CONTROL BREAKER TRIPPED', 'BRK_STAGING_CONTROL'),
    ('BrakeControlBreakerTripped', 43, 'POWER - BRAKE CONTROL BREAKER TRIPPED', 'BRK_BRAKE_CONTROL'),
    ('GearControlBreakerTripped', 44, 'POWER - GEAR CONTROL BREAKER TRIPPED', 'BRK_GEAR_CONTROL'),
    ('LightingEssBreakerTripped', 45, 'POWER - LIGHTING ESS BREAKER TRIPPED', 'BRK_LIGHTING_ESS'),
]


def read_preserving(path):
    raw = path.read_bytes()
    bom = raw.startswith(b'\xef\xbb\xbf')
    text = raw.decode('utf-8-sig')
    newline = '\r\n' if '\r\n' in text else '\n'
    return text.replace('\r\n', '\n'), bom, newline


def write_preserving(path, text, bom, newline):
    raw = text.replace('\n', newline).encode('utf-8')
    if bom:
        raw = b'\xef\xbb\xbf' + raw
    path.write_bytes(raw)


def patch_training_model(text):
    changed = False

    missing = [p for p in PRESETS if f'{p[0]} =' not in text]
    if missing:
        anchor = re.compile(r'(\s*GenBContactorFalseOpenIndication\s*=\s*38)(\s*\n\s*})')
        insertion = ''.join(f',\n        {name} = {value}' for name, value, _, _ in missing)
        text, count = anchor.subn(lambda m: m.group(1) + insertion + m.group(2), text, count=1)
        if count != 1:
            raise RuntimeError('Could not locate InstructorFailurePreset enum tail anchor.')
        changed = True

    missing_labels = [p for p in PRESETS if p[2] not in text]
    if missing_labels:
        anchor = re.compile(
            r'(\s*case\s+InstructorFailurePreset\.GuidABreakerTripped:\s*return\s+"POWER - GUID A BREAKER TRIPPED";)'
        )
        insertion = ''.join(
            f'\n                case InstructorFailurePreset.{name}: return "{label}";'
            for name, _, label, _ in missing_labels
        )
        text, count = anchor.subn(lambda m: m.group(1) + insertion, text, count=1)
        if count != 1:
            raise RuntimeError('Could not locate GUID A breaker label anchor.')
        changed = True

    return text, changed


def patch_console_form(text):
    changed = False

    # Add the seven presets to the existing electrical switch-failure routing condition.
    missing_conditions = [p for p in PRESETS if f'preset ==\n                            InstructorFailurePreset.{p[0]}' not in text and f'preset == InstructorFailurePreset.{p[0]}' not in text]
    if missing_conditions:
        anchor = re.compile(
            r'(preset\s*==\s*\n?\s*InstructorFailurePreset\.GuidABreakerTripped\s*\|\|)'
        )
        insertion = ''.join(
            f'\n                        preset ==\n                            InstructorFailurePreset.{name} ||'
            for name, _, _, _ in missing_conditions
        )
        text, count = anchor.subn(lambda m: m.group(1) + insertion, text, count=1)
        if count != 1:
            raise RuntimeError('Could not locate electrical switch-failure routing condition anchor.')
        changed = True

    # Add switch mappings immediately before the existing GUID A breaker mapping.
    missing_cases = [p for p in PRESETS if f'case InstructorFailurePreset.{p[0]}:' not in text]
    if missing_cases:
        anchor = re.compile(
            r'(\s*case\s+InstructorFailurePreset\.GuidABreakerTripped:\s*\n\s*switchId\s*=\s*"BRK_GUID_A";)'
        )
        insertion = ''
        for name, _, _, switch_id in missing_cases:
            insertion += (
                f'\n                            case InstructorFailurePreset.{name}:\n'
                f'                                switchId = "{switch_id}";\n'
                f'                                switchMode =\n'
                f'                                    SyntheticElectricalSwitchFailureMode.TrippedOpen;\n'
                f'                                break;'
            )
        text, count = anchor.subn(lambda m: insertion + m.group(1), text, count=1)
        if count != 1:
            raise RuntimeError('Could not locate GUID A breaker switch mapping anchor.')
        changed = True

    return text, changed


def validate(model_text, form_text):
    for name, value, label, switch_id in PRESETS:
        required_model = (f'{name} = {value}', label)
        for token in required_model:
            if token not in model_text:
                raise RuntimeError(f'Post-patch validation failed: missing {token}.')
        if f'InstructorFailurePreset.{name}' not in form_text:
            raise RuntimeError(f'Post-patch validation failed: missing routing for {name}.')
        if f'"{switch_id}"' not in form_text:
            raise RuntimeError(f'Post-patch validation failed: missing {switch_id}.')


def main():
    for path in (MODEL, FORM):
        if not path.exists():
            raise SystemExit(f'Missing required file: {path}')

    model, mbom, mnl = read_preserving(MODEL)
    form, fbom, fnl = read_preserving(FORM)

    model, mc = patch_training_model(model)
    form, fc = patch_console_form(form)
    validate(model, form)

    if mc:
        write_preserving(MODEL, model, mbom, mnl)
    if fc:
        write_preserving(FORM, form, fbom, fnl)

    print(
        '14.21.7 applied: seven ESS breaker-trip presets added to the F10 instructor failure menu.'
        if (mc or fc)
        else '14.21.7 already applied; no changes needed.'
    )


if __name__ == '__main__':
    main()
