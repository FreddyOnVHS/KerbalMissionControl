KMC 14.22.9 — Encounter Body Visualization
============================================

Purpose
-------
Adds a KSP-style future encounter-body marker to the MAP page so a planned
Mun/Minmus encounter is easier to understand visually.

Changes
-------
MODIFY:
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs

ADD:
- Tools/OrbitMap/tests/test_14_22_9_encounter_body_visual.py
- README_14.22.9.txt

Behavior
--------
- Keeps the normal child-body marker at the body's current position.
- When a projected patch is referenced to an immediate child body, adds a
  second encounter marker at that body's position at patch StartUT.
- Draws the future encounter body as a distinct filled/outlined disk.
- Labels it "<BODY> ENC".
- Draws a faint SOI ring when it is large enough to be useful on screen.
- Uses cached encounter geometry/state; camera movement does not rebuild the
  orbital solution.
- Does not change telemetry protocol or KSP plugin behavior.

Install
-------
Extract this ZIP over the KMC repository root and allow folders/files to merge.

Build
-----
Rebuild KMC.MissionControl in Release.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO

Runtime Acceptance
------------------
1. Keep the same Kerbin -> Mun encounter used for 14.22.8 validation.
2. Confirm RX continues climbing and MAP remains LIVE.
3. Confirm the normal current Mun marker remains on Mun's current orbital position.
4. Confirm a second "Mun ENC" body marker appears at the future encounter position.
5. Confirm the encounter arc visually passes around that encounter marker.
6. Confirm a faint SOI ring appears when its projected size is useful.
7. Rotate/zoom the camera and confirm geometry rebuild count remains stable.
