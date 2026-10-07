KMC 14.22.53 — MechJeb-Style Finite-SOI Candidate Ranking
=================================================================

BASE
----
Built from the exact user-supplied local 14.22.52 diagnostic tree.

ROOT CAUSE CONFIRMED
--------------------
ParkingOrbitAwareLambertSearch.IsBetter() ranked candidates in this order:

  1. lowest FiniteSoiDepartureAssessment.NormalizedStateError
  2. lowest ejection DV only when state errors were essentially tied

Live Dres therefore promoted a ~17.8 km/s departure seed because it had a
better source-SOI state match. The 14.22.52 shooting optimizer then correctly
stayed near that already-bad seed.

MECHJEB-STYLE FIX
-----------------
The source-SOI handoff is now treated as a FEASIBILITY CONSTRAINT.

A coarse candidate is production-feasible only when:

  position error <= 10% of source SOI radius
  velocity error <= 10% of Lambert departure excess speed

These are dimensionless, body-independent bootstrap tolerances.

Among feasible candidates:

  LOWEST DEPARTURE DV WINS

Finite-SOI normalized state error is now only a tie-breaker when departure DV
is effectively tied.

If NO coarse candidate satisfies the finite-SOI constraint, the parking-aware
search returns false. MapPage then uses the already-existing direct 3D
Lambert -> target-shooting bootstrap rather than promoting a pathological,
low-error/high-DV candidate.

FINITE-SOI LOCAL CORRECTION
---------------------------
The local correction stage remains a feasibility refinement, but it now has an
explicit total-DV guard:

  corrected DV <= 1.35 x its selected seed DV

This prevents the continuity-refinement stage from recreating the same runaway
behavior after a sensible seed has been selected.

MAP DIAGNOSTIC
--------------
When a parking-aware seed is used, TARGET-SOI SHOOTING shows:

  SOI SEED POS x.x%  VEL x.x%  FEASIBLE / MIN-DV

EXPECTED TESTS
--------------
NavigationTests: 31 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE DRES ACCEPTANCE
--------------------
Before CREATE KSP NODE:

- no hang/crash
- production seed should no longer be ~17.8 km/s
- if parking-aware candidate is used, source-SOI seed shows FEASIBLE / MIN-DV
- if no coarse candidate is feasible, direct 3D bootstrap should take over
- TARGET-SOI SHOOTING should remain within its displayed 1.50x DV cap
- PREDICT ENCOUNTER
- periapsis/B-plane error should improve

Do not push until live Dres passes. If this build passes Dres plus spot checks
for Duna/Eve, this is a strong candidate to commit and push so GitHub becomes
the new source of truth again.

KSP Plugin DLL replacement: NOT REQUIRED.
