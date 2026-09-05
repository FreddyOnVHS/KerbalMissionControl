from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]

SHARED = ROOT / "KMC.shared" / "SystemAuthorityPacket.cs"
GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
PLUGIN = ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"
PRIOR_TEST = ROOT / "Tools" / "ElectricalExpansion" / "tests" / "test_14_21_2_flight_control_authority.py"

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
    if re.search(r"EngineControl\s*=\s*5", text):
        return text, False
    pattern = re.compile(r"(ReactionWheels\s*=\s*4)(\s*\n\s*\})")
    text, count = pattern.subn(r"\1,\n        EngineControl = 5\2", text, count=1)
    if count != 1:
        raise RuntimeError("14.21.3 requires frozen 14.21.2 with ReactionWheels = 4.")
    return text, True

def patch_gnc(text):
    changed = False

    if '"ENGINE_CONTROL"' not in text:
        marker = '''            bool? reactionWheelPowered =
                ResolveElectricalLoadPower(
                    result,
                    "REACTION_WHEEL");'''
        replacement = marker + '''
            bool? engineControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "ENGINE_CONTROL");'''
        if marker not in text:
            raise RuntimeError("Could not locate 14.21.2 reaction-wheel power preamble.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "SystemAuthorityKind.EngineControl" not in text:
        marker = '''                    SystemAuthorityKind.Sas,
                    SystemAuthorityKind.ReactionWheels,
                    SystemAuthorityKind.Gear,'''
        replacement = '''                    SystemAuthorityKind.Sas,
                    SystemAuthorityKind.ReactionWheels,
                    SystemAuthorityKind.EngineControl,
                    SystemAuthorityKind.Gear,'''
        if marker not in text:
            raise RuntimeError("Could not locate 14.21.2 authority array.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "bool electricalEngineControlInhibit" not in text:
        marker = """                bool electricalReactionWheelInhibit =
                    authority ==
                        SystemAuthorityKind.ReactionWheels &&
                    reactionWheelPowered.HasValue &&
                    !reactionWheelPowered.Value;"""
        compact = """                bool electricalReactionWheelInhibit =
                    authority == SystemAuthorityKind.ReactionWheels &&
                    reactionWheelPowered.HasValue &&
                    !reactionWheelPowered.Value;"""
        addition = """

                bool electricalEngineControlInhibit =
                    authority ==
                        SystemAuthorityKind.EngineControl &&
                    engineControlPowered.HasValue &&
                    !engineControlPowered.Value;"""
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError("Could not locate reaction-wheel inhibit.")
        changed = True

    if "electricalEngineControlInhibit ||" not in text:
        marker = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalLightsInhibit;'''
        replacement = '''                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalEngineControlInhibit ||
                    electricalLightsInhibit;'''
        if marker not in text:
            raise RuntimeError("Could not locate 14.21.2 inhibitDesired.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if '"ENGINE CONTROL ELECTRICAL POWER LOST"' not in text:
        marker = '''                        else if (
                            electricalReactionWheelInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "REACTION WHEEL ELECTRICAL POWER LOST";
                        }'''
        replacement = marker + '''
                        else if (
                            electricalEngineControlInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "ENGINE CONTROL ELECTRICAL POWER LOST";
                        }'''
        if marker not in text:
            compact = '''                    else if (electricalReactionWheelInhibit && !explicitInhibit)
                        reason = "REACTION WHEEL ELECTRICAL POWER LOST";'''
            compact_replacement = compact + '''
                    else if (electricalEngineControlInhibit && !explicitInhibit)
                        reason = "ENGINE CONTROL ELECTRICAL POWER LOST";'''
            if compact not in text:
                raise RuntimeError("Could not locate reaction-wheel reason block.")
            text = text.replace(compact, compact_replacement, 1)
        else:
            text = text.replace(marker, replacement, 1)
        changed = True

    return text, changed

def patch_plugin(text):
    changed = False

    if "TryInhibitEngine(" not in text:
        marker = '''                    if (!MatchesAuthority(
                            module,
                            state.Authority))
                    {
                        continue;
                    }'''
        compact = '''                    if (!MatchesAuthority(module, state.Authority))
                        continue;'''
        insertion = '''
                    if (state.Authority ==
                            SystemAuthorityKind.EngineControl &&
                        TryInhibitEngine(
                            module,
                            state))
                    {
                        continue;
                    }
'''
        if marker in text:
            text = text.replace(marker, marker + insertion, 1)
        elif compact in text:
            text = text.replace(compact, compact + insertion, 1)
        else:
            raise RuntimeError("Could not locate DiscoverAndInhibit authority match.")
        changed = True

    if "case SystemAuthorityKind.EngineControl:" not in text:
        marker = '''                case SystemAuthorityKind.ReactionWheels:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleReactionWheel");'''
        compact = '''                case SystemAuthorityKind.ReactionWheels:
                    return IsName(moduleName, typeName, "ModuleReactionWheel");'''
        addition = '''
                case SystemAuthorityKind.EngineControl:
                    return
                        IsEngineModule(
                            module);'''
        if marker in text:
            text = text.replace(marker, marker + addition, 1)
        elif compact in text:
            text = text.replace(compact, compact + addition, 1)
        else:
            raise RuntimeError("Could not locate ReactionWheels authority match.")
        changed = True

    if "private static bool IsEngineModule(" not in text:
        marker = '''        private static void GateModuleActionsAndEvents(
            PartModule module,
            LeaseState state)'''
        compact = '''        private static void GateModuleActionsAndEvents(PartModule module, LeaseState state)'''
        helper = '''        private static bool IsEngineModule(
            PartModule module)
        {
            return
                module is ModuleEngines;
        }

        private static bool TryInhibitEngine(
            PartModule module,
            LeaseState state)
        {
            ModuleEngines engine =
                module as ModuleEngines;

            if (engine == null ||
                state == null)
            {
                return false;
            }

            GateEngineStartCommands(
                engine,
                state);

            try
            {
                if (engine.EngineIgnited)
                {
                    engine.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] Engine-control shutdown failed: " +
                    ex.GetType().Name);
            }

            return true;
        }

        private static void GateEngineStartCommands(
            ModuleEngines engine,
            LeaseState state)
        {
            if (engine == null ||
                state == null)
            {
                return;
            }

            try
            {
                if (engine.Actions != null)
                {
                    foreach (BaseAction action in
                        engine.Actions)
                    {
                        if (action == null)
                            continue;

                        bool isStartAction =
                            string.Equals(
                                action.name,
                                "ActivateAction",
                                StringComparison.Ordinal) ||
                            string.Equals(
                                action.name,
                                "OnAction",
                                StringComparison.Ordinal);

                        if (!isStartAction)
                            continue;

                        if (!state.PriorActionActive
                                .ContainsKey(action))
                        {
                            state.PriorActionActive[action] =
                                action.active;
                        }

                        action.active = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] Engine-control action gate failed: " +
                    ex.GetType().Name);
            }

            try
            {
                if (engine.Events != null)
                {
                    foreach (BaseEvent evt in
                        engine.Events)
                    {
                        if (evt == null ||
                            !string.Equals(
                                evt.name,
                                "Activate",
                                StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (!state.PriorEventActive
                                .ContainsKey(evt))
                        {
                            state.PriorEventActive[evt] =
                                evt.active;
                        }

                        evt.active = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[KMC] Engine-control event gate failed: " +
                    ex.GetType().Name);
            }
        }

'''
        if marker in text:
            text = text.replace(marker, helper + marker, 1)
        elif compact in text:
            text = text.replace(compact, helper + compact, 1)
        else:
            raise RuntimeError("Could not locate GateModuleActionsAndEvents.")
        changed = True

    return text, changed

def patch_prior_test(text):
    changed = False
    old_multiline = '''            '"ENGINE_CONTROL"',
            '"STAGING_CONTROL",'''
    if old_multiline in text:
        text = text.replace(old_multiline, '''            '"STAGING_CONTROL",''', 1)
        changed = True

    old_compact = '''('"ENGINE_CONTROL"', '"STAGING_CONTROL"', '''
    if old_compact in text:
        text = text.replace(old_compact, '''('"STAGING_CONTROL"', ''', 1)
        changed = True

    return text, changed

def validate(shared, gnc, plugin):
    required = (
        (shared, ("ReactionWheels = 4", "EngineControl = 5"), "shared"),
        (gnc, ('"ENGINE_CONTROL"', "SystemAuthorityKind.EngineControl",
               "electricalEngineControlInhibit",
               '"ENGINE CONTROL ELECTRICAL POWER LOST"'), "gnc"),
        (plugin, ("SystemAuthorityKind.EngineControl",
                  "module is ModuleEngines",
                  "ModuleEngines engine",
                  "engine.Shutdown()",
                  '"ActivateAction"',
                  '"OnAction"',
                  '"Activate"'), "plugin"),
    )
    for text, tokens, label in required:
        for token in tokens:
            if token not in text:
                raise RuntimeError(
                    "14.21.3 validation failed in " + label + ": " + token
                )

    restore_index = plugin.find("private static void RestoreState")
    if restore_index >= 0:
        restore_text = plugin[restore_index:]
        if ".Activate()" in restore_text:
            raise RuntimeError(
                "14.21.3 validation failed: restore path auto-activates engine."
            )

def main():
    for path in (SHARED, GNC, PLUGIN, PRIOR_TEST):
        if not path.exists():
            raise SystemExit("Missing required file: " + str(path))

    shared, sb, sn = read_preserving(SHARED)
    gnc, gb, gn = read_preserving(GNC)
    plugin, pb, pn = read_preserving(PLUGIN)
    prior, tb, tn = read_preserving(PRIOR_TEST)

    shared, sc = patch_shared(shared)
    gnc, gc = patch_gnc(gnc)
    plugin, pc = patch_plugin(plugin)
    prior, tc = patch_prior_test(prior)

    validate(shared, gnc, plugin)

    if sc:
        write_preserving(SHARED, shared, sb, sn)
    if gc:
        write_preserving(GNC, gnc, gb, gn)
    if pc:
        write_preserving(PLUGIN, plugin, pb, pn)
    if tc:
        write_preserving(PRIOR_TEST, prior, tb, tn)

    if sc or gc or pc or tc:
        print(
            "14.21.3 applied: ENGINE_CONTROL shuts down "
            "ModuleEngines-derived engines without disabling their PartModules."
        )
    else:
        print("14.21.3 already applied; no changes needed.")

if __name__ == "__main__":
    main()
