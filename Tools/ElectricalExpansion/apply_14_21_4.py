from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
SHARED = ROOT / "KMC.shared" / "SystemAuthorityPacket.cs"
GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
PLUGIN = ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"
TEST_14212 = ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_2_flight_control_authority.py"
TEST_14213 = ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_3_engine_control_authority.py"

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

def patch_shared(text):
    if "StagingControl = 6" in text:
        return text, False
    pattern = re.compile(r"(EngineControl\s*=\s*5)(\s*\n\s*\})")
    text, count = pattern.subn(r"\1,\n        StagingControl = 6\2", text, count=1)
    if count != 1:
        raise RuntimeError("Expected EngineControl = 5 baseline not found.")
    return text, True

def patch_gnc(text):
    changed = False

    if '"STAGING_CONTROL"' not in text:
        marker = '''            bool? engineControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "ENGINE_CONTROL");'''
        replacement = marker + '''
            bool? stagingControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "STAGING_CONTROL");'''
        if marker not in text:
            raise RuntimeError("ENGINE_CONTROL power anchor not found.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "SystemAuthorityKind.StagingControl" not in text:
        marker = '''                    SystemAuthorityKind.ReactionWheels,
                    SystemAuthorityKind.EngineControl,
                    SystemAuthorityKind.Gear,'''
        replacement = '''                    SystemAuthorityKind.ReactionWheels,
                    SystemAuthorityKind.EngineControl,
                    SystemAuthorityKind.StagingControl,
                    SystemAuthorityKind.Gear,'''
        if marker not in text:
            raise RuntimeError("Authority array anchor not found.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "bool electricalStagingControlInhibit" not in text:
        marker = '''                bool electricalEngineControlInhibit =
                    authority ==
                        SystemAuthorityKind.EngineControl &&
                    engineControlPowered.HasValue &&
                    !engineControlPowered.Value;'''
        compact = '''                bool electricalEngineControlInhibit =
                    authority == SystemAuthorityKind.EngineControl &&
                    engineControlPowered.HasValue &&
                    !engineControlPowered.Value;'''
        addition = '''

                bool electricalStagingControlInhibit =
                    authority ==
                        SystemAuthorityKind.StagingControl &&
                    stagingControlPowered.HasValue &&
                    !stagingControlPowered.Value;'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError("ENGINE_CONTROL inhibit anchor not found.")
        changed = True

    if "electricalStagingControlInhibit ||" not in text:
        marker = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalEngineControlInhibit ||
                    electricalLightsInhibit;'''
        replacement = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalEngineControlInhibit ||
                    electricalStagingControlInhibit ||
                    electricalLightsInhibit;'''
        if marker not in text:
            raise RuntimeError("inhibitDesired anchor not found.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if '"STAGING / SEPARATION ELECTRICAL POWER LOST"' not in text:
        marker = '''                        else if (
                            electricalEngineControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "ENGINE CONTROL ELECTRICAL POWER LOST";
                        }'''
        compact = '''                    else if (electricalEngineControlInhibit && !explicitInhibit)
                        reason = "ENGINE CONTROL ELECTRICAL POWER LOST";'''
        if marker in text:
            addition = '''
                        else if (
                            electricalStagingControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "STAGING / SEPARATION ELECTRICAL POWER LOST";
                        }'''
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            addition = '''
                    else if (electricalStagingControlInhibit && !explicitInhibit)
                        reason = "STAGING / SEPARATION ELECTRICAL POWER LOST";'''
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError("ENGINE_CONTROL reason anchor not found.")
        changed = True

    return text, changed

def patch_plugin(text):
    changed = False

    if "StagingInputLockId" not in text:
        marker = '''        private const float LeaseSeconds =
            2.50f;'''
        compact = '''        private const float LeaseSeconds = 2.50f;'''
        addition = '''
        private const string StagingInputLockId =
            "KMC.SYSTEM_AUTHORITY.STAGING";
'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError("LeaseSeconds anchor not found.")
        changed = True

    if "SetStagingInputLock(true)" not in text:
        marker = '''        private static void DiscoverAndInhibit(Vessel vessel, LeaseState state)
        {'''
        expanded = '''        private static void DiscoverAndInhibit(
            Vessel vessel,
            LeaseState state)
        {'''
        addition = '''
            if (state != null &&
                state.Authority == SystemAuthorityKind.StagingControl)
            {
                SetStagingInputLock(true);
            }
'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif expanded in text:
            text = text.replace(expanded, expanded + addition, 1)
        else:
            raise RuntimeError("DiscoverAndInhibit anchor not found.")
        changed = True

    if "GateSeparationCommands(" not in text:
        marker = '''                    if (state.Authority ==
                            SystemAuthorityKind.EngineControl &&
                        TryInhibitEngine(
                            module,
                            state))
                    {
                        continue;
                    }'''
        addition = '''

                    if (state.Authority ==
                            SystemAuthorityKind.StagingControl)
                    {
                        GateSeparationCommands(
                            module,
                            state);
                        continue;
                    }'''
        if marker not in text:
            raise RuntimeError("EngineControl special handling anchor not found.")
        text = text.replace(marker, marker + addition, 1)
        changed = True

    if "case SystemAuthorityKind.StagingControl:" not in text:
        marker = '''                case SystemAuthorityKind.EngineControl:
                    return
                        IsEngineModule(
                            module);'''
        addition = '''
                case SystemAuthorityKind.StagingControl:
                    return
                        IsSeparationModule(
                            module);'''
        if marker not in text:
            raise RuntimeError("EngineControl case anchor not found.")
        text = text.replace(marker, marker + addition, 1)
        changed = True

    if "private static bool IsSeparationModule(" not in text:
        marker = '''        private static bool TryInhibitEngine(
            PartModule module,
            LeaseState state)'''
        helper = '''        private static bool IsSeparationModule(
            PartModule module)
        {
            if (module == null)
            {
                return false;
            }

            return
                module is ModuleDecouplerBase ||
                module is ModuleAnchoredDecoupler ||
                module is ModuleDockingNode;
        }

        private static void SetStagingInputLock(bool locked)
        {
            try
            {
                if (locked)
                {
                    InputLockManager.SetControlLock(
                        ControlTypes.STAGING,
                        StagingInputLockId);
                }
                else
                {
                    InputLockManager.RemoveControlLock(
                        StagingInputLockId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] Staging input lock update failed: " +
                    ex.GetType().Name);
            }
        }

        private static void GateSeparationCommands(
            PartModule module,
            LeaseState state)
        {
            if (module == null || state == null)
            {
                return;
            }

            if (!state.PriorStagingEnabled.ContainsKey(module))
            {
                state.PriorStagingEnabled[module] =
                    module.stagingEnabled;
            }

            module.stagingEnabled = false;

            if (module.Actions != null)
            {
                foreach (BaseAction action in module.Actions)
                {
                    if (action == null)
                        continue;

                    bool separationAction =
                        string.Equals(
                            action.name,
                            "DecoupleAction",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            action.name,
                            "UndockAction",
                            StringComparison.Ordinal);

                    if (!separationAction)
                        continue;

                    if (!state.PriorActionActive.ContainsKey(action))
                    {
                        state.PriorActionActive[action] =
                            action.active;
                    }

                    action.active = false;
                }
            }

            if (module.Events != null)
            {
                foreach (BaseEvent evt in module.Events)
                {
                    if (evt == null)
                        continue;

                    bool separationEvent =
                        string.Equals(
                            evt.name,
                            "Decouple",
                            StringComparison.Ordinal) ||
                        string.Equals(
                            evt.name,
                            "Undock",
                            StringComparison.Ordinal);

                    if (!separationEvent)
                        continue;

                    if (!state.PriorEventActive.ContainsKey(evt))
                    {
                        state.PriorEventActive[evt] =
                            evt.active;
                    }

                    evt.active = false;
                }
            }
        }

'''
        if marker not in text:
            raise RuntimeError("TryInhibitEngine anchor not found.")
        text = text.replace(marker, helper + marker, 1)
        changed = True

    if "PriorStagingEnabled" not in text:
        lease_idx = text.find("class LeaseState")
        if lease_idx < 0:
            raise RuntimeError("LeaseState not found.")
        brace_idx = text.find("{", lease_idx)
        if brace_idx < 0:
            raise RuntimeError("LeaseState brace not found.")
        addition = '''
            public readonly Dictionary<PartModule, bool>
                PriorStagingEnabled =
                    new Dictionary<PartModule, bool>();
'''
        text = text[:brace_idx + 1] + addition + text[brace_idx + 1:]
        changed = True

    restore_idx = text.find("private static void RestoreState")
    restore_text = text[restore_idx:] if restore_idx >= 0 else ""
    if "state.PriorStagingEnabled" not in restore_text:
        marker = '''        private static void RestoreState(LeaseState state)
        {'''
        expanded = '''        private static void RestoreState(
            LeaseState state)
        {'''
        addition = '''
            SetStagingInputLock(false);

            foreach (KeyValuePair<PartModule, bool> pair in
                state.PriorStagingEnabled)
            {
                PartModule module = pair.Key;
                if (module != null)
                {
                    module.stagingEnabled = pair.Value;
                }
            }
'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif expanded in text:
            text = text.replace(expanded, expanded + addition, 1)
        else:
            raise RuntimeError("RestoreState anchor not found.")
        changed = True

    return text, changed

def patch_prior_test(text):
    target = '            \'"STAGING_CONTROL"\',\n'
    if target in text:
        return text.replace(target, "", 1), True

    compact = '\'"STAGING_CONTROL"\', '
    if compact in text:
        return text.replace(compact, "", 1), True

    return text, False

def validate(shared, gnc, plugin):
    for token in ("StagingControl = 6",):
        if token not in shared:
            raise RuntimeError("shared validation failed: " + token)

    for token in (
        '"STAGING_CONTROL"',
        "SystemAuthorityKind.StagingControl",
        "electricalStagingControlInhibit",
        '"STAGING / SEPARATION ELECTRICAL POWER LOST"',
    ):
        if token not in gnc:
            raise RuntimeError("GNC validation failed: " + token)

    for token in (
        "ControlTypes.STAGING",
        "InputLockManager.SetControlLock",
        "InputLockManager.RemoveControlLock",
        "module is ModuleDecouplerBase",
        "module is ModuleAnchoredDecoupler",
        "module is ModuleDockingNode",
        '"DecoupleAction"',
        '"UndockAction"',
        "PriorStagingEnabled",
    ):
        if token not in plugin:
            raise RuntimeError("plugin validation failed: " + token)

    restore_idx = plugin.find("private static void RestoreState")
    if restore_idx >= 0:
        restore = plugin[restore_idx:]
        for forbidden in (".Decouple()", ".Undock()", "ActivateNextStage"):
            if forbidden in restore:
                raise RuntimeError(
                    "Restore path must not replay separation: " + forbidden
                )

def main():
    for path in (SHARED, GNC, PLUGIN, TEST_14212, TEST_14213):
        if not path.exists():
            raise SystemExit("Missing required file: " + str(path))

    shared, sb, sn = read_preserving(SHARED)
    gnc, gb, gn = read_preserving(GNC)
    plugin, pb, pn = read_preserving(PLUGIN)
    t12, t12b, t12n = read_preserving(TEST_14212)
    t13, t13b, t13n = read_preserving(TEST_14213)

    shared, sc = patch_shared(shared)
    gnc, gc = patch_gnc(gnc)
    plugin, pc = patch_plugin(plugin)
    t12, c12 = patch_prior_test(t12)
    t13, c13 = patch_prior_test(t13)

    validate(shared, gnc, plugin)

    if sc:
        write_preserving(SHARED, shared, sb, sn)
    if gc:
        write_preserving(GNC, gnc, gb, gn)
    if pc:
        write_preserving(PLUGIN, plugin, pb, pn)
    if c12:
        write_preserving(TEST_14212, t12, t12b, t12n)
    if c13:
        write_preserving(TEST_14213, t13, t13b, t13n)

    if sc or gc or pc or c12 or c13:
        print(
            "14.21.4 applied: STAGING_CONTROL now gates staging, "
            "stock decouplers, and docking-port separation commands."
        )
    else:
        print("14.21.4 already applied; no changes needed.")

if __name__ == "__main__":
    main()
