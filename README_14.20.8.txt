KMC BUILD 14.20.8 — FULL DE IVA COVERAGE AUDIT / ACCEPTANCE SWEEP
================================================================

PURPOSE
-------
14.20.8 is an acceptance milestone only. It does not add or change KSP runtime
behavior. It proves that the completed DE IVA electrical-coverage work remains
closed after regenerating the audit/classification outputs from the installed
DE_IVAExtension dataset.

This corrective acceptance package fixes two audit-only issues found during the
first fresh 14.20.8 run:
  1. ALCORMFD60x30 is now discovered as already-supported and therefore no
     longer belongs in ReviewProps.csv or prop_classifications.csv.
  2. The acceptance verifier now compares the classification report directly
     against the fresh ReviewProps.csv set, so stale prior output cannot pass.

ADD
---
README_14.20.8.txt
Tools/IvaCoverageAudit/apply_14_20_8_acceptance_fix.py
Tools/IvaCoverageAudit/verify_14_20_8_acceptance.py
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_8.py

REPLACE
-------
README_14.20.8.txt
Tools/IvaCoverageAudit/verify_14_20_8_acceptance.py
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_8.py

REMOVE
------
No runtime files.
The helper removes only the obsolete ALCORMFD60x30 row from:
Tools/IvaCoverageAudit/prop_classifications.csv

KSP Plugin DLL Required? NO

STEP 0 — APPLY THE ACCEPTANCE-DATA FIX
--------------------------------------
From the KMC repository root:

python Tools/IvaCoverageAudit/apply_14_20_8_acceptance_fix.py

Expected first run:
  14.20.8 acceptance fix applied: removed obsolete ALCORMFD60x30 classification

The helper is idempotent. Re-running it is safe and reports that the row is
already absent. It does not change any other classification row.

STEP 1 — REGENERATE THE IVA AUDIT FROM THE INSTALLED MOD
--------------------------------------------------------
python Tools/IvaCoverageAudit/iva_coverage_audit.py --kmc-iva-root GameData/KMC/IVA --iva-root "D:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\DE_IVAExtension" --output-dir IvaAuditOutput

Expected frozen-dataset headline:
  IVAs scanned: 16

STEP 2 — REGENERATE CLASSIFICATION OUTPUTS
------------------------------------------
python Tools/IvaCoverageAudit/classify_review_props.py --review-props IvaAuditOutput/ReviewProps.csv --output-dir IvaClassificationOutput

This command must complete successfully before the acceptance verifier is run.
Expected:
  Classified 180 unique REVIEW prop(s).

STEP 3 — RUN THE 14.20.8 ACCEPTANCE VERIFIER
--------------------------------------------
python Tools/IvaCoverageAudit/verify_14_20_8_acceptance.py

Expected:
  KMC 14.20.8 IVA acceptance: PASS
  IVAs audited: 16
  KMC DE IVA runtime profiles: 15
  Intentional exceptions: 3
  Unresolved SPECIAL_REVIEW props: 0
  Classification decisions: 180

The verifier now requires PropClassificationReport.csv to contain exactly the
same prop-name set as the fresh IvaAuditOutput/ReviewProps.csv. This prevents a
failed/skipped classifier run from being hidden by stale output files.

The three intentional exceptions remain:
  ASET_Flashlight      — independent battery-powered device
  MonitorDockingMode  — stock exception; native behavior untouched
  kOSTerminal         — optional-mod exception; native behavior untouched

DE_MissionControl intentionally has no KMC runtime profile because its remaining
special display is the optional-mod kOSTerminal case closed in 14.20.7.

STEP 4 — RUN THE COMPLETE AUTOMATED TEST SUITE
----------------------------------------------
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

14.20.7 baseline: 42 tests.
The corrected 14.20.8 test module contains 10 acceptance/fix tests.
Expected total after replacement: 52 tests, all PASS.

STEP 5 — REPRESENTATIVE KSP RUNTIME ACCEPTANCE SWEEP
----------------------------------------------------
No new runtime code is installed for this milestone. Use the currently installed
14.20.6/14.20.7 runtime as-is and perform a representative acceptance sweep.

Representative set:
  1. Mk1 Cockpit       — basic reference cockpit
  2. Mk1-3             — command capsule
  3. Mk2 Lander Can    — lander
  4. Cupola            — 60x30 retained display family
  5. Mk2 Cockpit       — aircraft cockpit
  6. Mk3 Cockpit       — large multi-display aircraft cockpit

For each representative IVA verify:
  [ ] Nominal displays / gauges / annunciators render normally.
  [ ] MAIN A loss blacks out only the expected MAIN A powered props.
  [ ] MAIN B loss blacks out only the expected MAIN B powered props.
  [ ] MAIN A + MAIN B loss leaves ESS behavior consistent with schematic truth.
  [ ] When ESS is 0.0 V, ESS-powered displays / gauges / cockpit lighting go dark.
  [ ] CLEAR ALL / electrical restoration returns powered props automatically.
  [ ] Crew command state is preserved across electrical loss/restoration.
  [ ] Command controls remain physically movable while downstream authority may fail.
  [ ] External lighting follows ESS electrical authority and restores correctly.
  [ ] No checkerboards, broken RPM screens, missing props, or renderer/material artifacts.

ACCEPTANCE / FREEZE GATE
------------------------
Do not freeze 14.20.8 until all of the following are true:
  [ ] Acceptance-data fix applied.
  [ ] Fresh audit regenerated from installed DE_IVAExtension.
  [ ] Fresh classification command completed successfully.
  [ ] Acceptance verifier PASS against the fresh ReviewProps.csv set.
  [ ] Complete automated test suite PASS.
  [ ] Representative runtime sweep PASS.
  [ ] git diff --check reports no real whitespace errors.
  [ ] Final diff contains only this acceptance milestone's intended audit/test/output changes.

KSP Plugin DLL Required? NO
