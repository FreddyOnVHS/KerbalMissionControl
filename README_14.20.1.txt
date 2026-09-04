KMC BUILD 14.20.1 — IVA COVERAGE AUDIT & BATCH FRAMEWORK
=========================================================

BASELINE
--------
Designed against the frozen 14.19.1 source state and current master IVA
layout. This package contains development/audit tooling only.

ADD
---
Tools/IvaCoverageAudit/iva_coverage_audit.py
Tools/IvaCoverageAudit/ignore_props.txt
Tools/IvaCoverageAudit/README.txt
Tools/IvaCoverageAudit/tests/test_iva_coverage_audit.py
Tools/IvaCoverageAudit/tests/fixtures/kmc_iva/Profiles/KmcProfile_Test.cfg
Tools/IvaCoverageAudit/tests/fixtures/target/TestCockpit.cfg
Tools/IvaCoverageAudit/tests/fixtures/target/SecondCockpit.cfg
Tools/IvaCoverageAudit/tests/fixtures/multi/MultiInternal.cfg
docs/superpowers/specs/2026-09-03-kmc-14-20-1-iva-coverage-audit-design.md
docs/superpowers/plans/2026-09-03-kmc-14-20-1-iva-coverage-audit.md
README_14.20.1.txt

REPLACE
-------
NONE

REMOVE
------
NONE

INSTALL
-------
Drag/drop the ZIP contents into the KerbalMissionControl repository root.
This adds Tools/IvaCoverageAudit plus the design/plan documentation. It does
not overwrite existing KMC runtime files.

TEST PROCEDURE
--------------
1. Open Command Prompt or your normal terminal in the repository root.
2. Run:

   python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

3. Confirm every test reports "ok" and the final line reports "OK".
4. Optional fixture smoke test:

   python Tools/IvaCoverageAudit/iva_coverage_audit.py --kmc-iva-root Tools/IvaCoverageAudit/tests/fixtures/kmc_iva --iva-root Tools/IvaCoverageAudit/tests/fixtures/target --output-dir IvaAuditFixtureOutput

5. Confirm these four files appear in IvaAuditFixtureOutput:
   CockpitCoverageMatrix.csv
   CockpitCoverageMatrix.md
   ReviewProps.csv
   AuditSummary.txt

RUN AGAINST REAL IVA CONFIGS
----------------------------
Example:

   python Tools/IvaCoverageAudit/iva_coverage_audit.py --kmc-iva-root GameData/KMC/IVA --iva-root "C:\KSP\GameData\DE_IVAExtension" --output-dir IvaAuditOutput

Add more --iva-root arguments if needed for additional IVA config roots.
See Tools/IvaCoverageAudit/README.txt for details.

EXPECTED NEXT STEP
------------------
Send/review CockpitCoverageMatrix.md and ReviewProps.csv. We then solve the
highest-reuse REVIEW prop families first and create the first actual cockpit
implementation batch. Do not research every cockpit one prop at a time.

INTENTIONALLY UNTOUCHED
-----------------------
GameData/KMC/IVA/*
KMC.Plugin/*
KMC.Engine/*
KMC.shared/*
KMC.MissionControl/*

No runtime behavior changes are included in 14.20.1.

KSP Plugin DLL Required? NO
