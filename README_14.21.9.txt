KMC 14.21.9 — COMPLETE F10 BREAKER FAILURE COVERAGE
=====================================================

BASE
----
Frozen KMC 14.21.8
SHA: 6ef2916c721b87128bee17b49741c14d9055266f

PURPOSE
-------
Complete the F10 Instructor Console breaker-trip catalog so every live
electrical breaker shown on the 14.21.8 POWER / BREAKERS page can be
independently tripped for test and failure-injection purposes.

NEW F10 BREAKER FAILURES
------------------------
POWER - PUMP A BREAKER TRIPPED
POWER - CABIN FAN A BREAKER TRIPPED
POWER - THERMAL HEATER A BREAKER TRIPPED

POWER - FLIGHT COMPUTER BREAKER TRIPPED
POWER - INSTRUMENTATION ESS BREAKER TRIPPED
POWER - RCS CONTROL BREAKER TRIPPED

POWER - GUID B BREAKER TRIPPED
POWER - PUMP B BREAKER TRIPPED
POWER - CABIN FAN B BREAKER TRIPPED
POWER - THERMAL HEATER B BREAKER TRIPPED

NEW BREAKER IDS
---------------
BRK_PUMP_A
BRK_CABIN_FAN_A
BRK_THERMAL_HEATER_A
BRK_FLIGHT_COMPUTER
BRK_INSTRUMENTATION_ESS
BRK_RCS_CONTROL
BRK_GUID_B
BRK_PUMP_B
BRK_CABIN_FAN_B
BRK_THERMAL_HEATER_B

RESULT
------
Together with the existing breaker presets from earlier builds, F10 now has
breaker-trip coverage for all 20 live load breakers:

MAIN A (5)
BRK_GUID_A
BRK_COMM_A
BRK_PUMP_A
BRK_CABIN_FAN_A
BRK_THERMAL_HEATER_A

ESS (10)
BRK_FLIGHT_COMPUTER
BRK_INSTRUMENTATION_ESS
BRK_FLIGHT_CONTROL
BRK_REACTION_WHEEL
BRK_ENGINE_CONTROL
BRK_STAGING_CONTROL
BRK_BRAKE_CONTROL
BRK_GEAR_CONTROL
BRK_LIGHTING_ESS
BRK_RCS_CONTROL

MAIN B (5)
BRK_GUID_B
BRK_COMM_B
BRK_PUMP_B
BRK_CABIN_FAN_B
BRK_THERMAL_HEATER_B

ARCHITECTURE
------------
- Reuses InstructorFailurePreset.
- Reuses the existing F10 failure selector.
- Reuses InstructorElectricalSourceFailureBridge.InjectSwitchFailure(...).
- Every new breaker failure uses SyntheticElectricalSwitchFailureMode.TrippedOpen.
- Breaker truth remains authoritative in KMC.
- No duplicate electrical state is introduced.
- Existing equipment/system failures such as PumpA, PumpB, GuidB remain
  separate from breaker-trip failures.
- No player breaker-operation UI is introduced.
- No KMC.Plugin enforcement change is introduced.

ADD
---
README_14.21.9.txt
Tools/ElectricalExpansion/apply_14_21_9.py
Tools/ElectricalExpansion/tests/test_14_21_9_complete_f10_breaker_coverage.py

REPLACE / MODIFY IN PLACE WHEN PATCHER RUNS
-------------------------------------------
KMC.MissionControl/Training/InstructorTrainingModel.cs
KMC.MissionControl/Training/InstructorConsoleForm.cs

REMOVE
------
None.

HOW TO APPLY
------------
1. Extract the ZIP into the repository root, preserving folders.
2. From the repository root run:

   python Tools/ElectricalExpansion/apply_14_21_9.py

FOCUSED TEST
------------
python -m pytest -q Tools/ElectricalExpansion/tests/test_14_21_9_complete_f10_breaker_coverage.py

REGRESSION TEST
---------------
python -m pytest -q Tools/ElectricalExpansion/tests

RUNTIME TEST
------------
Open F10 Instructor Console and verify the 10 new POWER breaker-trip choices.

At minimum verify:
1. Trip PUMP A and confirm BRK_PUMP_A shows CMD CLOSED / IND OPEN /
   UNPOWERED / 0.0A on POWER / BREAKERS.
2. Trip FLIGHT COMPUTER and confirm only that branch loses electrical
   conduction.
3. Trip RCS CONTROL and confirm the RCS breaker branch goes unpowered.
4. Trip GUID B and confirm BRK_GUID_B trips independently of GUID A.
5. Use Clear Failures / reset flow and confirm the injected trips clear.

DO NOT PUSH / FREEZE
--------------------
Do not push until:
- focused test passes,
- full ElectricalExpansion regression passes,
- KMC.MissionControl builds with 0 errors,
- F10 runtime test passes,
- final diff review is clean.

KSP Plugin DLL Required? NO
