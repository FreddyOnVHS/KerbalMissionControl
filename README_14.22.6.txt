KMC 14.22.6 — PROJECTED PATCH FIDELITY + MAP CONTROL REFINEMENT

PURPOSE
-------
1. Render projected closed maneuver patches from the actual KSP patch start UT
   forward, instead of always drawing a complete 360-degree conic.
2. Space the MAP view buttons farther apart for readability.
3. Make RESET VIEW return to AZ 360 / EL 0 as requested.

MODIFY
------
KMC.Plugin/OrbitMapTelemetrySender.cs
  - Continues to avoid unsafe Orbit.period.
  - Safely derives elliptic PeriodSeconds from semi-major axis and the
    reference body's gravParameter.

KMC.MissionControl/Rendering/OrbitMap/OrbitMapConicSampler.cs
  - Elliptic projected patches now sample from patch.StartUniversalTimeSeconds.
  - Uses patch.EndUniversalTimeSeconds when it provides a shorter valid span.
  - Caps a closed projected patch to one orbital period.
  - Adds local mean-anomaly -> eccentric-anomaly -> true-anomaly propagation.
  - Hyperbolic fallback behavior is unchanged for now.

KMC.MissionControl/Rendering/OrbitMap/OrbitMapCamera.cs
  - RESET VIEW now sets yaw = 0 and elevation = 0.
  - The readout displays yaw zero as AZ 360.

KMC.MissionControl/Pages/MapPage.cs
  - MAP buttons use a 24 px horizontal gap and slightly wider rectangles.

TESTS
-----
ADD:
  Tools/OrbitMap/tests/test_14_22_6_patch_bounds_and_controls.py

MODIFY:
  Tools/OrbitMap/tests/test_14_22_2_visibility_orientation.py
    - Updates the superseded reset-view expectation to AZ 360 / EL 0.

VERIFICATION IN THIS ENVIRONMENT
--------------------------------
14.22.6 focused tests: 4/4 PASS
Full available OrbitMap Python suite: 39/39 PASS

A C# compiler/MSBuild is not available in this sandbox, so Visual Studio is
still the authoritative build verification.

BUILD / RUNTIME TEST
--------------------
1. Overlay this ZIP on the current 14.22.5 source.
2. Build KMC.Plugin in Release.
3. Build KMC.MissionControl in Release.
4. Replace GameData\KMC\Plugins\KMC.Plugin.dll with the rebuilt plugin.
5. Launch the rebuilt Mission Control application.
6. Create a maneuver node in KSP and compare the projected KMC trajectory with
   KSP Map View.
7. Confirm the projected line begins at the maneuver patch start and follows
   forward rather than showing a misleading full ellipse when KSP has a shorter
   valid patch interval.
8. Press RESET VIEW and confirm VIEW AZ 360 / EL 00.
9. Confirm RESET VIEW / FIT ORBIT / FIT MANEUVER / TARGET are visibly spaced.
10. Confirm RX continues increasing and GEOMETRY REBUILD remains stable when
    the maneuver solution is unchanged.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll changed? NO
