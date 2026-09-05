KMC 14.21.2 — ESS FLIGHT-CONTROL ENFORCEMENT
================================================

BASELINE
--------
Apply after 14.21.1.

PURPOSE
-------
Connect the first two new ESS electrical branches to real KSP capability.

FLIGHT_CONTROL / BRK_FLIGHT_CONTROL
- Controls SAS / automatic flight-control authority.
- Does not directly disable RCS.
- Does not directly disable reaction wheels.

REACTION_WHEEL / BRK_REACTION_WHEEL
- Controls all ModuleReactionWheel hardware.
- Independent from SAS.
- Independent from RCS.

FAIL-OPEN
---------
Missing KMC electrical evidence does not invent a failure.
The existing 2.5-second KSP authority lease remains in place.

ADD
---
README_14.21.2.txt
Tools/ElectricalExpansion/apply_14_21_2.py
Tools/ElectricalExpansion/tests/test_14_21_2_flight_control_authority.py

MODIFIED BY APPLY SCRIPT
------------------------
KMC.shared/SystemAuthorityPacket.cs
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
KMC.Plugin/KmcSystemAuthorityReceiver.cs

REMOVE
------
None.

No .csproj change is required.

APPLY
-----
Extract into:

C:\Users\mobil\source\repos\KMC

Then run:

python Tools\ElectricalExpansion\apply_14_21_2.py

Expected:

14.21.2 applied: FLIGHT_CONTROL -> SAS authority; REACTION_WHEEL -> vessel-wide reaction-wheel authority.

TESTS
-----
Focused:

python -m unittest Tools.ElectricalExpansion.tests.test_14_21_2_flight_control_authority -v

Expected:

Ran 10 tests
OK

All electrical expansion:

python -m unittest discover -s Tools\ElectricalExpansion\tests -v

Frozen IVA regression:

python -m unittest discover -s Tools\IvaCoverageAudit\tests -v

BUILD / INSTALL
---------------
KSP Plugin DLL Required? YES

1. Build Debug in Visual Studio.
2. Confirm the solution builds successfully.
3. Close KSP.
4. Replace the installed plugin DLL with:

C:\Users\mobil\source\repos\KMC\KMC.Plugin\bin\Debug\KMC.Plugin.dll

5. Restart KSP.

RUNTIME ACCEPTANCE
------------------
Use a vessel with SAS capability and reaction wheels.

1. Nominal:
   - ESS powered.
   - SAS works.
   - manual reaction-wheel pitch/yaw/roll works.
   - RCS behaves as before.

2. Lose one main bus only:
   - ESS remains powered from the surviving 12 A feed.
   - SAS and reaction wheels continue to work.

3. Lose MAIN A + MAIN B:
   - ESS reaches 0 V.
   - SAS effective authority is lost.
   - reaction-wheel torque is lost.
   - RCS remains governed by its existing independent RCS authority path.

4. CLEAR ALL:
   - ESS restores.
   - SAS restores.
   - reaction-wheel torque restores.
   - retained SAS command is restored.

5. Existing reaction-wheel failure interaction:
   - if practical, apply an existing KMC reaction-wheel failure.
   - then lose and restore ESS.
   - confirm the failed wheel remains failed after electrical power returns.
   - if this does not behave correctly, STOP and report before pushing.

DO NOT PUSH
-----------
Do not push until:
- 14.21.2 focused tests PASS
- all ElectricalExpansion tests PASS
- frozen IVA tests PASS
- Debug build PASS
- runtime acceptance PASS
- final diff reviewed

FINAL DIFF
----------
git status --short
git diff --check
git diff --stat
git diff --name-status

Expected production changes:
M KMC.shared/SystemAuthorityPacket.cs
M KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
M KMC.Plugin/KmcSystemAuthorityReceiver.cs

No GameData change.
No KMC.Engine electrical-model change.
