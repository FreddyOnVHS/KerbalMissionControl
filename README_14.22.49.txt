KMC 14.22.49 — Robust Long-Coast Propagation
=================================================

BASE
----
Apply this small overlay on top of the clean local 14.22.48 state.

WHY
---
Dres still produced a valid 3D Lambert ejection but TARGET-SOI SHOOTING did
not start.

The remaining common numerical weak point is parent-frame Cartesian
propagation from source SOI exit to the Lambert arrival epoch.

StateVectorPropagator used one universal-variable Newton solve for the entire
coast. Long, near-parabolic elliptic states can make that Newton solve poorly
conditioned even though the physical two-body trajectory is perfectly valid.

Dres has a much longer heliocentric coast than the already-proven Duna/Eve
cases. Jool and Eeloo would stress this same path even harder.

FIX
---
StateVectorPropagator now:

1. tries the existing one-step universal-variable solution unchanged;
2. if it fails, retries the same two-body propagation in 2 segments;
3. if necessary, retries with 4, 8, 16, ... up to 256 segments.

Each segment is still an exact universal-variable two-body propagation.
There are no planet-specific constants and no changed physical assumptions.

Normal short/ordinary propagation continues to use the original single-step
path, so proven Duna/Eve behavior is preserved.

REGRESSION
----------
A deterministic near-parabolic elliptic state that is numerically hostile to
a long one-step Newton solve is propagated forward and backward over a long
coast.

EXPECTED TESTS
--------------
NavigationTests: 27 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 5 passed, 0 failed

LIVE
----
Test Dres again before CREATE.

Success indication:
- TARGET-SOI SHOOTING appears
- PREDICT ENCOUNTER
- SAFE FLYBY

Then create the normal node and verify KSP ENCOUNTER YES.

If Dres still does not reach TARGET-SOI SHOOTING after this build, stop there.
The next diagnostic build will expose the exact initial-evaluation rejection
stage on MAP; do not push a failing build.

KSP Plugin DLL replacement: NOT REQUIRED.
