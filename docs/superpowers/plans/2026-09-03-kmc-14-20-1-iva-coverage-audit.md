# KMC 14.20.1 IVA Coverage Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Add a dependency-free repository-local audit tool that inventories IVA PROP usage, derives known KMC-supported prop names from existing KMC IVA configs, conservatively classifies unknown props, and emits repeatable coverage reports without changing KSP runtime behavior.

**Architecture:** `iva_coverage_audit.py` parses KSP CFG text with a brace-aware scanner, derives supported prop names from KMC ModuleManager `@PROP[...]` selectors, applies an explicit ignore list, and generates CSV/Markdown/text reports. Tests use only Python `unittest` and fixture CFG files, so KSP and third-party Python packages are not required.

**Tech Stack:** Python 3 standard library (`argparse`, `csv`, `pathlib`, `re`, `unittest`).

**Spec:** `docs/superpowers/specs/2026-09-03-kmc-14-20-1-iva-coverage-audit-design.md`

## Global Constraints

- 14.20.1 is audit/tooling only; do not change KSP runtime behavior.
- Do not modify the Mk1 reference cockpit or existing KMC IVA configs.
- Classification is conservative: unknown props are `REVIEW`.
- Inputs are read-only.
- No PowerShell, BAT, NuGet, or third-party Python dependencies.
- Outputs: `CockpitCoverageMatrix.csv`, `CockpitCoverageMatrix.md`, `ReviewProps.csv`, `AuditSummary.txt`.
- KSP Plugin DLL Required? NO.

---

### Task 1: Parser and classifier

**Files:**
- Create: `Tools/IvaCoverageAudit/iva_coverage_audit.py`
- Create: `Tools/IvaCoverageAudit/tests/test_iva_coverage_audit.py`
- Create: `Tools/IvaCoverageAudit/tests/fixtures/target/*.cfg`
- Create: `Tools/IvaCoverageAudit/tests/fixtures/kmc_iva/Profiles/*.cfg`

**Interfaces:**
- Produces: `parse_cfg_file(path)`, `discover_supported_props(kmc_root)`, `load_ignore_props(path)`, `classify_prop(name, supported, ignored)`.

- [x] Write tests for duplicate PROP instance counting, comments/whitespace, internal names, supported selectors, ignored names, and unknown-as-REVIEW.
- [x] Run `python -m unittest discover -s Tools/IvaCoverageAudit/tests -v` and verify RED because production module is absent.
- [x] Implement the minimum parser/classifier.
- [x] Re-run tests and verify GREEN.

### Task 2: Coverage aggregation and deterministic batching

**Files:**
- Modify: `Tools/IvaCoverageAudit/iva_coverage_audit.py`
- Modify: `Tools/IvaCoverageAudit/tests/test_iva_coverage_audit.py`

**Interfaces:**
- Produces: `audit_roots(kmc_root, target_roots, ignore_file) -> list[dict]`, deterministic `suggest_batches(rows)`.

- [x] Add tests for deterministic row ordering, support percentage excluding ignored props, repeated props, and batch labels.
- [x] Verify RED.
- [x] Implement aggregation and deterministic overlap-based batch assignment.
- [x] Verify GREEN.

### Task 3: Report writers and command-line interface

**Files:**
- Modify: `Tools/IvaCoverageAudit/iva_coverage_audit.py`
- Modify: `Tools/IvaCoverageAudit/tests/test_iva_coverage_audit.py`
- Create: `Tools/IvaCoverageAudit/ignore_props.txt`

**Interfaces:**
- Produces: four required output files and CLI arguments `--kmc-iva-root`, one-or-more `--iva-root`, `--output-dir`, optional `--ignore-file`.

- [x] Add tests that reports are generated from fixtures and input file hashes/content remain unchanged.
- [x] Verify RED.
- [x] Implement CSV, Markdown, review-prop, summary writers and CLI.
- [x] Verify GREEN.

### Task 4: Operator documentation and package verification

**Files:**
- Create: `README_14.20.1.txt`
- Create: `Tools/IvaCoverageAudit/README.txt`

- [x] Document exact ADD / REPLACE / REMOVE instructions.
- [x] Document one-command automated test procedure.
- [x] Document audit command examples for DE/RPM/ASET config roots.
- [x] State expected output files and intentionally untouched runtime files.
- [x] Run the complete unit suite.
- [x] Run the CLI against fixtures and verify all four reports are produced.
- [x] Confirm ZIP contains only intended additions and documentation.
