KMC 14.22.25 — Encounter Timing Panel
======================================

BASELINE
--------
GitHub master verified before build:
f10a3c280429a99ac60f62ae643f3309b915bd3b

Parent of baseline:
39bc8c06d3b58b00beb7b480dd7220529f779907

PURPOSE
-------
Add useful encounter timing to the MAP ENCOUNTER panel without changing the
proven 14.22.23/14.22.24 encounter geometry or patched-conic behavior.

CHANGES
-------
KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
- ENCOUNTER panel now shows SOI entry countdown using the encounter patch's
  StartUniversalTimeSeconds relative to the current packet UT.
- Existing child-body encounter periapsis altitude remains unchanged.
- ENCOUNTER panel now shows CLOSEST countdown using the already-cached
  closest-approach sample UT associated with the encounter visual.
- Added compact event-time formatting:
    future: T-HH:MM:SS
    elapsed: T+HH:MM:SS
    >24 h:   T-Nd HH:MM:SS / T+Nd HH:MM:SS

IMPORTANT PRECISION NOTE
------------------------
"CLOSEST" is intentionally not labeled "PE T-". The stored encounter UT is the
closest transmitted encounter sample used for the encounter-body visual. The
PE altitude remains the actual child-patch periapsis value. This avoids claiming
an exact periapsis timestamp that the current telemetry protocol does not carry.

UNCHANGED
---------
- Encounter geometry and moving child-body translation
- Closest-approach body placement
- Canonical coordinate transform
- Patch coloring (orange / purple / green)
- Telemetry rate and packet protocol
- Scene geometry caching / rebuild behavior
- KMC.Plugin
- KMC.shared

FILES IN THIS BUILD
-------------------
KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
Tools/OrbitMap/tests/test_14_22_25_encounter_timing_panel.py
README_14.22.25.txt

BUILD / DEPLOY
--------------
1. Unzip this package.
2. Drag/drop the contents over the KerbalMissionControl repository root.
3. Overwrite the matching file when prompted.
4. Rebuild KMC.MissionControl in Visual Studio.
5. Launch the rebuilt Mission Control application.

No KSP-side DLL replacement is required for this build.

RUNTIME ACCEPTANCE TEST
-----------------------
Create/load a maneuver that produces a child-body encounter (for example Mun).
On the MAP page verify:

1. Existing encounter geometry still matches KSP.
2. Existing encounter PE altitude still agrees with the KSP child-body PE.
3. ENCOUNTER panel shows the child body and an ENTRY T- countdown before SOI entry.
4. ENCOUNTER panel shows PE altitude.
5. ENCOUNTER panel shows CLOSEST T- countdown.
6. As UT advances, ENTRY and CLOSEST countdowns decrease.
7. After an event passes, its display changes to T+ rather than becoming negative.
8. Camera rotate/zoom does not cause orbit geometry rebuilds.
9. Patch colors remain orange first primary / purple encounter / green later primary.

Do not report runtime PASS until these checks are performed in KSP/KMC.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
