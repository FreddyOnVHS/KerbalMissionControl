from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
MODEL = ROOT / "KMC.MissionControl" / "Training" / "InstructorTrainingModel.cs"
FORM = ROOT / "KMC.MissionControl" / "Training" / "InstructorConsoleForm.cs"

PRESETS = [
    ("PumpABreakerTripped", 46, "POWER - PUMP A BREAKER TRIPPED", "BRK_PUMP_A"),
    ("CabinFanABreakerTripped", 47, "POWER - CABIN FAN A BREAKER TRIPPED", "BRK_CABIN_FAN_A"),
    ("ThermalHeaterABreakerTripped", 48, "POWER - THERMAL HEATER A BREAKER TRIPPED", "BRK_THERMAL_HEATER_A"),
    ("FlightComputerBreakerTripped", 49, "POWER - FLIGHT COMPUTER BREAKER TRIPPED", "BRK_FLIGHT_COMPUTER"),
    ("InstrumentationEssBreakerTripped", 50, "POWER - INSTRUMENTATION ESS BREAKER TRIPPED", "BRK_INSTRUMENTATION_ESS"),
    ("RcsControlBreakerTripped", 51, "POWER - RCS CONTROL BREAKER TRIPPED", "BRK_RCS_CONTROL"),
    ("GuidBBreakerTripped", 52, "POWER - GUID B BREAKER TRIPPED", "BRK_GUID_B"),
    ("PumpBBreakerTripped", 53, "POWER - PUMP B BREAKER TRIPPED", "BRK_PUMP_B"),
    ("CabinFanBBreakerTripped", 54, "POWER - CABIN FAN B BREAKER TRIPPED", "BRK_CABIN_FAN_B"),
    ("ThermalHeaterBBreakerTripped", 55, "POWER - THERMAL HEATER B BREAKER TRIPPED", "BRK_THERMAL_HEATER_B"),
]

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

def read_preserving(path):
    raw = path.read_bytes()
    bom = raw.startswith(b"\xef\xbb\xbf")
    text = raw.decode("utf-8-sig")
    newline = "\r\n" if "\r\n" in text else "\n"
    return text.replace("\r\n", "\n"), bom, newline

def write_preserving(path, text, bom, newline):
    raw = text.replace("\n", newline).encode("utf-8")
    if bom:
        raw = b"\xef\xbb\xbf" + raw
    path.write_bytes(raw)

def patch_training_model(text):
    changed = False

    missing = [p for p in PRESETS if f"{p[0]} =" not in text]
    if missing:
        anchor = re.compile(
            r"(\s*LightingEssBreakerTripped\s*=\s*45)(\s*\n\s*})"
        )
        insertion = "".join(
            f",\n        {name} = {value}"
            for name, value, _, _ in missing
        )
        text, count = anchor.subn(
            lambda m: m.group(1) + insertion + m.group(2),
            text,
            count=1,
        )
        if count != 1:
            raise RuntimeError(
                "Could not locate InstructorFailurePreset 14.21.8 tail anchor."
            )
        changed = True

    missing_labels = [p for p in PRESETS if p[2] not in text]
    if missing_labels:
        anchor = re.compile(
            r'(\s*case\s+InstructorFailurePreset\.LightingEssBreakerTripped:'
            r'\s*return\s+"POWER - LIGHTING ESS BREAKER TRIPPED";)'
        )
        insertion = "".join(
            f'\n                case InstructorFailurePreset.{name}: return "{label}";'
            for name, _, label, _ in missing_labels
        )
        text, count = anchor.subn(
            lambda m: m.group(1) + insertion,
            text,
            count=1,
        )
        if count != 1:
            raise RuntimeError(
                "Could not locate Lighting ESS breaker label anchor."
            )
        changed = True

    return text, changed

def patch_console_form(text):
    changed = False

    missing_conditions = [
        p for p in PRESETS
        if f"InstructorFailurePreset.{p[0]}" not in text
    ]
    if missing_conditions:
        anchor = re.compile(
            r"(preset\s*==\s*\n?\s*"
            r"InstructorFailurePreset\.LightingEssBreakerTripped\s*\|\|)"
        )
        insertion = "".join(
            f"\n                        preset ==\n"
            f"                            InstructorFailurePreset.{name} ||"
            for name, _, _, _ in missing_conditions
        )
        text, count = anchor.subn(
            lambda m: m.group(1) + insertion,
            text,
            count=1,
        )
        if count != 1:
            raise RuntimeError(
                "Could not locate Lighting ESS routing-condition anchor."
            )
        changed = True

    missing_cases = [
        p for p in PRESETS
        if f"case InstructorFailurePreset.{p[0]}:" not in text
    ]
    if missing_cases:
        anchor = re.compile(
            r"(\s*case\s+InstructorFailurePreset\.GuidABreakerTripped:\s*\n"
            r'\s*switchId\s*=\s*"BRK_GUID_A";)'
        )
        insertion = ""
        for name, _, _, switch_id in missing_cases:
            insertion += (
                f"\n                            case InstructorFailurePreset.{name}:\n"
                f'                                switchId = "{switch_id}";\n'
                f"                                switchMode =\n"
                f"                                    SyntheticElectricalSwitchFailureMode.TrippedOpen;\n"
                f"                                break;"
            )
        text, count = anchor.subn(
            lambda m: insertion + m.group(1),
            text,
            count=1,
        )
        if count != 1:
            raise RuntimeError(
                "Could not locate GUID A breaker switch-mapping anchor."
            )
        changed = True

    return text, changed

def validate(model_text, form_text):
    for name, value, label, switch_id in PRESETS:
        if f"{name} = {value}" not in model_text:
            raise RuntimeError(f"Missing enum value for {name}.")
        if label not in model_text:
            raise RuntimeError(f"Missing F10 label for {name}.")

        pattern = (
            rf"case InstructorFailurePreset\.{re.escape(name)}:.*?"
            rf'switchId\s*=\s*"{re.escape(switch_id)}";.*?'
            rf"SyntheticElectricalSwitchFailureMode\.TrippedOpen"
        )
        if not re.search(pattern, form_text, re.DOTALL):
            raise RuntimeError(
                f"{name} is not mapped to {switch_id} with TrippedOpen."
            )

    all_ids_found = set(
        re.findall(
            r'switchId\s*=\s*"(BRK_[A-Z0-9_]+)";.*?'
            r"SyntheticElectricalSwitchFailureMode\.TrippedOpen",
            form_text,
            re.DOTALL,
        )
    )
    missing_ids = sorted(ALL_BREAKER_IDS - all_ids_found)
    if missing_ids:
        raise RuntimeError(
            "F10 breaker coverage incomplete after patch: "
            + ", ".join(missing_ids)
        )

    if "InstructorElectricalSourceFailureBridge" not in form_text:
        raise RuntimeError("Existing switch failure bridge not found.")
    if ".InjectSwitchFailure(" not in form_text:
        raise RuntimeError("Existing InjectSwitchFailure path not found.")

def main():
    for path in (MODEL, FORM):
        if not path.exists():
            raise SystemExit(f"Missing required file: {path}")

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
        "14.21.9 applied: complete F10 breaker-trip coverage for all 20 live breakers."
        if (mc or fc)
        else "14.21.9 already applied; no changes needed."
    )
    print("KSP Plugin DLL Required? NO")

if __name__ == "__main__":
    main()
