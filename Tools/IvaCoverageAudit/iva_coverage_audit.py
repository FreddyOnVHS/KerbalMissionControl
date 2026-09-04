#!/usr/bin/env python3
"""KMC 14.20.1 IVA coverage audit.

Read-only scanner for KSP/ModuleManager CFG files.  It inventories PROP usage in
supplied IVA roots, derives explicitly-supported PROP names from the existing
KMC IVA config tree, and emits deterministic coverage reports.
"""
from __future__ import annotations

import argparse
import csv
import re
from collections import Counter, defaultdict, deque
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence


@dataclass(frozen=True)
class ParsedIva:
    internal_name: str
    source_file: Path
    prop_instances: list[str]


def _strip_line_comments(text: str) -> str:
    return "\n".join(line.split("//", 1)[0] for line in text.splitlines())


def _extract_blocks(text: str, keyword: str) -> list[str]:
    """Return contents of balanced ``KEYWORD { ... }`` blocks.

    The scanner intentionally targets ordinary KSP config nodes and not
    ModuleManager selectors such as ``@PROP[Name]``.
    """
    pattern = re.compile(rf"(?<![@+!%$\w]){re.escape(keyword)}\s*\{{", re.IGNORECASE)
    blocks: list[str] = []
    for match in pattern.finditer(text):
        open_brace = text.find("{", match.start())
        depth = 0
        for i in range(open_brace, len(text)):
            ch = text[i]
            if ch == "{":
                depth += 1
            elif ch == "}":
                depth -= 1
                if depth == 0:
                    blocks.append(text[open_brace + 1 : i])
                    break
    return blocks


def _first_name_assignment(block: str, fallback: str) -> str:
    match = re.search(r"(?im)(?:^|[{};])\s*name\s*=\s*([^\s{}]+)", block)
    if not match:
        # Normal KSP configs put assignments on their own line.  This fallback
        # also accepts the common ``PROP { name = Foo }`` compact form.
        match = re.search(r"(?i)\bname\s*=\s*([^\s{}]+)", block)
    return match.group(1).strip() if match else fallback


def parse_cfg_ivas(path: Path) -> list[ParsedIva]:
    path = Path(path)
    text = _strip_line_comments(path.read_text(encoding="utf-8", errors="replace"))
    internals = _extract_blocks(text, "INTERNAL")
    blocks = internals if internals else [text]
    results: list[ParsedIva] = []
    for index, internal_block in enumerate(blocks, start=1):
        fallback = path.stem if len(blocks) == 1 else f"{path.stem}#{index}"
        internal_name = _first_name_assignment(internal_block, fallback)
        props: list[str] = []
        for block in _extract_blocks(internal_block, "PROP"):
            name = _first_name_assignment(block, "")
            if name:
                props.append(name)
        if props or internals:
            results.append(ParsedIva(internal_name=internal_name, source_file=path, prop_instances=props))
    return results


def parse_cfg_file(path: Path) -> ParsedIva:
    results = parse_cfg_ivas(path)
    if results:
        return results[0]
    return ParsedIva(internal_name=Path(path).stem, source_file=Path(path), prop_instances=[])


def discover_supported_props(kmc_root: Path) -> set[str]:
    """Derive explicit supported PROP names from KMC ``@PROP[Name]`` selectors."""
    supported: set[str] = set()
    selector = re.compile(r"@PROP\s*\[\s*([^,\]\s]+)", re.IGNORECASE)
    for path in sorted(Path(kmc_root).rglob("*.cfg"), key=lambda p: str(p).lower()):
        text = _strip_line_comments(path.read_text(encoding="utf-8", errors="replace"))
        for match in selector.finditer(text):
            name = match.group(1).strip()
            if name and "*" not in name and "?" not in name:
                supported.add(name)
    return supported


def load_ignore_props(path: Path | None) -> set[str]:
    if path is None:
        return set()
    path = Path(path)
    if not path.exists():
        return set()
    ignored: set[str] = set()
    for raw in path.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.split("#", 1)[0].strip()
        if line:
            ignored.add(line)
    return ignored


def classify_prop(name: str, supported: set[str], ignored: set[str]) -> str:
    if name in supported:
        return "SUPPORTED"
    if name in ignored:
        return "IGNORE"
    return "REVIEW"


def _iter_cfg_files(roots: Sequence[Path]) -> Iterable[Path]:
    seen: set[Path] = set()
    for root in roots:
        root = Path(root)
        paths = [root] if root.is_file() and root.suffix.lower() == ".cfg" else root.rglob("*.cfg")
        for path in sorted(paths, key=lambda p: str(p).lower()):
            resolved = path.resolve()
            if resolved not in seen:
                seen.add(resolved)
                yield path


def audit_roots(
    kmc_root: Path,
    target_roots: Sequence[Path],
    ignore_file: Path | None = None,
    ignored: set[str] | None = None,
) -> list[dict]:
    supported = discover_supported_props(Path(kmc_root))
    ignored_names = set(ignored) if ignored is not None else load_ignore_props(ignore_file)
    rows: list[dict] = []

    for path in _iter_cfg_files([Path(p) for p in target_roots]):
        for parsed in parse_cfg_ivas(path):
            if not parsed.prop_instances:
                continue
            unique = sorted(set(parsed.prop_instances), key=str.casefold)
            supported_names = [n for n in unique if classify_prop(n, supported, ignored_names) == "SUPPORTED"]
            review_names = [n for n in unique if classify_prop(n, supported, ignored_names) == "REVIEW"]
            ignored_props = [n for n in unique if classify_prop(n, supported, ignored_names) == "IGNORE"]
            denominator = len(supported_names) + len(review_names)
            pct = round((100.0 * len(supported_names) / denominator), 1) if denominator else 100.0
            rows.append(
                {
                    "iva_internal": parsed.internal_name,
                    "config_source_file": str(path),
                    "total_prop_instances": len(parsed.prop_instances),
                    "unique_prop_names": len(unique),
                    "supported_unique_props": len(supported_names),
                    "review_unique_props": len(review_names),
                    "ignored_unique_props": len(ignored_props),
                    "support_percentage": pct,
                    "review_prop_names": review_names,
                    "suggested_batch": "",
                    "_instances": Counter(parsed.prop_instances),
                }
            )

    rows.sort(key=lambda r: (r["iva_internal"].casefold(), r["config_source_file"].casefold()))
    return rows


def suggest_batches(rows: list[dict]) -> None:
    """Assign deterministic batches using shared REVIEW props as reuse edges."""
    review_sets = [set(r["review_prop_names"]) for r in rows]
    adjacency: list[set[int]] = [set() for _ in rows]
    for i in range(len(rows)):
        for j in range(i + 1, len(rows)):
            if review_sets[i] and review_sets[i].intersection(review_sets[j]):
                adjacency[i].add(j)
                adjacency[j].add(i)

    review_indices = [i for i, s in enumerate(review_sets) if s]
    visited: set[int] = set()
    components: list[list[int]] = []
    for start in review_indices:
        if start in visited:
            continue
        q = deque([start])
        visited.add(start)
        component: list[int] = []
        while q:
            cur = q.popleft()
            component.append(cur)
            for nxt in sorted(adjacency[cur]):
                if nxt not in visited:
                    visited.add(nxt)
                    q.append(nxt)
        component.sort(key=lambda idx: rows[idx]["iva_internal"].casefold())
        components.append(component)

    components.sort(key=lambda comp: rows[comp[0]]["iva_internal"].casefold())
    for number, comp in enumerate(components, start=1):
        for idx in comp:
            rows[idx]["suggested_batch"] = f"BATCH-{number}"
    for i, row in enumerate(rows):
        if not review_sets[i]:
            row["suggested_batch"] = "READY"


def _public_row(row: dict) -> dict:
    return {k: v for k, v in row.items() if not k.startswith("_")}


def write_reports(rows: list[dict], output_dir: Path) -> None:
    output_dir = Path(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    fields = [
        "iva_internal",
        "config_source_file",
        "total_prop_instances",
        "unique_prop_names",
        "supported_unique_props",
        "review_unique_props",
        "ignored_unique_props",
        "support_percentage",
        "review_prop_names",
        "suggested_batch",
    ]

    with (output_dir / "CockpitCoverageMatrix.csv").open("w", newline="", encoding="utf-8") as f:
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for row in rows:
            public = _public_row(row)
            public["review_prop_names"] = "; ".join(public["review_prop_names"])
            writer.writerow(public)

    md_lines = [
        "# KMC Cockpit Coverage Matrix",
        "",
        "| IVA/Internal | Source | PROP instances | Unique | Supported | Review | Ignored | Coverage | Review props | Batch |",
        "|---|---|---:|---:|---:|---:|---:|---:|---|---|",
    ]
    for row in rows:
        md_lines.append(
            "| {iva} | `{source}` | {instances} | {unique} | {supported} | {review} | {ignored} | {pct:.1f}% | {props} | {batch} |".format(
                iva=row["iva_internal"],
                source=row["config_source_file"].replace("|", "\\|"),
                instances=row["total_prop_instances"],
                unique=row["unique_prop_names"],
                supported=row["supported_unique_props"],
                review=row["review_unique_props"],
                ignored=row["ignored_unique_props"],
                pct=row["support_percentage"],
                props=", ".join(row["review_prop_names"]) or "—",
                batch=row["suggested_batch"],
            )
        )
    (output_dir / "CockpitCoverageMatrix.md").write_text("\n".join(md_lines) + "\n", encoding="utf-8")

    review_usage: dict[str, dict] = defaultdict(lambda: {"ivas": set(), "instances": 0})
    for row in rows:
        for prop in row["review_prop_names"]:
            review_usage[prop]["ivas"].add(row["iva_internal"])
            review_usage[prop]["instances"] += row["_instances"].get(prop, 0)
    with (output_dir / "ReviewProps.csv").open("w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        writer.writerow(["prop_name", "iva_count", "instance_count", "ivas"])
        ranked = sorted(
            review_usage.items(),
            key=lambda item: (-len(item[1]["ivas"]), -item[1]["instances"], item[0].casefold()),
        )
        for prop, data in ranked:
            writer.writerow([prop, len(data["ivas"]), data["instances"], "; ".join(sorted(data["ivas"], key=str.casefold))])

    total_unique_supported = sum(r["supported_unique_props"] for r in rows)
    total_unique_review = sum(r["review_unique_props"] for r in rows)
    total_unique_ignored = sum(r["ignored_unique_props"] for r in rows)
    ready = sum(1 for r in rows if r["suggested_batch"] == "READY")
    batches = sorted({r["suggested_batch"] for r in rows if r["suggested_batch"].startswith("BATCH-")})
    summary = [
        "KMC 14.20.1 IVA COVERAGE AUDIT SUMMARY",
        "=====================================",
        f"IVAs scanned: {len(rows)}",
        f"Supported unique PROP occurrences by IVA: {total_unique_supported}",
        f"Review unique PROP occurrences by IVA: {total_unique_review}",
        f"Ignored unique PROP occurrences by IVA: {total_unique_ignored}",
        f"IVAs ready with no review props: {ready}",
        f"Suggested review batches: {len(batches)}",
        "",
        "Classification rule: SUPPORTED only from explicit KMC @PROP selectors;",
        "IGNORE only from the explicit ignore list; everything else is REVIEW.",
    ]
    (output_dir / "AuditSummary.txt").write_text("\n".join(summary) + "\n", encoding="utf-8")


def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Audit KSP IVA PROP coverage against existing KMC IVA support.")
    p.add_argument("--kmc-iva-root", required=True, type=Path, help="Repository GameData/KMC/IVA directory")
    p.add_argument("--iva-root", required=True, action="append", type=Path, help="IVA config root to scan; repeat as needed")
    p.add_argument("--output-dir", required=True, type=Path, help="Directory for generated reports")
    p.add_argument("--ignore-file", type=Path, default=Path(__file__).with_name("ignore_props.txt"), help="Explicit IGNORE prop list")
    return p


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    for root in [args.kmc_iva_root, *args.iva_root]:
        if not root.exists():
            raise SystemExit(f"Input path does not exist: {root}")
    rows = audit_roots(args.kmc_iva_root, args.iva_root, ignore_file=args.ignore_file)
    suggest_batches(rows)
    write_reports(rows, args.output_dir)
    print(f"Audited {len(rows)} IVA config(s). Reports written to {args.output_dir}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
