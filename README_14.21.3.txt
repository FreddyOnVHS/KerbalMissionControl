KMC 14.21.3 — ENGINE CONTROL / IGNITION AUTHORITY
==================================================

BASELINE
--------
Frozen 14.21.2:
1b8cb408cbcd0ee8d29bfac1fc04193a82e78e09

PURPOSE
-------
Wire the existing ESS load/breaker:

ENGINE_CONTROL
BRK_ENGINE_CONTROL

to real KSP engine behavior.

BEHAVIOR
--------
When ENGINE_CONTROL loses power:

- KMC identifies engines by KSP type inheritance:
  PartModule is ModuleEngines
- This includes stock ModuleEngines, ModuleEnginesFX, and compatible
  mod engine classes derived from them.
- If an identified engine is running, KMC calls the normal
  ModuleEngines.Shutdown() path.
- KMC does NOT set the entire engine PartModule enabled=false.
- Standard engine start commands/events are gated while electrical
  authority is absent.
- KMC repeats shutdown enforcement while the authority lease is active,
  so an engine cannot remain running through another activation path.
- Restoring electrical power restores the start command/event gates,
  but DOES NOT automatically relight the engine.
- The player must start/ignite the engine again normally.
- Unknown custom propulsion PartModules that do not derive from
  ModuleEngines are left alone (fail-open for mod compatibility).

No STAGING_CONTROL behavior is added in this milestone.

ADD
---
README_14.21.3.txt
Tools/ElectricalExpansion/apply_14_21_3.py
Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py

MODIFIED BY APPLY SCRIPT
------------------------
KMC.shared/SystemAuthorityPacket.cs
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
KMC.Plugin/KmcSystemAuthorityReceiver.cs
Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py

The 14.21.2 test is updated only so ENGINE_CONTROL is no longer
classified as an intentionally unwired future breaker.

REMOVE
------
None.

No .csproj change is required.

APPLY
-----
Extract this ZIP directly into:

C:\Users\mobil\source\repos\KMC

From the repo root run:

python Tools\ElectricalExpansion\apply_14_21_3.py

Expected:

14.21.3 applied: ENGINE_CONTROL shuts down ModuleEngines-derived engines without disabling their PartModules.

TESTS
-----
Focused:

python -m unittest Tools.ElectricalExpansion.tests.test_14_21_3_engine_control_authority -v

Expected:

Ran 11 tests
OK

Then all electrical tests:

python -m unittest discover -s Tools\ElectricalExpansion\tests -v

Then frozen IVA regression:

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
Use a vessel with at least one normal stock engine first.

A. NOMINAL
- ESS powered.
- Start engine normally.
- Engine responds to throttle normally.

B. SINGLE-FEED REDUNDANCY
- Fail MAIN A only.
- ESS stays powered.
- Running engine stays running.
- Restore.
- Fail MAIN B only.
- ESS stays powered.
- Running engine stays running.

C. TOTAL ESS LOSS
- Start engine.
- Fail MAIN A + MAIN B so ESS reaches 0 V.
- Engine must shut down immediately.
- Engine PartModule must not be globally disabled.
- Attempting normal engine activation while power is absent must not
  leave the engine running.

D. RESTORE
- CLEAR ALL / restore electrical power.
- Engine must NOT auto-reignite.
- Player manually activates/starts the engine.
- Engine runs normally again.

E. EXISTING ENGINE FAILURE
If practical:
- Apply an existing KMC engine failure.
- Lose and restore ESS.
- Electrical restoration must NOT repair the engine failure.

F. MOD COMPATIBILITY
If you have a common mod engine installed, especially one derived from
ModuleEnginesFX, repeat C and D with it.
- It should shut down through its normal engine path.
- It should restart normally after electrical power is restored.
- If a particular mod engine behaves differently, STOP and tell me the
  engine/mod name before push. We will add compatibility only where it
  is safe and specific.

DO NOT PUSH
-----------
Do not push until:
- focused 14.21.3 tests PASS
- all ElectricalExpansion tests PASS
- 52/52 IVA regression tests PASS
- Debug build PASS
- stock-engine runtime acceptance PASS
- any available representative mod-engine test PASS
- final git diff reviewed

FINAL DIFF
----------
git status --short
git diff --check
git diff --stat
git diff --name-status

Expected tracked production/test changes:
M KMC.shared/SystemAuthorityPacket.cs
M KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
M KMC.Plugin/KmcSystemAuthorityReceiver.cs
M Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py

Expected new files:
?? README_14.21.3.txt
?? Tools/ElectricalExpansion/apply_14_21_3.py
?? Tools/ElectricalExpansion/tests/test_14_21_3_engine_control_authority.py

No KMC.Engine electrical-model change.
No GameData/config change.

KSP Plugin DLL Required? YES
