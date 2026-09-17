KMC 14.22.5 — PROJECTED MANEUVER ORBIT / PATCH SOURCE

PURPOSE
-------
Display KSP's authoritative projected post-maneuver orbit on the KMC MAP page.

ROOT CAUSE
----------
14.22.4 transmitted active.orbit as patch 0. That is the vessel's current
momentum/orbit, so Mission Control was simply re-drawing the current orbit and
never receiving the maneuver node's projected post-burn conic.

KSP's ManeuverNode.nextPatch is the projected orbit beginning after that burn.

MODIFY
------
KMC.Plugin/OrbitMapTelemetrySender.cs
  - If a maneuver node exists, sort nodes by UT and begin the projected patch
    chain at the first node's nextPatch.
  - Follow nextPatch from there for subsequent patched-conic/SOI segments.
  - If no maneuver exists, begin only at active.orbit.nextPatch so the current
    orbit is not duplicated as a projected patch.
  - Existing safe future-patch element extraction remains unchanged.

NO PROTOCOL CHANGE
------------------
KMC-ORBITMAP1 remains unchanged. Mission Control already receives and renders
OrbitMapPacket.Patches, so no Mission Control code change is required.

ADD
---
Tools/OrbitMap/tests/test_14_22_5_projected_maneuver_patches.py

VERIFICATION IN THIS ENVIRONMENT
--------------------------------
TDD:
  RED: new projected-patch tests fail against 14.22.4 behavior.
  GREEN: tests pass after selecting ManeuverNode.nextPatch.
  Full available OrbitMap Python regression suite run afterward.

RUNTIME ACCEPTANCE
------------------
1. Overlay this ZIP on current 14.22.4 source.
2. Build KMC.Plugin in Release.
3. Replace GameData\KMC\Plugins\KMC.Plugin.dll.
4. Create one obvious prograde maneuver node in KSP.
5. Confirm KMC shows a distinct projected post-burn orbit.
6. Compare its shape/orientation with KSP's white projected trajectory.
7. Move the node / change delta-v and confirm the projected path updates.
8. Confirm RX continues climbing and GEOMETRY REBUILD increments when the
   maneuver solution changes, not continuously while unchanged.
9. If the projected orbit does not match, capture KSP + KMC screenshots and
   upload a fresh KSP.log.

KSP Plugin DLL Required? YES
Mission Control does not need rebuilding for this build.
