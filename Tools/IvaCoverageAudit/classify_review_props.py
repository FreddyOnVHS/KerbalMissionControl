#!/usr/bin/env python3
"""KMC 14.20.2 explicit IVA prop classification tool.

Consumes the conservative 14.20.1 ReviewProps.csv and an explicit one-row-per-
prop decision table.  It refuses incomplete, duplicate, or stale decision data
so classifications cannot silently drift.
"""
from __future__ import annotations

import argparse
import csv
from collections import Counter, defaultdict
from pathlib import Path

VALID_CATEGORIES = {
    "REFERENCE_BASELINE",
    "IGNORE_STATIC",
    "CONTROL_NO_BLACKOUT",
    "REUSE_ANNUNCIATOR",
    "REUSE_DIGITAL",
    "REUSE_PASSIVE",
    "REUSE_DISPLAY",
    "SPECIAL_REVIEW",
}
REUSE_CATEGORIES = {
    "REUSE_ANNUNCIATOR",
    "REUSE_DIGITAL",
    "REUSE_PASSIVE",
    "REUSE_DISPLAY",
}


class ClassificationError(ValueError):
    pass


def load_review_rows(path: Path) -> list[dict]:
    with Path(path).open(newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    required = {"prop_name", "iva_count", "instance_count", "ivas"}
    if not rows and required:
        return []
    if rows and not required.issubset(rows[0]):
        raise ClassificationError(f"Review CSV missing columns: {sorted(required - set(rows[0]))}")
    names = [r["prop_name"].strip() for r in rows]
    dupes = sorted(n for n, c in Counter(names).items() if c > 1)
    if dupes:
        raise ClassificationError(f"Duplicate review props: {', '.join(dupes)}")
    normalized = []
    for row in rows:
        normalized.append({
            "prop_name": row["prop_name"].strip(),
            "iva_count": int(row["iva_count"]),
            "instance_count": int(row["instance_count"]),
            "ivas": [x.strip() for x in row["ivas"].split(";") if x.strip()],
        })
    return normalized


def load_classifications(path: Path) -> dict[str, dict]:
    with Path(path).open(newline="", encoding="utf-8-sig") as f:
        rows = list(csv.DictReader(f))
    required = {"prop_name", "category", "family", "rationale"}
    if rows and not required.issubset(rows[0]):
        raise ClassificationError(f"Classification CSV missing columns: {sorted(required - set(rows[0]))}")
    result: dict[str, dict] = {}
    for row in rows:
        name = row["prop_name"].strip()
        if not name:
            raise ClassificationError("Classification contains a blank prop_name")
        if name in result:
            raise ClassificationError(f"Duplicate classification for {name}")
        category = row["category"].strip()
        if category not in VALID_CATEGORIES:
            raise ClassificationError(f"Invalid category {category!r} for {name}")
        result[name] = {
            "prop_name": name,
            "category": category,
            "family": row["family"].strip(),
            "rationale": row["rationale"].strip(),
        }
    return result


def classify_review_rows(review_rows: list[dict], classifications: dict[str, dict]) -> list[dict]:
    review_names = {r["prop_name"] for r in review_rows}
    class_names = set(classifications)
    missing = sorted(review_names - class_names, key=str.casefold)
    extra = sorted(class_names - review_names, key=str.casefold)
    if missing or extra:
        pieces = []
        if missing:
            pieces.append("missing classifications: " + ", ".join(missing))
        if extra:
            pieces.append("stale/extra classifications: " + ", ".join(extra))
        raise ClassificationError("; ".join(pieces))
    result = []
    for source in review_rows:
        decision = classifications[source["prop_name"]]
        row = dict(source)
        row.update(decision)
        result.append(row)
    result.sort(key=lambda r: r["prop_name"].casefold())
    return result


def _write_csv(path: Path, fieldnames: list[str], rows: list[dict]) -> None:
    with path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            writer.writerow({k: row.get(k, "") for k in fieldnames})


def write_classification_reports(rows: list[dict], output_dir: Path) -> None:
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    public_rows = []
    for r in sorted(rows, key=lambda x: x["prop_name"].casefold()):
        public_rows.append({
            "prop_name": r["prop_name"],
            "category": r["category"],
            "family": r["family"],
            "iva_count": r["iva_count"],
            "instance_count": r["instance_count"],
            "ivas": "; ".join(r["ivas"]) if isinstance(r["ivas"], list) else r["ivas"],
            "rationale": r["rationale"],
        })
    fields = ["prop_name", "category", "family", "iva_count", "instance_count", "ivas", "rationale"]
    _write_csv(output_dir / "PropClassificationReport.csv", fields, public_rows)

    specials = [r for r in public_rows if r["category"] == "SPECIAL_REVIEW"]
    _write_csv(output_dir / "NewElectricalReview.csv", fields, specials)

    by_iva = defaultdict(lambda: Counter())
    for r in rows:
        for iva in r["ivas"]:
            c = by_iva[iva]
            c["review_props_total"] += 1
            if r["category"] == "REFERENCE_BASELINE":
                c["reference_baseline_props"] += 1
            elif r["category"] == "IGNORE_STATIC":
                c["ignore_static_props"] += 1
            elif r["category"] == "CONTROL_NO_BLACKOUT":
                c["control_no_blackout_props"] += 1
            elif r["category"] in REUSE_CATEGORIES:
                c["reuse_electrical_props"] += 1
            elif r["category"] == "SPECIAL_REVIEW":
                c["special_review_props"] += 1
    workload_rows = []
    for iva in sorted(by_iva, key=str.casefold):
        c = by_iva[iva]
        workload_rows.append({
            "iva_internal": iva,
            "review_props_total": c["review_props_total"],
            "reference_baseline_props": c["reference_baseline_props"],
            "ignore_static_props": c["ignore_static_props"],
            "control_no_blackout_props": c["control_no_blackout_props"],
            "reuse_electrical_props": c["reuse_electrical_props"],
            "special_review_props": c["special_review_props"],
        })
    workload_fields = ["iva_internal", "review_props_total", "reference_baseline_props", "ignore_static_props", "control_no_blackout_props", "reuse_electrical_props", "special_review_props"]
    _write_csv(output_dir / "CockpitWorkload.csv", workload_fields, workload_rows)

    counts = Counter(r["category"] for r in rows)
    md = [
        "# KMC 14.20.2 Prop Classification Summary",
        "",
        f"Unique REVIEW props classified: **{len(rows)}**",
        "",
        "| Category | Unique props |",
        "|---|---:|",
    ]
    for category in sorted(VALID_CATEGORIES):
        md.append(f"| {category} | {counts[category]} |")
    md.extend([
        "",
        f"Special electrical/manual review remaining: **{counts['SPECIAL_REVIEW']}**",
        "",
        "`CONTROL_NO_BLACKOUT` means the cockpit control itself remains operable/animated; downstream hardware authority is handled separately by KMC/KSP.",
    ])
    (output_dir / "PropClassificationSummary.md").write_text("\n".join(md) + "\n", encoding="utf-8")


def main() -> int:
    parser = argparse.ArgumentParser(description="Classify KMC IVA REVIEW props using an explicit decision table.")
    parser.add_argument("--review-props", required=True, type=Path, help="14.20.1 ReviewProps.csv")
    parser.add_argument("--classifications", type=Path, default=Path(__file__).with_name("prop_classifications.csv"))
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()
    try:
        rows = classify_review_rows(load_review_rows(args.review_props), load_classifications(args.classifications))
        write_classification_reports(rows, args.output_dir)
    except (OSError, ClassificationError, ValueError) as exc:
        print(f"Classification failed: {exc}")
        return 2
    print(f"Classified {len(rows)} unique REVIEW prop(s). Reports written to {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
