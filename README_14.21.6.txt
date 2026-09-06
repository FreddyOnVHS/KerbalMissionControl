KMC 14.21.6 — LIGHTING BREAKER-SPECIFIC AUTHORITY
==================================================

BASELINE
--------
Frozen 14.21.5:
2ef337b6d46da61bc091cbf27b3c0db43a56931

PURPOSE
-------
Wire LIGHTING_ESS / BRK_LIGHTING_ESS to the already-proven Lights authority.
The old broad ESS lighting dependency becomes:

BUS_ESS -> BRK_LIGHTING_ESS -> LIGHTING_ESS -> existing Lights authority

Total ESS loss still kills lighting because LIGHTING_ESS itself loses power.

BEHAVIOR
--------
- Opening/tripping BRK_LIGHTING_ESS inhibits exterior/emergency lights.
- Other ESS-backed systems remain available if their own branches remain powered.
- Total ESS loss still inhibits lighting.
- Existing explicit KMC light inhibit remains combined with electrical truth.
- Existing ModuleLight / ModuleColorChanger receiver behavior is reused unchanged.
- No renderer/material hacks.
- Restoring lighting power restores capability while preserving the retained player light command.
- Missing LIGHTING_ESS evidence fails open.

ADD
---
README_14.21.6.txt
Tools/ElectricalExpansion/apply_14_21_6.py
Tools/ElectricalExpansion/tests/test_14_21_6_lighting_breaker_authority.py

MODIFIED BY APPLY SCRIPT
------------------------
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py
Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py
Tools/ElectricalExpansion/tests/test_14_21_4_staging_separation_authority.py
Tools/ElectricalExpansion/tests/test_14_21_5_gear_brake_authority.py
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_6.py

NOT MODIFIED
------------
KMC.Plugin runtime code
KMC.shared protocol
KMC.Engine electrical model
GameData / IVA configs

APPLY
-----
Extract into C:\Users\mobil\source\repos\KMC
Then run:
python Tools\ElectricalExpansion\apply_14_21_6.py

TESTS
-----
python -m unittest Tools.ElectricalExpansion.tests.test_14_21_6_lighting_breaker_authority -v
python -B -m unittest discover -s Tools\ElectricalExpansion\tests -v
python -B -m unittest discover -s Tools\IvaCoverageAudit\tests -v

RUNTIME ACCEPTANCE
------------------
1. Normal ESS + lighting branch powered: exterior lights work normally.
2. Single MAIN A or MAIN B loss: lights continue because ESS remains powered.
3. Total ESS loss: exterior/emergency lights go dark.
4. Restore ESS: commanded-ON lights return; commanded-OFF lights remain off.
5. If individual breaker control is available, open only BRK_LIGHTING_ESS: lights go dark while SAS, wheels, engine control, staging, gear, brakes and RCS remain available.
6. Restore BRK_LIGHTING_ESS: retained light command is respected.

DO NOT PUSH until focused tests, full ElectricalExpansion tests, 52/52 IVA tests, Debug build, runtime acceptance, and final diff review all pass.

KSP Plugin DLL Required? NO
