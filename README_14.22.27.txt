KMC 14.22.27 — Kerbol System / Transfer Planner Foundation
============================================================

BASELINE
--------
GitHub master verified before build:
e4fb3c41c1e10191bd5fbe7516396044705d5051

PURPOSE
-------
Start the interplanetary transfer-planning feature without disturbing the
proven LOCAL orbit-map geometry.

This build adds the first TRANSFER subpage and expands the KSP body catalog
sent through the existing orbit-map packet so Mission Control can select
bodies outside the vessel's immediate local system.

WHAT CHANGED
------------
KMC.Plugin/OrbitMapTelemetrySender.cs
- BuildBodies now walks FlightGlobals.Bodies rather than sending only the
  current reference body and its direct children.
- Body names/orbits come from KSP at runtime; stock body names are NOT
  hard-coded in Mission Control.
- The current reference body's local packet position remains 0,0,0 so the
  existing LOCAL map frame remains unchanged.
- Other bodies retain canonical position/orbit data relative to their own
  parent body.
- The existing OrbitMapPacket.MaxBodies limit remains unchanged. Stock KSP's
  sixteen orbiting celestial bodies fit the existing catalog. The central
  star is omitted unless it is the active vessel's reference body.

KMC.MissionControl/Pages/MapPage.cs
- Adds MAP subpage tabs:
    LOCAL
    TRANSFER
- LOCAL retains the existing orbit map, camera, patches, encounter display,
  control strip and geometry cache.
- TRANSFER adds a KSP-driven system catalog and clickable destination buttons.
- Current reference body is shown as ORIGIN and is excluded from destination
  choices.
- Selected destination shows parent body and real KSP orbital metadata:
    semi-major axis
    eccentricity
    inclination
    orbital period
- Shows whether origin/destination share the same parent as groundwork for
  later transfer-solution routing.
- No transfer burn calculation or maneuver creation is performed yet.

IMPORTANT SCOPE
---------------
14.22.27 is deliberately the planner FOUNDATION only.

It does NOT:
- calculate a Hohmann transfer
- calculate a phase angle
- calculate ejection delta-v
- calculate maneuver-node placement
- create or modify a KSP maneuver node

Those belong in following builds after this catalog/UI foundation is runtime
validated.

FILES IN THIS BUILD
-------------------
KMC.Plugin/OrbitMapTelemetrySender.cs
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_27_transfer_planner_foundation.py
README_14.22.27.txt

BUILD / DEPLOY
--------------
1. Unzip this package.
2. Drag/drop its contents over the KerbalMissionControl repository root.
3. Overwrite matching files when prompted.
4. Rebuild KMC.Plugin.
5. Rebuild KMC.MissionControl.
6. Replace the KSP-side plugin DLL:
     GameData/KMC/Plugins/KMC.Plugin.dll
7. Launch KSP and the rebuilt Mission Control application.

RUNTIME ACCEPTANCE TEST
-----------------------
LOCAL PAGE
1. LOCAL page still renders the active vessel/current body exactly as before.
2. Mun/Minmus or equivalent current-body children remain positioned correctly.
3. Existing maneuver/encounter patches and colors remain correct.
4. Rotate/zoom and RESET/FIT controls still work.
5. Geometry rebuild behavior remains stable when the trajectory is static.

TRANSFER PAGE
6. Click TRANSFER and verify a destination-body catalog appears.
7. For stock KSP around Kerbin, verify bodies outside the Kerbin system are
   present (for example interplanetary destinations, not just Mun/Minmus).
8. Click several destination bodies and verify the selected destination changes.
9. Verify parent/SMA/ECC/INC/PERIOD data changes with the selected body.
10. Verify the active reference body is shown as ORIGIN and is not selectable
    as its own destination.
11. Return to LOCAL and verify the normal map remains operational.

Do not report runtime PASS until Visual Studio compilation and these KSP/KMC
checks are performed.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
