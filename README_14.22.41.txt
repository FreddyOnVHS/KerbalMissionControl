KMC 14.22.41 — Finite-SOI Departure Matching
===============================================

BASELINE
--------
Built on the local, unpushed 14.22.40 diagnostic build.
Last frozen Git baseline:
a02b563907895bd3541331a1661667b86a0cb1c6

WHY
---
14.22.40 proved the exact P/N/R Lambert test node reaches KSP unchanged.

KSP results:
- Duna: near miss, ~22.8 Mm outside SOI
- Eve: large miss, ~551 Mm outside SOI

That means the remaining problem is trajectory construction, not uplink.

MODEL CHANGE
------------
Earlier ranking optimized the local parking-orbit burn.

14.22.41 evaluates the actual finite patched-conic handoff:

1. solve Lambert
2. calculate local parking-orbit ejection
3. reconstruct the post-burn body-centered state
4. propagate the escape to the live origin SOI radius
5. propagate the origin body to the same UT
6. transform the escape state into the parent frame
7. propagate the desired Lambert trajectory to the same UT
8. compare parent-frame position and velocity
9. rank candidates by normalized finite-SOI state error
10. use local ejection DV as the secondary score

No stock-body constants or body-specific exceptions are used.

NEW ENGINE
----------
StateVectorPropagator
- universal-variable Cartesian two-body propagation
- elliptic/hyperbolic support

FiniteSoiDepartureEvaluator
- finds the first SOI exit
- produces:
  exit UT
  burn-to-exit time
  position error
  velocity error
  normalized state error

ParkingOrbitAwareLambertSearch
- now ranks by:
  1. finite-SOI state error
  2. local ejection DV
  3. arrival V-infinity

MAP
---
Adds:

FINITE-SOI MATCH / TEST NODE SOURCE
EXIT UT ...
SOI POS ERR ... VEL ERR ...
STATE SCORE ...

CREATE LAMBERT TEST NODE automatically uses this newly ranked candidate.

Normal CREATE KSP NODE remains legacy Hohmann authority.
Lambert test refinement remains disabled.

DLL / PROTOCOL
--------------
KSP Plugin DLL replacement: NOT REQUIRED.
KMC.shared change: NONE.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 21 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE TEST
---------
Use a clean maneuver-node list.

Test Duna:
- record finite-SOI position error
- velocity error
- state score
- CREATE LAMBERT TEST NODE
- record KSP encounter / closest approach

Delete node, then repeat for Eve.

DO NOT PUSH solely because the state score is lower.

Acceptance remains KSP patched-conic behavior.

NEXT
----
This build still only SELECTS the candidate with the best finite-SOI handoff.

If Duna/Eve improve but still miss, the next build should use the same
FiniteSoiDepartureEvaluator as the objective for a generic local correction
optimizer over:
- burn UT
- prograde DV
- normal DV
- radial DV

That optimizer can drive finite-SOI state error toward zero without any
body-specific tuning.
