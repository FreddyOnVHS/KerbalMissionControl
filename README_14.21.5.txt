KMC 14.21.5 — GEAR + BRAKE BREAKER AUTHORITY
=============================================

BASELINE
--------
Frozen 14.21.4:
76d08646e74241fac3274277a56636c1cd51ecc5

PURPOSE
-------
Wire the existing ESS loads/breakers:

GEAR_CONTROL / BRK_GEAR_CONTROL
BRAKE_CONTROL / BRK_BRAKE_CONTROL

to the existing proven KSP system-authority paths for landing gear and
wheel brakes.

IMPORTANT
---------
This milestone does NOT add new KSP-side gear/brake enforcement code.

The KMC.Plugin already contains the proven authority behavior:
- Gear -> ModuleWheelDeployment / existing bidirectional gear gating
- Brakes -> ModuleWheelBrakes / existing brake authority

14.21.5 changes Mission Control so those existing authority leases are
now driven by the dedicated electrical branches instead of only manual
failure authority.

BEHAVIOR
--------
GEAR_CONTROL:
- breaker/power available -> gear authority available
- breaker open/tripped or ESS unavailable -> gear actuation inhibited
- command controls may still be moved
- existing KSP authority path handles both deployment/retraction
- restoring power restores capability without forcing a gear movement

BRAKE_CONTROL:
- breaker/power available -> wheel-brake authority available
- breaker open/tripped or ESS unavailable -> braking authority inhibited
- command controls may still be issued
- restoring power restores capability without creating a brake command

Both systems:
- single surviving ESS feed remains sufficient
- missing KMC electrical evidence fails open
- existing KMC failures remain authoritative
- LIGHTING_ESS remains intentionally unwired for 14.21.6

ADD
---
README_14.21.5.txt
Tools/ElectricalExpansion/apply_14_21_5.py
Tools/ElectricalExpansion/tests/test_14_21_5_gear_brake_authority.py

MODIFIED BY APPLY SCRIPT
------------------------
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py
Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py
Tools/ElectricalExpansion/tests/test_14_21_4_staging_separation_authority.py

The older tests are updated only to remove GEAR_CONTROL and
BRAKE_CONTROL from their intentionally-unwired future-breaker lists.

NOT MODIFIED
------------
KMC.Plugin/KmcSystemAuthorityReceiver.cs
KMC.shared/SystemAuthorityPacket.cs
KMC.Engine electrical model
GameData / IVA configs

No .csproj change is required.

APPLY
-----
Extract into:

C:\Users\mobil\source\repos\KMC

Then run:

python Tools\ElectricalExpansion\apply_14_21_5.py

Expected:

14.21.5 applied: GEAR_CONTROL and BRAKE_CONTROL now drive the existing KSP gear/brake authority paths.

TESTS
-----
Focused:

python -m unittest Tools.ElectricalExpansion.tests.test_14_21_5_gear_brake_authority -v

Expected:

Ran 12 tests
OK

Then:

python -m unittest discover -s Tools\ElectricalExpansion\tests -v

Then:

python -m unittest discover -s Tools\IvaCoverageAudit\tests -v

BUILD
-----
Build the solution in Debug.

KSP Plugin DLL Required? NO

The KSP plugin source does not change in 14.21.5. Do not replace the
installed KMC.Plugin.dll just for this milestone.

RUNTIME ACCEPTANCE
------------------
Use a vessel with retractable landing gear and wheel brakes.

A. NOMINAL
- ESS powered.
- Gear deploy/retract works.
- Brakes work.

B. SINGLE-FEED REDUNDANCY
- Fail MAIN A only.
- ESS stays powered.
- Gear and brakes keep working.
- Restore.
- Fail MAIN B only.
- ESS stays powered.
- Gear and brakes keep working.

C. TOTAL ESS LOSS
- Fail MAIN A + MAIN B so ESS reaches 0 V.
- Gear actuation must be unavailable in both directions.
- Brake authority must be unavailable.
- Gear/brake controls may still be operated, but downstream hardware
  must not respond.

D. RESTORE
- CLEAR ALL / restore electrical power.
- No gear movement should happen merely because power returned.
- Gear should respond on the next deliberate command.
- Brakes should respond on the next deliberate command.

E. BREAKER-SPECIFIC ACCEPTANCE
If the current KMC UI/debug controls allow individual breaker operation:
- open BRK_GEAR_CONTROL while ESS remains powered -> gear authority lost,
  brakes remain available
- restore BRK_GEAR_CONTROL
- open BRK_BRAKE_CONTROL -> brakes lost, gear remains available
- restore BRK_BRAKE_CONTROL

F. MOD COMPATIBILITY
If you have a modded landing gear/brake assembly that uses the stock
wheel modules, repeat the breaker/ESS-loss tests.
Unknown custom gear/brake modules remain fail-open through the existing
authority behavior.

DO NOT PUSH
-----------
Do not push until:
- focused 14.21.5 tests PASS
- full ElectricalExpansion suite PASS
- 52/52 IVA suite PASS
- Debug build PASS
- runtime acceptance PASS
- final diff reviewed

FINAL DIFF
----------
git status --short
git diff --check
git diff --stat
git diff --name-status

Expected tracked changes:
M KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
M Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py
M Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py
M Tools/ElectricalExpansion/tests/test_14_21_4_staging_separation_authority.py

Expected new files:
?? README_14.21.5.txt
?? Tools/ElectricalExpansion/apply_14_21_5.py
?? Tools/ElectricalExpansion/tests/test_14_21_5_gear_brake_authority.py

KSP Plugin DLL Required? NO
