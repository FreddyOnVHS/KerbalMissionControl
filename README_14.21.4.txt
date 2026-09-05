KMC 14.21.4 — STAGING / SEPARATION AUTHORITY
=============================================

BASELINE
--------
Frozen 14.21.3:
7eb91380a0dfb2816fe3955d2c97aa1d03387fe1

PURPOSE
-------
Wire the existing ESS load/breaker:

STAGING_CONTROL
BRK_STAGING_CONTROL

to real KSP staging and separation behavior.

BEHAVIOR
--------
When STAGING_CONTROL loses power:

- Normal KSP staging input is locked.
- Stock stack decouplers are inhibited.
- Stock radial / anchored decouplers are inhibited.
- Docking-port Undock / Decouple commands are inhibited.
- Recognized separation modules remain enabled; KMC gates only their
  staging / separation command paths.
- No attempted stage, decouple, or undock command is replayed when
  power returns.
- Restoring electrical power restores the player's normal staging and
  separation controls.
- Unknown custom separation PartModules remain fail-open for mod
  compatibility rather than being guessed at or disabled.

MOD COMPATIBILITY
-----------------
The build recognizes stock KSP separation families using runtime type
checks where practical:
- ModuleDecouplerBase and subclasses
- ModuleAnchoredDecoupler
- ModuleDockingNode

This allows compatible subclasses to inherit KMC behavior automatically.
Completely custom separation modules are left alone.

ADD
---
README_14.21.4.txt
Tools/ElectricalExpansion/apply_14_21_4.py
Tools/ElectricalExpansion/tests/test_14_21_4_staging_separation_authority.py

MODIFIED BY APPLY SCRIPT
------------------------
KMC.shared/SystemAuthorityPacket.cs
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
KMC.Plugin/KmcSystemAuthorityReceiver.cs
Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py
Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py

The older tests are updated only to remove STAGING_CONTROL from their
lists of intentionally-unwired future breakers.

REMOVE
------
None.

No .csproj change is required.

APPLY
-----
Extract into:

C:\Users\mobil\source\repos\KMC

Then run:

python Tools\ElectricalExpansion\apply_14_21_4.py

Expected:

14.21.4 applied: STAGING_CONTROL now gates staging, stock decouplers, and docking-port separation commands.

TESTS
-----
Focused:

python -m unittest Tools.ElectricalExpansion.tests.test_14_21_4_staging_separation_authority -v

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

KSP Plugin DLL Required? YES

After a successful Debug build:
1. Close KSP.
2. Replace the installed KMC.Plugin.dll with:

C:\Users\mobil\source\repos\KMC\KMC.Plugin\bin\Debug\KMC.Plugin.dll

3. Restart KSP.

RUNTIME ACCEPTANCE
------------------
Use a simple two-stage stock vessel with:
- at least one stack or radial decoupler
- optionally two docked vessels / a docking port test craft

A. NOMINAL
- ESS powered.
- Spacebar staging works.
- Right-click / action-group Decouple works.
- Docking-port Undock / Decouple works.

B. SINGLE-FEED REDUNDANCY
- Fail MAIN A only.
- ESS stays powered.
- Staging / separation still works.
- Restore.
- Fail MAIN B only.
- ESS stays powered.
- Staging / separation still works.

C. TOTAL ESS LOSS
- Fail MAIN A + MAIN B so ESS reaches 0 V.
- Spacebar must NOT activate the next stage.
- Right-click / action-group Decouple must be unavailable / ineffective.
- Docking-port Undock / Decouple must be unavailable / ineffective.
- No decoupler/docking PartModule should be globally disabled.

D. RESTORE
- CLEAR ALL / restore electrical power.
- Nothing should separate automatically.
- Spacebar staging should work again on the next deliberate press.
- Decouple / Undock commands should work again deliberately.

E. COMMAND RETENTION SAFETY
- While ESS is dead, press Spacebar once or more.
- Restore power.
- Verify no stored stage fires automatically.

F. MOD COMPATIBILITY
If you have a modded decoupler or docking port that still uses a stock
module family, repeat C and D.
If a mod uses a completely custom separation module, it may remain
operable by design (fail-open). Tell me the mod/module name before push
if you want explicit compatibility added.

DO NOT PUSH
-----------
Do not push until:
- focused 14.21.4 tests PASS
- all ElectricalExpansion tests PASS
- 52/52 IVA tests PASS
- Debug build PASS
- staging runtime acceptance PASS
- docking/decoupler acceptance PASS
- final diff reviewed

FINAL DIFF
----------
git status --short
git diff --check
git diff --stat
git diff --name-status

Expected tracked changes:
M KMC.shared/SystemAuthorityPacket.cs
M KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
M KMC.Plugin/KmcSystemAuthorityReceiver.cs
M Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py
M Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py

Expected new files:
?? README_14.21.4.txt
?? Tools/ElectricalExpansion/apply_14_21_4.py
?? Tools/ElectricalExpansion/tests/test_14_21_4_staging_separation_authority.py

No KMC.Engine electrical-model change.
No GameData/config change.

KSP Plugin DLL Required? YES
