from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
PRIOR_TESTS = (
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_2_flight_control_authority.py",
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_3_engine_control_authority.py",
    ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_4_staging_separation_authority.py",
)

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

def patch_gnc(text):
    changed = False

    if '"GEAR_CONTROL"' not in text:
        marker = '''            bool? stagingControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "STAGING_CONTROL");'''
        addition = '''
            bool? gearControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "GEAR_CONTROL");
            bool? brakeControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "BRAKE_CONTROL");'''
        if marker not in text:
            raise RuntimeError(
                "14.21.5 could not locate the frozen 14.21.4 "
                "STAGING_CONTROL power preamble."
            )
        text = text.replace(marker, marker + addition, 1)
        changed = True

    if "bool electricalGearControlInhibit" not in text:
        marker = '''                bool electricalStagingControlInhibit =
                    authority ==
                        SystemAuthorityKind.StagingControl &&
                    stagingControlPowered.HasValue &&
                    !stagingControlPowered.Value;'''
        compact = '''                bool electricalStagingControlInhibit =
                    authority == SystemAuthorityKind.StagingControl &&
                    stagingControlPowered.HasValue &&
                    !stagingControlPowered.Value;'''
        addition = '''

                bool electricalGearControlInhibit =
                    authority ==
                        SystemAuthorityKind.Gear &&
                    gearControlPowered.HasValue &&
                    !gearControlPowered.Value;

                bool electricalBrakeControlInhibit =
                    authority ==
                        SystemAuthorityKind.Brakes &&
                    brakeControlPowered.HasValue &&
                    !brakeControlPowered.Value;'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError(
                "14.21.5 could not locate the frozen 14.21.4 "
                "staging electrical inhibit."
            )
        changed = True

    if "electricalGearControlInhibit ||" not in text:
        marker = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalEngineControlInhibit ||
                    electricalStagingControlInhibit ||
                    electricalLightsInhibit;'''
        replacement = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalEngineControlInhibit ||
                    electricalStagingControlInhibit ||
                    electricalGearControlInhibit ||
                    electricalBrakeControlInhibit ||
                    electricalLightsInhibit;'''
        if marker not in text:
            raise RuntimeError(
                "14.21.5 could not locate the frozen 14.21.4 "
                "inhibitDesired expression."
            )
        text = text.replace(marker, replacement, 1)
        changed = True

    if '"GEAR CONTROL ELECTRICAL POWER LOST"' not in text:
        marker = '''                        else if (
                            electricalStagingControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "STAGING / SEPARATION ELECTRICAL POWER LOST";
                        }'''
        compact = '''                    else if (electricalStagingControlInhibit && !explicitInhibit)
                        reason = "STAGING / SEPARATION ELECTRICAL POWER LOST";'''

        addition = '''
                        else if (
                            electricalGearControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "GEAR CONTROL ELECTRICAL POWER LOST";
                        }
                        else if (
                            electricalBrakeControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "BRAKE CONTROL ELECTRICAL POWER LOST";
                        }'''
        compact_addition = '''
                    else if (electricalGearControlInhibit && !explicitInhibit)
                        reason = "GEAR CONTROL ELECTRICAL POWER LOST";
                    else if (electricalBrakeControlInhibit && !explicitInhibit)
                        reason = "BRAKE CONTROL ELECTRICAL POWER LOST";'''

        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + compact_addition, 1)
        else:
            raise RuntimeError(
                "14.21.5 could not locate the frozen 14.21.4 "
                "staging reason block."
            )
        changed = True

    return text, changed

def patch_prior_test(text):
    changed = False
    for equipment_id in ("BRAKE_CONTROL", "GEAR_CONTROL"):
        multiline = '            \'"' + equipment_id + '"\',\n'
        if multiline in text:
            text = text.replace(multiline, "", 1)
            changed = True

        compact = '\'"' + equipment_id + '"\', '
        if compact in text:
            text = text.replace(compact, "", 1)
            changed = True

    return text, changed

def validate(gnc):
    required = (
        '"GEAR_CONTROL"',
        '"BRAKE_CONTROL"',
        "electricalGearControlInhibit",
        "electricalBrakeControlInhibit",
        '"GEAR CONTROL ELECTRICAL POWER LOST"',
        '"BRAKE CONTROL ELECTRICAL POWER LOST"',
    )

    for token in required:
        if token not in gnc:
            raise RuntimeError(
                "14.21.5 validation failed: missing " + token
            )

    if '"LIGHTING_ESS"' in gnc:
        raise RuntimeError(
            "14.21.5 validation failed: LIGHTING_ESS must remain unwired."
        )

def main():
    if not GNC.exists():
        raise SystemExit("Missing required file: " + str(GNC))

    for path in PRIOR_TESTS:
        if not path.exists():
            raise SystemExit("Missing required file: " + str(path))

    gnc, gb, gn = read_preserving(GNC)
    tests = []
    for path in PRIOR_TESTS:
        text, bom, newline = read_preserving(path)
        tests.append([path, text, bom, newline])

    gnc, gnc_changed = patch_gnc(gnc)

    any_test_changed = False
    for item in tests:
        item[1], changed = patch_prior_test(item[1])
        any_test_changed = any_test_changed or changed

    validate(gnc)

    # Write only after all patching and validation succeed.
    if gnc_changed:
        write_preserving(GNC, gnc, gb, gn)

    for path, text, bom, newline in tests:
        original, _, _ = read_preserving(path)
        if text != original:
            write_preserving(path, text, bom, newline)

    if gnc_changed or any_test_changed:
        print(
            "14.21.5 applied: GEAR_CONTROL and BRAKE_CONTROL now "
            "drive the existing KSP gear/brake authority paths."
        )
    else:
        print("14.21.5 already applied; no changes needed.")

if __name__ == "__main__":
    main()
