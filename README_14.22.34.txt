KMC 14.22.34 — Lambert Solver Foundation
=========================================

BASELINE
--------
Verified master HEAD before build:
4420c4ee820cefa9594869e6a9926828b9f2b398

PURPOSE
-------
Add the first generic Lambert boundary-value solver to the KMC celestial-
mechanics engine without changing the currently accepted MAP transfer path.

This build is deliberately math-only. It gives later transfer-window search
and full-vector maneuver planning a reusable solver while leaving the current
Hohmann/ejection -> KSP verification workflow untouched.

NEW ENGINE API
--------------
KMC.Engine/CelestialMechanics now adds:

- LambertTransferPath
  - ShortWay
  - LongWay

- LambertSolution
  - departure velocity
  - arrival velocity
  - time of flight
  - central gravitational parameter
  - selected path

- LambertSolver.TrySolve(...)
  Inputs:
  - departure position vector
  - arrival position vector
  - positive time of flight
  - positive central-body gravitational parameter
  - short-way or long-way zero-revolution path

  Outputs:
  - departure velocity vector
  - arrival velocity vector

ALGORITHM / NUMERICAL SCOPE
---------------------------
- Universal-variable Lambert formulation.
- Stumpff C(z)/S(z) functions.
- Series evaluation near z=0 to avoid cancellation.
- Bracketed root solve; no unguarded Newton iteration.
- Supports zero-revolution short-way and long-way solutions.
- Works in generic right-handed 3D inertial coordinates.
- Unit-consistent; production KMC uses SI units.

SAFE FAILURE
------------
The solver returns false/no solution for:
- nonfinite vectors/time/mu
- zero-length endpoint position vectors
- time of flight <= 0
- mu <= 0
- unsupported path values
- collinear/antiparallel endpoint geometry where the transfer plane is not
  uniquely defined by r1 and r2 alone
- failure to bracket/converge a supported zero-revolution root

Collinear geometry is intentionally rejected rather than guessing a transfer
plane. A later API may accept an explicit reference-plane normal when that
capability is needed.

TEST COVERAGE
-------------
Tools/NavigationTests adds three behavior groups:

1. Canonical circular short-way Lambert transfer:
   r1=(1,0,0), r2=(0,1,0), mu=1, tof=pi/2
   Expected v1=(0,1,0), v2=(-1,0,0).

2. Canonical circular long-way Lambert transfer:
   same endpoints, tof=3pi/2
   Expected v1=(0,-1,0), v2=(1,0,0).

3. Standard Vallado 3D reference case plus energy/angular-momentum agreement
   and invalid/degenerate input rejection.

A Python architecture guard confirms the solver is compiled into KMC.Engine,
remains free of KSP/Unity/MAP dependencies, and is NOT wired into MapPage yet.

UNCHANGED RUNTIME BEHAVIOR
--------------------------
14.22.34 does NOT change:
- MAP display or controls
- current Hohmann transfer-window calculation
- current parking-orbit ejection calculation
- maneuver-node creation
- maneuver uplink protocol
- KSP plugin
- KSP-authoritative encounter/closest-approach assessment
- operator-triggered REFINE KSP NODE behavior

No KSP Plugin DLL rebuild/replacement should be required for this build.
Mission Control behavior should remain identical because MAP does not call the
new Lambert solver yet.

FILES
-----
KMC.Engine/CelestialMechanics/LambertTransferPath.cs
KMC.Engine/CelestialMechanics/LambertSolution.cs
KMC.Engine/CelestialMechanics/LambertSolver.cs
KMC.Engine/KMC.Engine.csproj
Tools/NavigationTests/Program.cs
Tools/NavigationTests/README.md
Tools/OrbitMap/tests/test_14_22_34_lambert_foundation.py
README_14.22.34.txt

NEXT
----
After compile/test acceptance, the next milestone should use KeplerPropagator
and LambertSolver together in a transfer-search layer:

  departure UT x flight time
        -> propagate origin/target states
        -> Lambert solve
        -> compare departure/capture cost
        -> select candidate

That next build should still remain KMC-side first; only after the search is
validated should MAP begin using Lambert-generated maneuver candidates.
