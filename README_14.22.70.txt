KMC 14.22.70 - True Mixed Maneuver Vector Guidance

Build on top of 14.22.69b.

Problem:
GUID ResolveAttitudeMode supports pure prograde/retrograde, normal/anti-normal,
and radial maneuvers. A mixed P/N/R stock node fell through to prograde, so the
FDAI green cue did not represent the actual transfer burn vector.

Fix:
- Mixed 2-axis and 3-axis nodes now form:
    P * prograde-unit + N * normal-unit + R * radial-out-unit
- The resultant is normalized in the vessel ReferenceTransform frame.
- FDAI alignment error and cue use that exact combined direction.
- Pure-axis maneuver behavior is unchanged.
- Requires the already-existing verified velocity, normal, and radial telemetry
  only for the axes actually used by the maneuver.

Apply:
From the repository root:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_70_true_mixed_maneuver_vector.ps1

Then run:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected NavigationTests:
42 passed, 0 failed

No KSP plugin changes.
