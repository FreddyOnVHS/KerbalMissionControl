KMC 14.20.5 — Aircraft IVA Power Batch (Corrective)
=====================================================

BASELINE
--------
Frozen 14.20.4 HEAD:
a3442fd765244db23f8134e15737d0a168ccb613

PURPOSE
-------
Aircraft IVA power-domain coverage for:
- Mk1 Inline Cockpit
- Mk2 Spaceplane Cockpit
- Mk2 Inline Cockpit
- Mk3 Cockpit

CORRECTIVE CHANGE
-----------------
Runtime testing found the Mk3 right-side docking/landing display remained on
with all KMC electrical buses failed. Root cause: DE_MK3_Cockpit_Int contains
one ALCORMFD60x30 in addition to its four ALCORMFD40x20 displays, but the
original 14.20.5 Mk3 profile/test incorrectly omitted it.

The Mk3 ALCORMFD60x30 is now explicitly assigned to ESS using the already
proven KMC_ALCORMFD60x30_ESS family. No new runtime mechanism is introduced.

ADD
---
Nothing.

REPLACE
-------
GameData/KMC/IVA/Profiles/DE_IVAExtension/KmcProfile_DE_Mk3Cockpit.cfg
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_5.py
README_14.20.5.txt

REMOVE
------
Nothing.

AUTOMATED TEST
--------------
From the repository root:

  python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

Expected in the current 14.20.5 repo:

  Ran 28 tests
  OK

RUNTIME RETEST
--------------
1. Close KSP before copying the corrected config into GameData/KMC.
2. Restart KSP fresh.
3. Load the Mk3 Cockpit IVA.
4. Confirm all displays are powered normally.
5. Fail both MAIN buses and both ESS feeds (or otherwise remove all modeled
   KMC electrical power).
6. Confirm the right-side large docking/landing display now goes completely
   dark with the rest of its assigned electrical domain.
7. Clear all failures / restore nominal power.
8. Confirm the display wakes and returns without re-toggling cockpit controls.
9. Recheck MAIN A / MAIN B split behavior for the four 40x20 MFDs.

Do not push until the runtime retest passes.

KSP Plugin DLL Required? NO
