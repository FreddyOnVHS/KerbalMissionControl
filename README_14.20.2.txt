KMC 14.20.2 — IVA PROP CLASSIFICATION & COMMON-COVERAGE PLANNING
================================================================

GOAL
----
Turn the conservative 14.20.1 REVIEW list into a small, explicit worklist so
remaining cockpits can be brought to Mk1-level coverage in batches.

ADD
---
Tools\IvaCoverageAudit\classify_review_props.py
Tools\IvaCoverageAudit\prop_classifications.csv
Tools\IvaCoverageAudit\tests\test_classify_review_props.py
docs\superpowers\plans\2026-09-03-kmc-14-20-2-prop-classification.md
README_14.20.2.txt

REPLACE
-------
Tools\IvaCoverageAudit\README.txt

REMOVE
------
NONE

RUNTIME CHANGES
---------------
NONE. No GameData/KMC runtime CFG, C# source, project file, or DLL is changed.

WHY THE MK1 REFERENCE MATTERS
-----------------------------
The completed DE Mk1 cockpit still contained 105 conservative REVIEW prop
names in the 14.20.1 audit. Those are now explicitly marked
REFERENCE_BASELINE: they do not represent new work needed to make another IVA
match the scope already accepted as complete for Mk1.

The remaining prop names are split into static/mechanical, command controls,
reusable proven electrical families, or SPECIAL_REVIEW.

TEST PROCEDURE
--------------
1. Extract this ZIP into the KMC repository root and allow README.txt under
   Tools\IvaCoverageAudit to be replaced.

2. Open PowerShell in:
   C:\Users\mobil\source\repos\KMC

3. Run the full tool test suite:

   python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

   Expected: 13 tests, all PASS.

4. If IvaAuditOutput from 14.20.1 still exists, run:

   python Tools/IvaCoverageAudit/classify_review_props.py --review-props IvaAuditOutput/ReviewProps.csv --output-dir IvaClassificationOutput

   Expected for the dataset used to build this milestone:
   Classified 181 unique REVIEW prop(s). Reports written to IvaClassificationOutput

5. Open IvaClassificationOutput\PropClassificationSummary.md.
   Expected totals:
     REFERENCE_BASELINE     105
     IGNORE_STATIC           17
     CONTROL_NO_BLACKOUT     19
     REUSE_ANNUNCIATOR       24
     REUSE_DIGITAL            6
     REUSE_PASSIVE            6
     REUSE_DISPLAY            1
     SPECIAL_REVIEW           3

6. Open IvaClassificationOutput\NewElectricalReview.csv.
   Expected special-review prop names:
     ASET_Flashlight
     kOSTerminal
     MonitorDockingMode

7. Do NOT push yet. Send the test result and classification summary back for
   review. The next milestone will use this evidence to build the first actual
   cockpit coverage batch.

KSP Plugin DLL Required? NO
