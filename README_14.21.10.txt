KMC 14.21.10 — RCS BREAKER ENFORCEMENT FIX
===========================================

BASE
----
Frozen KMC 14.21.9
SHA: c10f6f605b3841112953f1e9871549de31d9b763

ROOT CAUSE
----------
Runtime diagnostics proved that the F10 failure for BRK_RCS_CONTROL activates,
but RCS authority remains electrically powered and therefore no inhibit packet
is sent.

The root cause is ordering in the synthetic electrical build:

1. BuildAndApply() creates the nominal distribution.
2. Electrical switch failures are applied.
3. Only AFTER BuildAndApply() returns, SpacecraftSystemsSystem adds the
   RCS_CONTROL load and BRK_RCS_CONTROL as a compatibility overlay.
4. The newly-created RCS breaker therefore did not exist when the switch-failure
   pass processed the active BRK_RCS_CONTROL failure.
5. Recalculation sees a healthy newly-created breaker, so RCS electrical
   authority incorrectly remains AVAILABLE.

FIX
---
Promote RCS_CONTROL into BuildNominalDistribution() alongside the other
essential loads.

This means BRK_RCS_CONTROL exists BEFORE:
- ApplyCrewControls(...)
- ApplyElectricalSwitchFailures(...)
- ResolveSwitching(...)
- Recalculate(...)

The existing later compatibility overlay remains harmless: it finds the
already-existing RCS load/breaker and does not create a duplicate.

EXPECTED FLOW
-------------
F10 BRK_RCS_CONTROL trip
    -> failure snapshot contains TrippedOpen for BRK_RCS_CONTROL
    -> nominal distribution already contains BRK_RCS_CONTROL
    -> electrical switch-failure pass marks breaker tripped/open
    -> breaker.Conducting = false
    -> RCS electrical authority becomes unavailable
    -> existing KMC-RCSAUTH1 inhibit lease is sent
    -> existing KSP RCS receiver inhibits RCS
    -> clearing failure restores authority

ADD
---
README_14.21.10.txt
Tools/ElectricalExpansion/apply_14_21_10.py
Tools/ElectricalExpansion/tests/test_14_21_10_rcs_breaker_enforcement.py

MODIFY
------
KMC.Engine/SpacecraftSystems/ElectricalDistributionSystem.cs

REMOVE
------
None from frozen 14.21.9.

TEMPORARY DIAGNOSTIC CLEANUP
----------------------------
The earlier 14.21.10 diagnostic candidate must NOT be committed.

Before applying this final package, restore these diagnostic-modified tracked
files to frozen 14.21.9:

    git restore KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
    git restore KMC.Plugin/KmcIvaAnnunciatorTestReceiver.cs

Then remove the old diagnostic-only files if they are still present:

    Remove-Item README_14.21.10.txt -ErrorAction SilentlyContinue
    Remove-Item Tools\ElectricalExpansion\apply_14_21_10.py -ErrorAction SilentlyContinue
    Remove-Item Tools\ElectricalExpansion\tests\test_14_21_10_rcs_diagnostics.py -ErrorAction SilentlyContinue

After that, extract this FINAL ZIP into the repo root.

APPLY
-----
From the repo root:

    python Tools/ElectricalExpansion/apply_14_21_10.py

FOCUSED TEST
------------
    python -m pytest -q Tools/ElectricalExpansion/tests/test_14_21_10_rcs_breaker_enforcement.py

FULL REGRESSION
---------------
    python -m pytest -q Tools/ElectricalExpansion/tests

Then:

    git diff --check
    git status --short

BUILD
-----
Rebuild:
- KMC.Engine
- KMC.MissionControl

KMC.Plugin source is unchanged by this final fix.

RUNTIME ACCEPTANCE
------------------
1. Start KSP and Mission Control.
2. Load a vessel with working RCS.
3. Confirm RCS translation/rotation works.
4. F10 -> POWER - RCS CONTROL BREAKER TRIPPED.
5. POWER / BREAKERS should show BRK_RCS_CONTROL open/unpowered.
6. RCS translation/rotation must no longer produce control.
7. Clear the failure.
8. RCS must become available again.
9. Stop Mission Control while RCS is inhibited to confirm the existing
   KSP-side 2.5-second fail-open lease still restores control.

DO NOT PUSH / FREEZE YET
------------------------
Do not commit/push/freeze until:
- focused test passes,
- full regression passes,
- solution build passes,
- runtime RCS trip/restore passes,
- final diff review is clean.

KSP Plugin DLL Required? NO

NOTE ABOUT THE TEMPORARY DIAGNOSTIC DLL
---------------------------------------
If you previously deployed the diagnostic KMC.Plugin.dll, the final source
does not require a new plugin build. The diagnostic DLL is functionally the
same RCS receiver plus logging. For a completely clean frozen runtime, restore
your prior 14.21.9 KMC.Plugin.dll after acceptance testing, or rebuild the
unchanged plugin source once diagnostics are removed.
