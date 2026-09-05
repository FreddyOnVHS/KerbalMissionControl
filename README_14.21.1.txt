KMC 14.21.1 — EXPANDED ESS BREAKER MODEL, SIMULATION ONLY
===========================================================

BASELINE
--------
Apply only on top of frozen 14.20.8 master:
241989bad6c677b755766a815fb3d14ddba2e55f

SCOPE
-----
This milestone changes the KMC.Engine electrical simulation only.

It:
- raises ESS FEED A from 6.0 A to 12.0 A
- raises ESS FEED B from 6.0 A to 12.0 A
- adds seven ESS loads/breakers:
  * SAS / FLIGHT CONTROL ELECTRONICS
  * REACTION WHEEL POWER
  * ENGINE CONTROL / IGNITION
  * STAGING / SEPARATION
  * BRAKE CONTROL
  * GEAR CONTROL / ACTUATION
  * EXTERNAL / EMERGENCY LIGHTING
- adds matching spacecraft-system components and BUS_ESS dependencies
- extends the existing DEBUG electrical self-test

This milestone DOES NOT yet make these breakers disable real KSP systems.
That enforcement is intentionally deferred to later builds.

IMPORTANT BREAKER IDs
---------------------
The proven AddLoad(...) path automatically creates:
BRK_FLIGHT_CONTROL
BRK_REACTION_WHEEL
BRK_ENGINE_CONTROL
BRK_STAGING_CONTROL
BRK_BRAKE_CONTROL
BRK_GEAR_CONTROL
BRK_LIGHTING_ESS

BRK_RCS_CONTROL is unchanged.

ADD
---
README_14.21.1.txt
Tools/ElectricalExpansion/apply_14_21_1.py
Tools/ElectricalExpansion/tests/test_14_21_1_expanded_ess_breakers.py
docs/superpowers/specs/2026-09-04-kmc-14-21-0-electrical-system-expansion-architecture.md
docs/superpowers/plans/2026-09-04-kmc-14-21-1-expanded-ess-breaker-model.md

MODIFY (by the apply script)
----------------------------
KMC.Engine/SpacecraftSystems/ElectricalDistributionSystem.cs
KMC.Engine/SpacecraftSystems/SpacecraftSystemsFoundationSystem.cs

REMOVE
------
None.

INSTALL / APPLY
---------------
1. Extract this ZIP directly into:
   C:\Users\mobil\source\repos\KMC

2. In PowerShell from the repo root run:

   python Tools\ElectricalExpansion\apply_14_21_1.py

Expected:
14.21.1 applied: 12 A ESS feeds + 7 simulation-only ESS breakers/components.

The script is idempotent. Running it again should report that 14.21.1 is already applied.

AUTOMATED TEST
--------------
Run:

python -m unittest discover -s Tools\ElectricalExpansion\tests -v

Then preserve the frozen IVA gate:

python -m unittest discover -s Tools\IvaCoverageAudit\tests -v

BUILD
-----
Build KMC.Engine Debug:

dotnet build KMC.Engine\KMC.Engine.csproj -c Debug

If your local .NET tooling requires Visual Studio for the project, use Visual Studio:
Build > Build Solution
with Debug selected.

KSP RUNTIME ACCEPTANCE
----------------------
No KSP plugin replacement is required.

Use the same installed KSP/KMC runtime you already have.

Verify:
1. Normal MAIN A / MAIN B / ESS operation remains normal.
2. Fail MAIN B only: ESS remains powered from ESS FEED A.
3. Clear.
4. Fail MAIN A only: ESS remains powered from ESS FEED B.
5. Clear.
6. Fail MAIN A + MAIN B: ESS still collapses to 0 V as before.
7. Existing cockpit displays/lighting/RCS behavior remains unchanged.

The seven new breaker consequences are NOT expected in KSP yet.

DO NOT PUSH
-----------
Do not push until:
- focused ElectricalExpansion tests pass
- existing IVA tests pass
- Debug build succeeds
- runtime acceptance passes
- git diff is reviewed

FINAL DIFF CHECK
----------------
git diff --check
git status --short
git diff --stat
git diff --name-status

Expected production-code scope:
KMC.Engine/SpacecraftSystems/ElectricalDistributionSystem.cs
KMC.Engine/SpacecraftSystems/SpacecraftSystemsFoundationSystem.cs

There must be NO:
- KMC.Plugin changes
- KMC.MissionControl authority changes
- GameData runtime changes

KSP Plugin DLL Required? NO
