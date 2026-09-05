from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]

SHARED = ROOT / "KMC.shared" / "SystemAuthorityPacket.cs"
GNC = ROOT / "KMC.MissionControl" / "Engineering" / "GncFailureIntegrationController.cs"
PLUGIN = ROOT / "KMC.Plugin" / "KmcSystemAuthorityReceiver.cs"

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
    if re.search(r"ReactionWheels\s*=\s*4", text):
        return text, False

    pattern = re.compile(r"(Lights\s*=\s*3)(\s*\n\s*\})")
    text, count = pattern.subn(r"\1,\n        ReactionWheels = 4\2", text, count=1)
    if count != 1:
        raise RuntimeError(
            "14.21.2 could not add SystemAuthorityKind.ReactionWheels. "
            "Current SystemAuthorityPacket.cs does not match expected master."
        )
    return text, True

def patch_gnc(text):
    changed = False

    if '"FLIGHT_CONTROL"' not in text:
        marker = """            bool? essPowered =
                ResolveEssElectricalPower(
                    result);"""
        replacement = marker + """
            bool? flightControlPowered =
                ResolveElectricalLoadPower(
                    result,
                    "FLIGHT_CONTROL");
            bool? reactionWheelPowered =
                ResolveElectricalLoadPower(
                    result,
                    "REACTION_WHEEL");"""
        if marker not in text:
            raise RuntimeError("14.21.2 could not locate ESS authority preamble.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "SystemAuthorityKind.ReactionWheels" not in text:
        marker = """                    SystemAuthorityKind.Sas,
                    SystemAuthorityKind.Gear,"""
        replacement = """                    SystemAuthorityKind.Sas,
                    SystemAuthorityKind.ReactionWheels,
                    SystemAuthorityKind.Gear,"""
        if marker not in text:
            raise RuntimeError("14.21.2 could not locate system authority array.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "bool electricalSasInhibit" not in text:
        marker = """                bool electricalLightsInhibit =
                    authority ==
                        SystemAuthorityKind.Lights &&
                    essPowered.HasValue &&
                    !essPowered.Value;

                bool inhibitDesired =
                    explicitInhibit ||
                    electricalLightsInhibit;"""
        replacement = """                bool electricalSasInhibit =
                    authority ==
                        SystemAuthorityKind.Sas &&
                    flightControlPowered.HasValue &&
                    !flightControlPowered.Value;

                bool electricalReactionWheelInhibit =
                    authority ==
                        SystemAuthorityKind.ReactionWheels &&
                    reactionWheelPowered.HasValue &&
                    !reactionWheelPowered.Value;

                bool electricalLightsInhibit =
                    authority ==
                        SystemAuthorityKind.Lights &&
                    essPowered.HasValue &&
                    !essPowered.Value;

                bool inhibitDesired =
                    explicitInhibit ||
                    electricalSasInhibit ||
                    electricalReactionWheelInhibit ||
                    electricalLightsInhibit;"""
        if marker not in text:
            raise RuntimeError(
                "14.21.2 could not locate the existing electrical lights inhibit block."
            )
        text = text.replace(marker, replacement, 1)
        changed = True

    if '"FLIGHT CONTROL ELECTRICAL POWER LOST"' not in text:
        marker = """                        string reason =
                            state.Active
                                ? "LEASE REFRESH"
                                : electricalLightsInhibit &&
                                  !explicitInhibit
                                    ? "ESS ELECTRICAL POWER LOST"
                                    : "KMC AUTHORITY INHIBIT";"""
        replacement = """                        string reason;
                        if (state.Active)
                        {
                            reason =
                                "LEASE REFRESH";
                        }
                        else if (
                            electricalSasInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "FLIGHT CONTROL ELECTRICAL POWER LOST";
                        }
                        else if (
                            electricalReactionWheelInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "REACTION WHEEL ELECTRICAL POWER LOST";
                        }
                        else if (
                            electricalLightsInhibit &&
                            !explicitInhibit)
                        {
                            reason =
                                "ESS ELECTRICAL POWER LOST";
                        }
                        else
                        {
                            reason =
                                "KMC AUTHORITY INHIBIT";
                        }"""
        if marker not in text:
            raise RuntimeError("14.21.2 could not locate authority reason block.")
        text = text.replace(marker, replacement, 1)
        changed = True

    if "private static bool? ResolveElectricalLoadPower(" not in text:
        marker = """        private static bool? ResolveEssElectricalPower(
            AnalysisPipelineResult result)"""
        helper = """        private static bool? ResolveElectricalLoadPower(
            AnalysisPipelineResult result,
            string equipmentId)
        {
            bool? essPowered =
                ResolveEssElectricalPower(
                    result);
            if (!essPowered.HasValue)
            {
                return null;
            }

            SyntheticElectricalDistributionModel distribution =
                result != null &&
                result.Snapshot != null &&
                result.Snapshot.SpacecraftSystems != null
                    ? result.Snapshot.SpacecraftSystems
                        .ElectricalDistribution
                    : null;

            if (distribution == null ||
                string.IsNullOrWhiteSpace(
                    equipmentId))
            {
                return null;
            }

            SyntheticElectricalLoad load =
                null;

            for (int index = 0;
                 index < distribution.Loads.Count;
                 index++)
            {
                SyntheticElectricalLoad candidate =
                    distribution.Loads[index];

                if (candidate != null &&
                    string.Equals(
                        candidate.EquipmentId,
                        equipmentId,
                        StringComparison.Ordinal))
                {
                    load =
                        candidate;
                    break;
                }
            }

            if (load == null ||
                !string.Equals(
                    load.BusId,
                    "BUS_ESS",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(
                    load.BreakerId))
            {
                return null;
            }

            SyntheticElectricalSwitch breaker =
                distribution.FindSwitch(
                    load.BreakerId);

            if (breaker == null)
            {
                return null;
            }

            bool powered =
                load.CommandedOn &&
                !load.AutomaticallyShed &&
                breaker.Conducting &&
                essPowered.Value;

            return powered;
        }

"""
        if marker not in text:
            raise RuntimeError("14.21.2 could not locate ResolveEssElectricalPower.")
        text = text.replace(marker, helper + marker, 1)
        changed = True

    return text, changed

def patch_plugin(text):
    if "case SystemAuthorityKind.ReactionWheels:" in text:
        return text, False

    marker = """                case SystemAuthorityKind.Sas:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleSAS");"""
    replacement = marker + """
                case SystemAuthorityKind.ReactionWheels:
                    return
                        IsName(
                            moduleName,
                            typeName,
                            "ModuleReactionWheel");"""
    if marker not in text:
        raise RuntimeError(
            "14.21.2 could not locate the SAS authority match in "
            "KmcSystemAuthorityReceiver.cs."
        )
    text = text.replace(marker, replacement, 1)
    return text, True

def validate(shared, gnc, plugin):
    checks = {
        "shared": (shared, ("ReactionWheels = 4",)),
        "GNC": (
            gnc,
            (
                '"FLIGHT_CONTROL"',
                '"REACTION_WHEEL"',
                "SystemAuthorityKind.ReactionWheels",
                "electricalSasInhibit",
                "electricalReactionWheelInhibit",
                "ResolveElectricalLoadPower",
                '"FLIGHT CONTROL ELECTRICAL POWER LOST"',
                '"REACTION WHEEL ELECTRICAL POWER LOST"',
            ),
        ),
        "plugin": (
            plugin,
            (
                "SystemAuthorityKind.ReactionWheels",
                '"ModuleReactionWheel"',
                "LeaseSeconds",
                "RestoreState",
            ),
        ),
    }

    for label, pair in checks.items():
        text = pair[0]
        tokens = pair[1]
        for token in tokens:
            if token not in text:
                raise RuntimeError(
                    "14.21.2 post-patch validation failed in " +
                    label +
                    ": missing " +
                    token
                )

def main():
    for path in (SHARED, GNC, PLUGIN):
        if not path.exists():
            raise SystemExit("Missing required file: " + str(path))

    shared, shared_bom, shared_nl = read_preserving(SHARED)
    gnc, gnc_bom, gnc_nl = read_preserving(GNC)
    plugin, plugin_bom, plugin_nl = read_preserving(PLUGIN)

    shared, shared_changed = patch_shared(shared)
    gnc, gnc_changed = patch_gnc(gnc)
    plugin, plugin_changed = patch_plugin(plugin)

    validate(shared, gnc, plugin)

    if shared_changed:
        write_preserving(SHARED, shared, shared_bom, shared_nl)
    if gnc_changed:
        write_preserving(GNC, gnc, gnc_bom, gnc_nl)
    if plugin_changed:
        write_preserving(PLUGIN, plugin, plugin_bom, plugin_nl)

    if shared_changed or gnc_changed or plugin_changed:
        print(
            "14.21.2 applied: FLIGHT_CONTROL -> SAS authority; "
            "REACTION_WHEEL -> vessel-wide reaction-wheel authority."
        )
    else:
        print("14.21.2 already applied; no changes needed.")

if __name__ == "__main__":
    main()
