KMC 14.22.2 — ORBIT MAP VISIBILITY / ORIENTATION REFINEMENT

MODIFY
------
KMC.MissionControl/Rendering/OrbitMap/OrbitMapCamera.cs
  - Camera-space body-sphere occlusion test.
  - Camera azimuth/elevation readouts.
  - Projected view-axis directions.
  - RESET uses a three-quarter oblique view (-35 deg yaw, +22 deg pitch).

KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
  - Body-hidden orbit/target/patch segments are not painted.
  - Body-hidden markers are not painted.
  - Adds VIEW AZ / EL readout.
  - Adds compact X / Y / N orientation triad.

ADD
---
Tools/OrbitMap/tests/test_14_22_2_visibility_orientation.py

VERIFICATION IN THIS ENVIRONMENT
--------------------------------
TDD:
  RED: new 14.22.2 test failed before implementation.
  GREEN: new 14.22.2 test passed after implementation.

The complete available OrbitMap Python regression suite was also run.
Visual Studio remains the authoritative C# build/runtime verification.

RUNTIME ACCEPTANCE
------------------
1. Build KMC.MissionControl in Release.
2. Launch KMC with a vessel in a stable orbit.
3. Rotate through front/side/rear views.
4. Confirm Kerbin cleanly hides far-side orbit segments.
5. Confirm AP/PE/VSL/TGT/MNV markers hide when physically behind the body.
6. Confirm VIEW AZ / EL and X/Y/N change with camera rotation.
7. Press RESET VIEW and confirm the default is a readable oblique view.
8. Confirm RX continues rising and GEOMETRY REBUILD remains low/stable.

KSP Plugin DLL Required? NO
Keep the working 14.22.1 KMC.Plugin.dll with the safe patched-conic fixes.
