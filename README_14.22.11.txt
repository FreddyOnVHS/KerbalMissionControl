KMC 14.22.11 — Encounter Frame Handedness / Patch Continuity Fix

Baseline:
- Apply after the current 14.22.10 MAP build.

Problem:
- The projected Mun encounter body was at the correct future phase, but the child-SOI trajectory could appear on the opposite side of the encounter body compared with KSP.
- This was not a camera/perspective correction. The child-local hyperbolic branch could be reconstructed with the opposite transverse handedness when moved into the parent system frame.

Fix:
- Preserve KSP patched-conic continuity as the deciding invariant.
- Mission Control now remembers the end point of the preceding projected patch.
- For a child-referenced hyperbolic patch, it evaluates both the normal reconstructed branch and its reflection across the conic periapsis axis.
- It selects the branch whose StartUT point is closest to the preceding patch endpoint.
- This keeps the SOI transition continuous instead of blindly flipping an axis globally.
- Elliptic and same-body patch behavior is unchanged.

Files modified:
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs
- Tools/OrbitMap/tests/test_14_22_10_encounter_phase_alignment.py

Files added:
- Tools/OrbitMap/tests/test_14_22_11_encounter_frame_handedness.py

Build:
- Rebuild KMC.MissionControl in Release.

Runtime acceptance:
1. Keep the same Kerbin -> Mun maneuver encounter.
2. Compare KSP Map View and KMC MAP at the same time.
3. Verify the trajectory approaches/passes the future Mun encounter marker from the same side in both displays.
4. Verify the parent transfer and child-SOI patch meet continuously at the SOI transition.
5. Verify RX remains live and GEOMETRY REBUILD remains stable when the maneuver is not edited.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
