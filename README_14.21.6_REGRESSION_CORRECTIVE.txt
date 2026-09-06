KMC 14.21.6 — REGRESSION CORRECTIVE

Fixes two test-only regressions:
1. Restores a valid body to the old 14.21.5 LIGHTING_ESS test.
2. Updates the old IVA fail-open expectation from broad essPowered
   truth to lightingEssPowered truth.

No production code changes.

Apply from repo root:
python Tools\ElectricalExpansion\fix_14_21_6_regressions.py

Then rerun:
python -B -m unittest discover -s Tools\ElectricalExpansion\tests -v
python -B -m unittest discover -s Tools\IvaCoverageAudit\tests -v

Do not push yet.

KSP Plugin DLL Required? NO
