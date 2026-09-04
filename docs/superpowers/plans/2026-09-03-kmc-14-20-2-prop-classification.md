# KMC 14.20.2 IVA Prop Classification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convert the 14.20.1 conservative REVIEW list into one explicit, reusable classification decision per unique prop and generate a small electrical-work shortlist for all 16 audited IVAs.

**Architecture:** Keep KSP runtime untouched. Add a standard-library Python classifier beside the 14.20.1 audit tool, backed by an explicit CSV decision table rather than fuzzy runtime inference. It consumes `ReviewProps.csv` and emits deterministic classification/workload reports; missing or duplicate decisions are hard failures.

**Tech Stack:** Python 3 standard library (`argparse`, `csv`, `collections`, `pathlib`, `unittest`).

**Spec:** Approved in-chat 14.20.2 scope: classify all REVIEW props once, separate static/non-display controls from reusable electrical families and genuinely special electrical review, then batch implementation later.

## Global Constraints

- GitHub `master` remains source of truth for runtime KMC selectors.
- Do not modify KSP runtime CFG, C# source, or DLL behavior in 14.20.2.
- Classification must be explicit and deterministic; no unreviewed fuzzy classification may silently become supported.
- Preserve command controls as controls: do not electrically disable cockpit buttons/switches merely because a bus is dead.
- Reports must make any missing classification visible as a failure.

---

### Task 1: Classification engine and hard validation

**Files:**
- Create: `Tools/IvaCoverageAudit/classify_review_props.py`
- Create: `Tools/IvaCoverageAudit/tests/test_classify_review_props.py`

**Interfaces:**
- Consumes: `ReviewProps.csv`, `prop_classifications.csv`
- Produces: deterministic classified rows and four report files.

- [ ] Write failing tests for full coverage, duplicate-decision rejection, unknown-input rejection, deterministic report ordering, and per-IVA workload counts.
- [ ] Run the new test module and verify RED because the classifier module does not exist.
- [ ] Implement the smallest classifier API and CLI required by the tests.
- [ ] Run the complete `Tools/IvaCoverageAudit/tests` suite and verify GREEN.

### Task 2: Explicit 181-prop decision table

**Files:**
- Create: `Tools/IvaCoverageAudit/prop_classifications.csv`

**Interfaces:**
- Produces one exact decision for every prop in the supplied 14.20.1 `ReviewProps.csv`.

- [ ] Populate explicit rows with categories `IGNORE_STATIC`, `CONTROL_NO_BLACKOUT`, `REUSE_ANNUNCIATOR`, `REUSE_DIGITAL`, `REUSE_PASSIVE`, `REUSE_DISPLAY`, and `SPECIAL_REVIEW`.
- [ ] Validate the table against the user-provided 181-name `ReviewProps.csv`; missing=0, extra=0, duplicates=0.
- [ ] Generate classification reports from the real audit data.

### Task 3: Documentation and package

**Files:**
- Replace: `Tools/IvaCoverageAudit/README.txt`
- Create: `README_14.20.2.txt`

- [ ] Document ADD/REPLACE/REMOVE and exact commands using the user's repository and KSP paths.
- [ ] Document category semantics and why command controls are not display-blackout targets.
- [ ] Run full tests fresh and package repository-root ZIP without caches or generated user-specific reports.
