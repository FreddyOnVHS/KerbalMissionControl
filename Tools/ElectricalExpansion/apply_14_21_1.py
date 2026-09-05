from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[2]
DIST = ROOT / "KMC.Engine" / "SpacecraftSystems" / "ElectricalDistributionSystem.cs"
FOUNDATION = ROOT / "KMC.Engine" / "SpacecraftSystems" / "SpacecraftSystemsFoundationSystem.cs"

NEW_LOADS = '            AddLoad(\n                distribution,\n                "FLIGHT_CONTROL",\n                "SAS / FLIGHT CONTROL ELECTRONICS",\n                "BUS_ESS",\n                1.0,\n                1);\n            AddLoad(\n                distribution,\n                "REACTION_WHEEL",\n                "REACTION WHEEL POWER",\n                "BUS_ESS",\n                1.0,\n                1);\n            AddLoad(\n                distribution,\n                "ENGINE_CONTROL",\n                "ENGINE CONTROL / IGNITION",\n                "BUS_ESS",\n                0.75,\n                1);\n            AddLoad(\n                distribution,\n                "STAGING_CONTROL",\n                "STAGING / SEPARATION",\n                "BUS_ESS",\n                0.25,\n                1);\n            AddLoad(\n                distribution,\n                "BRAKE_CONTROL",\n                "BRAKE CONTROL",\n                "BUS_ESS",\n                0.5,\n                1);\n            AddLoad(\n                distribution,\n                "GEAR_CONTROL",\n                "GEAR CONTROL / ACTUATION",\n                "BUS_ESS",\n                0.5,\n                1);\n            AddLoad(\n                distribution,\n                "LIGHTING_ESS",\n                "EXTERNAL / EMERGENCY LIGHTING",\n                "BUS_ESS",\n                0.5,\n                1);'
NEW_COMPONENTS = '            AddComponent(\n                model,\n                "FLIGHT_CONTROL",\n                "SAS / FLIGHT CONTROL ELECTRONICS",\n                SpacecraftSystemCategory.Guidance);\n            AddComponent(\n                model,\n                "REACTION_WHEEL",\n                "REACTION WHEEL POWER",\n                SpacecraftSystemCategory.Guidance);\n            AddComponent(\n                model,\n                "ENGINE_CONTROL",\n                "ENGINE CONTROL / IGNITION",\n                SpacecraftSystemCategory.Propulsion);\n            AddComponent(\n                model,\n                "STAGING_CONTROL",\n                "STAGING / SEPARATION",\n                SpacecraftSystemCategory.Propulsion);\n            AddComponent(\n                model,\n                "BRAKE_CONTROL",\n                "BRAKE CONTROL",\n                SpacecraftSystemCategory.Guidance);\n            AddComponent(\n                model,\n                "GEAR_CONTROL",\n                "GEAR CONTROL / ACTUATION",\n                SpacecraftSystemCategory.Guidance);\n            AddComponent(\n                model,\n                "LIGHTING_ESS",\n                "EXTERNAL / EMERGENCY LIGHTING",\n                SpacecraftSystemCategory.Electrical);'
NEW_DEPENDENCIES = '            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "FLIGHT_CONTROL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "REACTION_WHEEL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "ENGINE_CONTROL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "STAGING_CONTROL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "BRAKE_CONTROL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "GEAR_CONTROL");\n            AddPowerDependency(\n                model,\n                "BUS_ESS",\n                "LIGHTING_ESS");'

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

def patch_feed(text, feed_id):
    pattern = re.compile(
        rf'("{feed_id}"\s*,.*?SyntheticElectricalSourceKind\.BusFeed\s*,\s*)6\.0(\s*,)',
        re.S,
    )
    updated, count = pattern.subn(r'\g<1>12.0\2', text, count=1)
    if count == 0:
        already = re.search(
            rf'"{feed_id}"\s*,.*?SyntheticElectricalSourceKind\.BusFeed\s*,\s*12\.0\s*,',
            text,
            re.S,
        )
        if not already:
            raise RuntimeError(f"Could not locate {feed_id} at 6.0 A or 12.0 A.")
        return text, False
    return updated, True

def patch_distribution(text):
    changed = False
    for feed_id in ("FEED_ESS_A", "FEED_ESS_B"):
        text, did = patch_feed(text, feed_id)
        changed |= did

    if '"FLIGHT_CONTROL"' not in text:
        anchor = re.compile(
            r'(AddLoad\(\s*distribution\s*,\s*"INSTRUMENTATION_ESS"\s*,\s*'
            r'"ESS INSTRUMENTATION"\s*,\s*"BUS_ESS"\s*,\s*1\.0\s*,\s*1\s*\);)',
            re.S,
        )
        text, count = anchor.subn(lambda m: m.group(1) + "\n" + NEW_LOADS, text, count=1)
        if count != 1:
            raise RuntimeError("Could not locate the ESS INSTRUMENTATION load anchor.")
        changed = True
    return text, changed

def patch_foundation(text):
    changed = False
    if '"FLIGHT_CONTROL"' not in text:
        anchor = re.compile(
            r'(AddComponent\(\s*model\s*,\s*"FLIGHT_COMPUTER"\s*,\s*'
            r'"PRIMARY FLIGHT COMPUTER"\s*,\s*SpacecraftSystemCategory\.Guidance\s*\);)',
            re.S,
        )
        text, count = anchor.subn(lambda m: m.group(1) + "\n" + NEW_COMPONENTS, text, count=1)
        if count != 1:
            raise RuntimeError("Could not locate FLIGHT_COMPUTER component anchor.")
        changed = True

    if not re.search(
        r'AddPowerDependency\(\s*model\s*,\s*"BUS_ESS"\s*,\s*"FLIGHT_CONTROL"\s*\);',
        text,
        re.S,
    ):
        anchor = re.compile(
            r'(AddPowerDependency\(\s*model\s*,\s*"BUS_ESS"\s*,\s*"FLIGHT_COMPUTER"\s*\);)',
            re.S,
        )
        text, count = anchor.subn(lambda m: m.group(1) + "\n" + NEW_DEPENDENCIES, text, count=1)
        if count != 1:
            raise RuntimeError("Could not locate FLIGHT_COMPUTER ESS dependency anchor.")
        changed = True
    return text, changed

def main():
    for path in (DIST, FOUNDATION):
        if not path.exists():
            raise SystemExit(f"Missing required file: {path}")

    dist, dbom, dnl = read_preserving(DIST)
    foundation, fbom, fnl = read_preserving(FOUNDATION)

    dist, dc = patch_distribution(dist)
    foundation, fc = patch_foundation(foundation)

    required = (
        '"FLIGHT_CONTROL"', '"REACTION_WHEEL"', '"ENGINE_CONTROL"',
        '"STAGING_CONTROL"', '"BRAKE_CONTROL"', '"GEAR_CONTROL"', '"LIGHTING_ESS"'
    )
    for token in required:
        if token not in dist or token not in foundation:
            raise RuntimeError(f"Post-patch validation failed: missing {token}.")

    if dc:
        write_preserving(DIST, dist, dbom, dnl)
    if fc:
        write_preserving(FOUNDATION, foundation, fbom, fnl)

    print(
        "14.21.1 applied: 12 A ESS feeds + 7 simulation-only ESS breakers/components."
        if (dc or fc)
        else "14.21.1 already applied; no changes needed."
    )

if __name__ == "__main__":
    main()
