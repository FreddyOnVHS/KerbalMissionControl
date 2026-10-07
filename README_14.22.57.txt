KMC 14.22.57 — Coupled Finite-SOI Interplanetary Optimizer Foundation
=====================================================================

BASE
----
Built from the user-supplied local navigation tree containing the 14.22.56
B-plane experiments. GitHub master remains frozen at 14.22.46 until a later
build is deliberately promoted and pushed.

ARCHITECTURE CHANGE
-------------------
This build does NOT add another patch to TargetSoiShootingSolver.

It introduces a new, independent coupled finite-SOI solver path in SHADOW MODE:

  parking orbit
    -> departure burn
    -> source SOI exit
    -> parent-frame transfer
    -> target SOI entry
    -> target hyperbola
    -> requested periapsis

The new formulation follows the proven MechJeb architecture conceptually:

OBJECTIVE
  minimize departure DV

CONSTRAINTS
  source finite-SOI continuity
  source SOI exit outbound
  target SOI encounter/interface
  target SOI entry inbound
  target requested periapsis

STAGED FOUNDATION
-----------------
1. Lambert + proven 3D hyperbolic departure is the bootstrap.
2. Evaluate source finite-SOI feasibility first.
3. If source feasibility is usable, initialize target geometry from the
   requested periapsis B-plane.
4. Evaluate the complete source -> parent -> target trajectory as one candidate.
5. Keep departure DV as the objective after feasibility constraints are tied.

IMPORTANT: 14.22.57 establishes the coupled request/result/evaluator contract
and target B-plane initialization. It does NOT yet claim the final nonlinear
constrained optimizer is complete. That optimizer can now be added without
changing production maneuver authority or extending the old coordinate-search
solver.

SHADOW DIAGNOSTICS
------------------
The transfer page can now display:

  COUPLED FINITE-SOI OPTIMIZER / SHADOW
  BOOTSTRAP DV / FINAL DV
  FEASIBILITY PASS/FAIL
  SOURCE IF POS / VEL / OUT
  TARGET IF ERR / INBOUND
  TARGET PE ERR / B-PLANE INIT
  ITER / EVAL / NO NODE AUTHORITY

AUTHORITY SAFETY
----------------
The shadow result NEVER replaces _lambertEjectionPreview.
CREATE KSP NODE continues to use the existing production path.
No KSP plugin DLL change is required.

DO NOT PUSH YET.

NEXT
----
Run automated tests, then live Duna/Eve/Dres diagnostics. The next optimizer
iteration should replace the initializer with the true coupled nonlinear
constrained solve, then validate the fixed six-case matrix before promotion.
