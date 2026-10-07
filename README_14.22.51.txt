KMC 14.22.51 — Analytic Hyperbolic Source-SOI Exit
=======================================================

BASE
----
Apply on top of the clean local 14.22.50 state.

LIVE FAILURE IDENTIFIED
-----------------------
Dres now reports:

  TARGET-SOI SHOOTING REJECTED
  SOURCE SOI EXIT

The shooting seed therefore fails before any parent-frame transfer or target
encounter calculation.

ROOT CAUSE AREA
---------------
The old source-SOI handoff estimates a future escape time and repeatedly asks
StateVectorPropagator to propagate the post-burn state until the SOI radius is
bracketed.

For a high-energy, strongly 3D hyperbolic departure this can fail numerically
before the SOI boundary is found.

14.22.49 attempted to compensate with global segmented propagation retries.
That caused severe search multiplication, UI stalls, and eventual crashes.

FIX
---
New shared:

  HyperbolicSoiExitSolver

It derives the osculating hyperbola directly from the live post-burn state:

- specific orbital energy
- angular momentum vector
- eccentricity vector
- semi-latus rectum
- hyperbolic true anomaly
- hyperbolic anomaly / mean anomaly

It then solves the future outbound intersection with the source SOI sphere
analytically and constructs the exact two-body exit position/velocity.

No long propagation, no bracketing search, no segmentation, and no
body-specific constants are required.

Both TargetSoiShootingSolver and FiniteSoiDepartureEvaluator now use this
shared analytic path first. Their previous bounded numerical method remains
as compatibility fallback.

EXPECTED TESTS
--------------
NavigationTests: 29 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected line:

  PASS analytic hyperbolic SOI exit handles arbitrary 3D escape

LIVE DRES
---------
Select Dres and stop before CREATE KSP NODE.

Ideal result:
- no UI stall/crash
- TARGET-SOI SHOOTING appears
- PREDICT ENCOUNTER
- SAFE FLYBY

If shooting is still rejected, send the exact new rejection stage. It should
now be downstream of SOURCE SOI EXIT.

KSP Plugin DLL replacement: NOT REQUIRED.
Do not push yet.
