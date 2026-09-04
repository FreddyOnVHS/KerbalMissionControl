# KMC 14.20.1 — IVA Coverage Audit & Batch Framework Design

## Goal

Bring the remaining supported cockpits/capsules to the Mk1 reference-cockpit level as efficiently as possible by auditing all IVA configs first, reusing proven Mk1 RPM/ASET support wherever possible, and investigating only genuinely new electrical props.

## Frozen baseline

14.19.1 — commit `879e066c9be1ea31fecd8926d7a27760edcda9ef`.

The proven Mk1 implementation is the reference behavior. Do not change its runtime architecture during this milestone.

## Scope of 14.20.1

14.20.1 is audit/tooling only. It does not change cockpit behavior, KSP plugin behavior, or electrical authority rules.

The milestone will:

1. Inventory the existing KMC IVA support rules and Mk1 profile mappings.
2. Scan target DE/RPM/ASET IVA configuration files.
3. Extract PROP usage per cockpit/IVA.
4. Classify each prop as:
   - `SUPPORTED` — covered by an existing KMC generic rule or proven Mk1 family.
   - `REVIEW` — electrical-looking prop not yet covered by KMC.
   - `IGNORE` — decorative/non-electrical/mechanical prop outside the current basic electrical-simulation scope.
5. Produce a Cockpit Coverage Matrix.
6. Produce an Unknown/Review Prop Report sorted by reuse opportunity so one new prop family can be solved once and applied to every cockpit that uses it.
7. Produce a recommended implementation batch order.

## Efficiency strategy

Do not research each cockpit prop-by-prop in KSP.

The Mk1 cockpit is the reference implementation. Existing KMC support for RPM MFD power, passive ASET instruments, digital indicators, annunciators, backlighting, bus assignment, restoration, and fail-open behavior is reused wherever the same prop families appear.

Only `REVIEW` props receive manual investigation.

## Target implementation order after the audit

The audit will recommend exact batches based on overlap, but the preferred starting order is:

1. Mk1-3 Command Pod + Mk1 Lander Can + Cupola
2. Mk2 Lander Can and remaining command capsules
3. Mk2 Cockpit + Mk2 Inline and remaining aircraft IVAs
4. Remaining DE/ASET exceptions

If the matrix shows a different grouping reduces new-prop work, the matrix wins.

## Audit tool

Create a small repository-local development tool under `Tools/IvaCoverageAudit/`.

The tool must be dependency-light and run without KSP. Python 3 standard library only is preferred so the user does not need NuGet packages, PowerShell scripts, or special tooling.

Inputs:

- repository `GameData/KMC/IVA` directory
- one or more IVA config roots supplied on the command line

Outputs:

- `CockpitCoverageMatrix.csv`
- `CockpitCoverageMatrix.md`
- `ReviewProps.csv`
- `AuditSummary.txt`

The tool must never modify input config files.

## Classification behavior

Classification must be deterministic and conservative.

A prop is `SUPPORTED` only when the audit can match it to an explicit known KMC rule/profile/family.

A prop is `IGNORE` only when it is explicitly listed as non-electrical/decorative/mechanical.

Everything else is `REVIEW`.

Unknown props must never be silently assumed supported.

## Coverage matrix fields

Each cockpit row must contain at least:

- IVA/internal name
- config source file
- total PROP instances
- unique PROP names
- supported unique props
- review unique props
- ignored unique props
- support percentage excluding ignored props
- review prop names
- suggested batch

## Tests

The package must include automated tests runnable without KSP.

Required cases:

- parse a simple IVA with multiple PROP blocks
- count duplicate PROP instances correctly
- classify explicit supported props correctly
- classify explicit ignored props correctly
- classify unknown props as REVIEW
- preserve deterministic output ordering
- handle comments/whitespace/nested config blocks safely enough for KSP cfg files used by the project
- never modify scanned files
- produce CSV and Markdown reports from fixture data

The README must include one command to run the tests and one command to run the audit.

## Packaging

Deliver as a drag/drop ZIP rooted at repository folders.

README must state:

- ADD
- REPLACE
- REMOVE
- test procedure
- how to run the audit
- expected output files
- what files are intentionally untouched

No PowerShell/BAT/manual XML editing.

## Non-goals

14.20.1 does not:

- change KMC.Plugin
- change KSP runtime authority
- add new RPM/ASET electrical patches
- modify the Mk1 reference cockpit
- add detailed RCS modeling
- attempt one-prop-at-a-time runtime research

## Success criteria

14.20.1 is complete when the audit tool passes its automated tests and can generate a repeatable coverage matrix from supplied IVA config roots, clearly separating already-covered props from the small set requiring manual review.

## KSP Plugin DLL Required?

NO.
