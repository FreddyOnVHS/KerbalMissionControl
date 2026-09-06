KMC 14.21.7 — EXPANDED ESS BREAKER F10 FAILURE CONTROLS
========================================================

BASELINE
--------
Apply on top of the current 14.21.6 working tree.

Frozen predecessor:
KMC 14.21.5
2ef337b6d46da61bc091cbf27b3c0db43a56931

14.21.6 is intentionally NOT frozen/pushed yet.

SCOPE
-----
This is a Mission Control failure/test-injection milestone only.
It does NOT create the eventual player-facing breaker interface.
Players are still intended to operate breakers through IVA MFDs later.

This milestone adds seven breaker-trip choices to the existing F10
Instructor Console and routes them through the already-proven synthetic
electrical switch failure infrastructure using TrippedOpen.

NEW F10 FAILURE PRESETS
-----------------------
POWER - FLIGHT CONTROL BREAKER TRIPPED  -> BRK_FLIGHT_CONTROL
POWER - REACTION WHEEL BREAKER TRIPPED -> BRK_REACTION_WHEEL
POWER - ENGINE CONTROL BREAKER TRIPPED -> BRK_ENGINE_CONTROL
POWER - STAGING CONTROL BREAKER TRIPPED -> BRK_STAGING_CONTROL
POWER - BRAKE CONTROL BREAKER TRIPPED -> BRK_BRAKE_CONTROL
POWER - GEAR CONTROL BREAKER TRIPPED -> BRK_GEAR_CONTROL
POWER - LIGHTING ESS BREAKER TRIPPED -> BRK_LIGHTING_ESS

ARCHITECTURE
------------
- Reuses InstructorFailurePreset and the existing F10 selector population.
- Reuses InstructorElectricalSourceFailureBridge.InjectSwitchFailure(...).
- Uses SyntheticElectricalSwitchFailureMode.TrippedOpen for all seven.
- Breaker truth remains authoritative in KMC.
- No KSP PartModule disable behavior is introduced here.
- No new failure engine is introduced.
- No KMC.Plugin runtime enforcement changes are introduced.

ADD
---
README_14.21.7.txt
Tools/ElectricalExpansion/apply_14_21_7.py
Tools/ElectricalExpansion/tests/test_14_21_7_f10_ess_breakers.py

REPLACE / MODIFY IN PLACE WHEN PATCHER RUNS
-------------------------------------------
KMC.MissionControl/Training/InstructorTrainingModel.cs
KMC.MissionControl/Training/InstructorConsoleForm.cs

REMOVE
------
None.

HOW TO APPLY
------------
1. Extract this ZIP into the repository root, preserving folders.
2. Open a terminal in the repository root.
3. Run:

   python Tools/ElectricalExpansion/apply_14_21_7.py

The patcher is idempotent: running it again should report that 14.21.7 is
already applied and should not duplicate presets or switch cases.

TESTS
-----
Focused patcher tests:

   python -m pytest -q Tools/ElectricalExpansion/tests/test_14_21_7_f10_ess_breakers.py

Expected focused result from package construction:
   3 passed

Then run the existing ElectricalExpansion regressions and the IVA suite before
runtime testing. 14.21.6 had returned IVA to 52/52 PASS; preserve that result.

RUNTIME TEST CHECKLIST
----------------------
Open F10 Instructor Console and individually inject each of the seven new
POWER breaker-trip entries.

For every breaker:
1. Confirm the failure appears in the active failure list.
2. Confirm the named BRK_* switch becomes tripped/open in KMC electrical truth.
3. Confirm ONLY that branch loses capability:
   - FLIGHT CONTROL -> SAS / flight-control electronics
   - REACTION WHEEL -> reaction wheels
   - ENGINE CONTROL -> engine control / ignition
   - STAGING CONTROL -> staging / separation
   - BRAKE CONTROL -> brakes
   - GEAR CONTROL -> gear
   - LIGHTING ESS -> exterior/emergency lighting
4. Confirm unrelated ESS-backed branches remain available.
5. CLEAR SELECTED / CLEAR ALL / RESET NOMINAL and confirm authority restores.
6. Re-check MAIN A + MAIN B loss behavior: if ESS is 0 V, ESS-backed branches
   remain unavailable regardless of individual breaker state.

DO NOT PUSH / FREEZE YET
------------------------
Do not push/freeze 14.21.7 until:
- focused tests pass
- ElectricalExpansion regressions pass
- IVA suite passes
- runtime F10 injection tests pass for all seven entries
- clear/reset behavior is confirmed

KSP Plugin DLL Required? NO
