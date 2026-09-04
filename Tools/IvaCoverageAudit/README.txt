KMC IVA COVERAGE AUDIT + PROP CLASSIFICATION
=============================================

PURPOSE
-------
14.20.1 inventories DE/ASET IVA PROP usage conservatively.
14.20.2 converts the REVIEW list into one explicit reusable decision per PROP.

Neither tool edits KSP or KMC runtime files.

14.20.2 CATEGORIES
------------------
REFERENCE_BASELINE
  Present in the completed DE Mk1 cockpit REVIEW set. No new work is required
  to reach the same Mk1-level scope in another cockpit.

IGNORE_STATIC
  Decorative, structural, storage, hatch, or other non-display prop.

CONTROL_NO_BLACKOUT
  Command control. The switch/button/handle may still move; KMC models the
  downstream hardware authority instead of electrically disabling the control.

REUSE_ANNUNCIATOR
  New prop name that fits the proven powered-annunciator architecture.

REUSE_DIGITAL
  New prop name that fits the proven powered-digital-indicator architecture.

REUSE_PASSIVE
  New prop name that fits the proven powered passive-instrument architecture.

REUSE_DISPLAY
  New powered display that should use the proven RPM MFD power architecture.

SPECIAL_REVIEW
  Electrically relevant prop with no safe direct Mk1-family assignment yet.
  These are the only props that need individual inspection before batching.

RUN 14.20.1 AUDIT
-----------------
From the KMC repository root:

python Tools/IvaCoverageAudit/iva_coverage_audit.py --kmc-iva-root GameData/KMC/IVA --iva-root "D:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\DE_IVAExtension" --output-dir IvaAuditOutput

RUN 14.20.2 CLASSIFICATION
--------------------------
python Tools/IvaCoverageAudit/classify_review_props.py --review-props IvaAuditOutput/ReviewProps.csv --output-dir IvaClassificationOutput

Expected for the audited 16-DE-IVA dataset used to create 14.20.2:
  Classified 181 unique REVIEW prop(s).

The classifier intentionally FAILS if ReviewProps.csv contains a prop with no
explicit decision, or if the decision table contains a stale/extra prop.

OUTPUTS
-------
PropClassificationReport.csv
  Complete one-row-per-prop decision table joined to audit occurrence data.

PropClassificationSummary.md
  Category totals and remaining special-review count.

CockpitWorkload.csv
  Per-IVA counts showing how much is reference baseline, static/control,
  reusable electrical-family work, or special review.

NewElectricalReview.csv
  Only SPECIAL_REVIEW props. This is the manual-research shortlist.

TESTS
-----
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

14.20.2 packaged expectation: 13 tests, all PASS.

SAFETY / ARCHITECTURE
---------------------
This milestone does not alter GameData/KMC/IVA runtime configs.
This milestone does not alter any C# project or DLL.
The classifier is planning evidence only; REUSE_* means "candidate for the
proven family" and is not itself a runtime patch.
