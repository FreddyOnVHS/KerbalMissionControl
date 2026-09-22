KMC 14.22.28 — First Transfer-Window Calculation
===================================================

BASELINE
--------
GitHub master verified before build:
52ab7bfba1134e0b8d2110134b39030a96d6828a

PURPOSE
-------
Add the first real transfer-window calculation to the TRANSFER subpage while
keeping maneuver creation and hierarchy-change planning out of scope.

CALCULATION SCOPE
-----------------
14.22.28 calculates an idealized Hohmann-style window only when ORIGIN and
DESTINATION orbit the same parent body.

Examples from Kerbin:
- Kerbin -> Duna: supported
- Kerbin -> Eve: supported
- Kerbin -> Jool: supported
- Kerbin -> Mun: not supported yet (hierarchy change)
- Kerbin -> Bop: not supported yet (hierarchy change)

The solution is explicitly a CIRCULAR / COPLANAR approximation. It is not yet
an ejection burn from the vessel's parking orbit.

WHAT THE PLANNER SHOWS
----------------------
For a valid same-parent route:
- parent body
- current phase angle
- required Hohmann phase angle
- time until next idealized window
- departure universal time
- idealized transfer time
- parent-frame transfer delta-v at the origin body's orbital radius

MATH / DATA AUTHORITY
---------------------
- No stock Kerbol gravitational constant is hard-coded.
- Parent gravitational parameter is derived from KSP-transmitted body orbit
  semi-major axis + period via Kepler's third law.
- Current origin/destination phase is propagated from KSP-transmitted orbital
  elements to the current packet UT.
- The active origin body's transmitted local PositionX/Y/Z remains untouched;
  LOCAL map coordinate behavior is therefore unchanged.

IMPORTANT LIMITATIONS
---------------------
14.22.28 does NOT:
- account for inclination-change cost
- solve Lambert trajectories
- model eccentric-body departure geometry exactly
- calculate vessel parking-orbit ejection angle/delta-v
- create or modify a KSP maneuver node
- plan hierarchy-change routes such as Kerbin -> Mun/Bop

Those are later builds.

FILES IN THIS BUILD
-------------------
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_28_first_transfer_window.py
README_14.22.28.txt

BUILD / DEPLOY
--------------
1. Unzip this package.
2. Drag/drop contents over the KerbalMissionControl repository root.
3. Overwrite matching files.
4. Rebuild KMC.MissionControl.
5. Launch the rebuilt Mission Control application.

The 14.22.27 KSP plugin already provides all telemetry needed by this build.
No KSP-side DLL replacement is required.

RUNTIME ACCEPTANCE TEST
-----------------------
1. Verify LOCAL still behaves exactly as 14.22.27.
2. Open TRANSFER.
3. Select Duna from a Kerbin-origin flight.
4. Verify ROUTE CLASS says SAME PARENT.
5. Verify a HOHMANN WINDOW block appears.
6. Verify CURRENT PHASE, REQ PHASE, WINDOW IN, DEPARTURE UT,
   TRANSFER TIME and PARENT DV all show finite values.
7. Select Eve/Jool/Moho and verify the values change.
8. Select Mun/Bop and verify KMC does NOT fabricate a direct Hohmann result;
   it should state that hierarchy-change planning is required.
9. Verify destination selection and page switching remain responsive.
10. Do not report runtime PASS until these checks are performed in KSP/KMC.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
