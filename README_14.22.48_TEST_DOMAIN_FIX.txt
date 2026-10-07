KMC 14.22.48 Test/Domain Fix
================================

Two Lambert-ejection tests failed after the cumulative 14.22.48 package.

FAILURE 1
---------
"Lambert ejection exposes out-of-plane normal component"

Old test assumption:
  radial DV must equal zero.

That assumption belonged to the old projected-in-plane departure model.

A true non-coplanar single-impulse hyperbolic departure is allowed to use
radial, normal, and prograde components simultaneously. The new test now
requires:
- nonzero normal authority
- finite radial authority
- P/N/R magnitude equals total DV
- small hyperbolic geometry residual

FAILURE 2
---------
"Lambert ejection rejects impossible and invalid geometry"

The new general 3D planner accidentally accepted parking eccentricity above the
existing KMC preview domain.

The historical domain limit is restored:

  parking eccentricity <= 0.05

14.22.48 therefore fixes 3D departure geometry without expanding the supported
parking-orbit eccentricity scope.

EXPECTED
--------
NavigationTests: 27 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

After automated tests pass, resume the live Dres regression.

Do not push yet.
