#!/usr/bin/env python3
"""KMC 14.20.7 guarded closure patch for the final three IVA exceptions.

This final cleanup deliberately leaves all three props outside KMC spacecraft
power authority.  The classifier schema is preserved; family+rationale records
the exact reason for each intentional exception.
"""
from __future__ import annotations

import csv
import sys
from pathlib import Path

OLD_RATIONALE = (
    "Electrically relevant prop without a direct Mk1-reference equivalent; "
    "inspect its native module before assigning a KMC power family."
)

INTERIM_KOS_RATIONALE = (
    "Supported in 14.20.7 as an ESS-powered RPM display using native "
    "RasterPropMonitor power gating and the prop's native JSICallbackAnimator blackout."
)

DECISIONS = {
    "ASET_Flashlight": {
        "accepted_old": [
            ("SPECIAL_REVIEW", "special-display-or-light", OLD_RATIONALE),
            ("IGNORE_STATIC", "independent-device", "Intentional KMC exception: handheld flashlight is modeled as a self-contained battery-powered device independent of spacecraft buses."),
        ],
        "category": "IGNORE_STATIC",
        "family": "independent-device",
        "rationale": (
            "Intentional KMC exception: handheld flashlight is modeled as a "
            "self-contained battery-powered device independent of spacecraft buses."
        ),
    },
    "MonitorDockingMode": {
        "accepted_old": [
            ("SPECIAL_REVIEW", "special-display-or-light", OLD_RATIONALE),
            ("IGNORE_STATIC", "stock-exception", "Intentional KMC exception: stock internalGeneric docking monitor exposes no safe supported electrical-power API; leave native behavior untouched."),
        ],
        "category": "IGNORE_STATIC",
        "family": "stock-exception",
        "rationale": (
            "Intentional KMC exception: stock internalGeneric docking monitor exposes "
            "no safe supported electrical-power API; leave native behavior untouched."
        ),
    },
    "kOSTerminal": {
        "accepted_old": [
            ("SPECIAL_REVIEW", "special-display-or-light", OLD_RATIONALE),
            ("REUSE_DISPLAY", "RPM-MFD", INTERIM_KOS_RATIONALE),
        ],
        "category": "IGNORE_STATIC",
        "family": "optional-mod-exception",
        "rationale": (
            "Intentional KMC optional mod exception: kOSTerminal belongs to the optional "
            "Probe Control Room / kOSPropMonitor integration; KMC core leaves it native."
        ),
    },
}


def patch(path: Path) -> None:
    path = Path(path)
    with path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)
        rows = list(reader)
        fieldnames = list(reader.fieldnames or [])

    required = ["prop_name", "category", "family", "rationale"]
    if fieldnames != required:
        raise SystemExit(f"Unexpected classification columns: {fieldnames!r}")

    by_name = {row["prop_name"]: row for row in rows}
    missing = [name for name in DECISIONS if name not in by_name]
    if missing:
        raise SystemExit("Missing expected classification row(s): " + ", ".join(missing))

    changed = []
    for name, decision in DECISIONS.items():
        row = by_name[name]
        desired = {
            "category": decision["category"],
            "family": decision["family"],
            "rationale": decision["rationale"],
        }
        if all(row[k] == v for k, v in desired.items()):
            continue

        current = (row["category"], row["family"], row["rationale"])
        if current not in decision["accepted_old"]:
            raise SystemExit(
                f"Refusing to patch {name}: local row differs from recognized 14.20.6/14.20.7 states."
            )

        row.update(desired)
        changed.append(name)

    with path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)

    if changed:
        print("14.20.7 classifications updated: " + ", ".join(changed))
    else:
        print("14.20.7 classifications already applied; no changes needed.")


def main() -> int:
    if len(sys.argv) > 2:
        print("Usage: apply_14_20_7_classifications.py [prop_classifications.csv]")
        return 2
    path = Path(sys.argv[1]) if len(sys.argv) == 2 else Path(__file__).with_name("prop_classifications.csv")
    patch(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
