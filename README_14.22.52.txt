KMC 14.22.52 — MechJeb-Style Encounter Optimization Foundation
==================================================================

BASE
----
Apply on top of the clean local 14.22.51 state.

DESIGN DIRECTIVE
----------------
For interplanetary encounters KMC now follows the problem structure proven by
MechJeb, without copying its implementation line-for-line:

- encounter/periapsis are feasibility constraints;
- departure impulse is the optimization objective;
- establish a feasible trajectory before terminal shaping;
- use B-plane geometry to guide target-periapsis shaping.

WHY
---
Dres finally reached TARGET-SOI SHOOTING in 14.22.51, but the old KMC
lexicographic search could purchase a better encounter by allowing departure
DV to grow without an explicit seed-relative trust region. The live result
reached ~18.24 km/s and still had poor periapsis targeting.

CHANGES
-------
1. Two explicit shooting passes

   PASS 1 — FEASIBILITY
   Establish target-SOI encounter only.

   PASS 2 — TERMINAL GEOMETRY
   Once encounter exists, shape the arrival hyperbola toward the requested
   periapsis.

2. Seed-derived departure-DV trust region

   The shooting correction cannot exceed 1.50x the Lambert/3D ejection seed.
   This is generic and destination-independent.

   A ~3 km/s seed therefore cannot turn into an ~18 km/s "better encounter."

3. DV becomes the final objective

   Once the target encounter and periapsis constraints are feasible, lower
   departure DV wins. Constraint satisfaction no longer permanently outranks
   an arbitrarily expensive burn.

4. Target B-plane geometry

   KMC now computes:
   - current transverse B-plane radius at target SOI entry;
   - desired impact parameter corresponding to requested periapsis;
   - B-plane error.

   The terminal-geometry pass uses normalized periapsis error plus B-plane
   error while the constraint is still infeasible.

5. MAP diagnostics

   TARGET-SOI SHOOTING now shows:

     DV SEED xxxx.x -> FINAL xxxx.x m/s  CAP xxxx.x

   and, when available:

     TARGET PE R ...  PRED PE R ...  BERR ...

EXPECTED TESTS
--------------
NavigationTests: 31 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected lines:

  PASS target SOI optimization keeps DV as constrained objective
  PASS target B-plane maps desired periapsis to impact parameter

LIVE DRES ACCEPTANCE
--------------------
Before CREATE KSP NODE:

- no UI hang/crash
- TARGET-SOI SHOOTING
- PREDICT ENCOUNTER
- DV FINAL <= displayed CAP
- final DV must not reproduce the ~18.24 km/s runaway
- periapsis / B-plane error should improve meaningfully

If no encounter can be obtained inside the trust region, that is a useful
result. Do NOT relax the guard blindly. The next MechJeb-derived step would be
to improve the finite-SOI/B-plane bootstrap itself rather than purchasing the
encounter with excess DV.

KSP Plugin DLL replacement: NOT REQUIRED.
Do not push yet.
