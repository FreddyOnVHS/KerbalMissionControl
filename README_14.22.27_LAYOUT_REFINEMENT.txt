KMC 14.22.27 — MAP Subpage Header Layout Refinement
=====================================================

BASELINE
--------
This patch is applied on top of the unpushed/runtime-tested 14.22.27 Transfer
Planner Foundation built from frozen master:

e4fb3c41c1e10191bd5fbe7516396044705d5051

PURPOSE
-------
Fix the visible overlap between the ORBIT MAP title and the new LOCAL /
TRANSFER subpage tabs before 14.22.27 is frozen.

CHANGES
-------
KMC.MissionControl/Pages/MapPage.cs
- Adds a little more vertical breathing room between ORBIT MAP and the LOCAL / TRANSFER tabs.
- Moves the LOCAL viewport farther down to preserve a clear gap below the tabs.
- Moves the TRANSFER page panels farther down to preserve the same clear gap.
- No transfer-planner behavior changes.
- No telemetry changes.
- No orbit geometry changes.

FILES IN THIS PATCH
-------------------
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_27_header_layout.py
README_14.22.27_LAYOUT_REFINEMENT.txt

BUILD / DEPLOY
--------------
1. Unzip this package over the repository root containing the current 14.22.27
   foundation build.
2. Overwrite KMC.MissionControl/Pages/MapPage.cs.
3. Rebuild KMC.MissionControl only.
4. No KSP DLL replacement is required for this layout-only refinement.

RUNTIME ACCEPTANCE
------------------
1. LOCAL / TRANSFER tabs no longer overlap ORBIT MAP.
2. LOCAL page viewport begins below the subpage tabs.
3. TRANSFER SYSTEM BODIES / TRANSFER PLANNER panels begin below the tabs.
4. Destination selection still works.
5. LOCAL map behavior remains unchanged.

KSP Plugin DLL Required? NO (for this refinement; keep the 14.22.27 plugin already deployed)
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
