KMC 14.20.6 — UNIFIED LIGHTING ELECTRICAL AUTHORITY
=====================================================

BASELINE
--------
Frozen source baseline:
c7f4dcad026d2d77f7f3359b7a8b0c67aecc4aa5c

This build fixes both lighting paths without replacing the proven lighting
actuators:

1. EXTERNAL VESSEL LIGHTS / WINDOW EMISSIVES
   - Physical light output remains controlled by the proven 14.19.1
     KmcSystemAuthorityReceiver.
   - Lights authority is now automatically inhibited when the actual BUS_ESS
     electrical bus is known dead / failed / below 18.0 V.
   - Contactor command state alone does NOT count as electrical power.
   - Existing explicit Lights authority inhibits are preserved and ORed with
     electrical loss.
   - Unknown/missing electrical evidence fails open.
   - Crew Light action-group command is retained through power loss.

2. INTERNAL IVA / PANEL LIGHTING
   - Generalizes the proven Mk1 ASET backlight gate to the DE_IVAExtension
     interiors already brought to Mk1-reference electrical parity through
     14.20.5.
   - Uses actual ESS power truth.
   - Keeps PERSISTENT_BackLight as the crew command.
   - Unknown/non-target IVAs and KMC link loss fail open.
   - No renderer/material/RenderTexture/texture/mesh hacks.

IMPORTANT INSTALL METHOD
------------------------
This package uses a guarded Python source patcher for the four existing files.
The patcher validates every exact frozen-master marker BEFORE writing any file.
If the local source does not match the expected baseline structure, it aborts
before writing changes rather than guessing.

ADD
---
Tools/LightingAuthorityPatch/apply_14_20_6.py
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_6.py
docs/superpowers/specs/2026-09-04-kmc-14-20-6-unified-lighting-electrical-authority-design.md
docs/superpowers/plans/2026-09-04-kmc-14-20-6-unified-lighting-electrical-authority.md
README_14.20.6.txt

REPLACE / MODIFY (performed by apply_14_20_6.py)
------------------------------------------------
KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs
GameData/KMC/IVA/KmcRpmBridge.cfg
GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg

REMOVE
------
None.

STEP 1 — EXTRACT
----------------
Extract this ZIP into the root of your KMC repository:

C:\Users\mobil\source\repos\KMC

Allow the new Tools/docs/README files to be added.

STEP 2 — APPLY THE GUARDED SOURCE PATCH
---------------------------------------
From the KMC repository root run:

python Tools/LightingAuthorityPatch/apply_14_20_6.py

Expected:

KMC 14.20.6 lighting patch applied successfully.
Modified:
  KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
  KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs
  GameData/KMC/IVA/KmcRpmBridge.cfg
  GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg

If the patcher reports a mismatch, STOP. Do not manually edit the files.
Send the output back for review.

STEP 3 — AUTOMATED TESTS
------------------------
Run:

python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

Expected on the frozen 14.20.5 repository plus this build:

Ran 36 tests
OK

The eight new 14.20.6 tests verify:
- actual BUS_ESS truth drives external Lights authority
- unknown ESS evidence fails open
- explicit and electrical light inhibits are combined
- generalized and legacy RPM lighting variables are registered
- all 15 supported DE IVA INTERNAL names are in lighting scope
- Mission Control remains out of scope
- ASET crew-command architecture is preserved
- no renderer/material lighting hacks are introduced

STEP 4 — BUILD DEBUG
--------------------
KSP Plugin DLL Required? YES

Build the solution in DEBUG.

Why YES:
- KMC.MissionControl runtime authority logic changes.
- KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs changes.

STEP 5 — INSTALL RUNTIME BUILD
------------------------------
1. CLOSE KSP.
2. Build Debug.
3. Replace the installed KSP plugin DLL with:

   KMC.Plugin\bin\Debug\KMC.Plugin.dll

4. Copy/merge the repository GameData\KMC folder into the KSP GameData\KMC
   folder, replacing the changed CFG files.
5. Restart KSP fresh.

STEP 6 — EXTERNAL LIGHT RUNTIME ACCEPTANCE
------------------------------------------
Start with crew external lights commanded ON.

A. Nominal power
   Physical external lights/window emissives = ON.

B. MAIN A only failed
   Follow actual ESS state shown by KMC. If ESS remains energized >= 18 V,
   lights may remain available.

C. MAIN B only failed
   Same rule.

D. MAIN A + MAIN B failed so KMC schematic shows:
   ESSENTIAL BUS = UNPOWERED / 0.0 V

   Expected:
   Physical external lights/window emissives = OFF.

E. CLEAR ALL / restore electrical power
   Expected:
   Lights automatically return ON because crew command remained ON.

F. Command external lights OFF, then collapse and restore ESS
   Expected:
   Lights remain OFF after restoration.

STEP 7 — INTERNAL IVA LIGHT RUNTIME ACCEPTANCE
----------------------------------------------
Test representative families from a fresh KSP launch:

- Mk1 Cockpit
- Mk1 Pod or Mk1-3
- KV-1 or KV-2
- Mk2 Spaceplane or Mk2 Inline
- Mk3

For each representative:

1. Internal/panel light command ON at nominal ESS -> lit.
2. Collapse ESS to 0 V -> dark.
3. Restore ESS -> returns lit automatically.
4. Command internal/panel lighting OFF.
5. Collapse/restore ESS -> remains OFF.

STEP 8 — FAIL-OPEN
------------------
While KMC is actively inhibiting external lights because electrical authority
is lost, stop Mission Control / allow the KMC authority lease to expire.

Expected:
Normal KSP light authority returns automatically.

DO NOT PUSH YET
---------------
Do not push 14.20.6 until:
- 36 automated tests pass locally
- Debug build succeeds
- external lighting runtime acceptance passes
- retained OFF state passes
- fail-open lease passes
- representative internal IVA lighting tests pass

Then provide runtime results before pushing.

KSP Plugin DLL Required? YES
