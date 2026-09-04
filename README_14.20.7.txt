KMC 14.20.7 — FINAL DE IVA EXCEPTIONS & COVERAGE CLOSURE
========================================================

BASELINE
--------
Frozen source baseline: 14.20.6
571b0da553c65e5e9625208b90dea6f75bbbeec6

PURPOSE
-------
Close the final three SPECIAL_REVIEW props from the 14.20.2 IVA audit by
recording deliberate exceptions.  No KSP runtime behavior is added in this
milestone.

DECISIONS
---------
1. ASET_Flashlight
   INTENTIONAL INDEPENDENT-POWER EXCEPTION.
   Handheld/self-contained light; modeled as using batteries independent of the
   spacecraft electrical buses. KMC does not gate it.

2. MonitorDockingMode
   INTENTIONAL STOCK EXCEPTION.
   Stock internalGeneric prop has no safe supported power-control API. KMC leaves
   it native rather than introducing renderer/material hacks.

3. kOSTerminal
   OPTIONAL-MOD EXCEPTION.
   The terminal belongs to optional Probe Control Room / kOSPropMonitor
   integration rather than the core supported spacecraft IVA set. KMC core does
   not patch or gate it.

ADD
---
Tools/IvaCoverageAudit/apply_14_20_7_classifications.py
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_7.py
README_14.20.7.txt

REPLACE
-------
Tools/IvaCoverageAudit/tests/test_classify_review_props.py
Tools/IvaCoverageAudit/prop_classifications.csv
  (updated by the guarded classification patch command below)

REMOVE
------
GameData/KMC/IVA/KmcRpmSpecialDisplays14_20_7.cfg
GameData/KMC/IVA/Profiles/DE_IVAExtension/KmcProfile_DE_MissionControl.cfg

These two files were part of the interim 14.20.7 attempt and must not remain in
final 14.20.7.

INSTALL INTO REPOSITORY
-----------------------
Extract this ZIP into the KMC repository root:
C:\Users\mobil\source\repos\KMC

Then run:
python Tools/IvaCoverageAudit/apply_14_20_7_classifications.py

Expected after the interim 14.20.7 classification state:
14.20.7 classifications updated: kOSTerminal

If running directly from frozen 14.20.6, all three names may be listed.
The command is guarded and idempotent.

AUTOMATED TEST
--------------
From repository root:
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

Expected total on the 14.20.6 baseline plus final 14.20.7:
42 tests, all OK.

Re-run classification using the real audit data:
python Tools/IvaCoverageAudit/classify_review_props.py --review-props IvaAuditOutput/ReviewProps.csv --output-dir IvaClassificationOutput

Expected:
Classified 181 unique REVIEW prop(s). Reports written to IvaClassificationOutput

Then inspect:
IvaClassificationOutput\NewElectricalReview.csv

Expected: header only / zero SPECIAL_REVIEW props.

RUNTIME TEST
------------
None required. Final 14.20.7 changes classification/tooling documentation only.
No GameData runtime patch is part of the final milestone.

DO NOT PUSH until the full automated suite and classification rerun pass.

KSP Plugin DLL Required? NO

No runtime C# or GameData behavior is changed in final 14.20.7.
