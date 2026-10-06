KMC 14.22.37 — Lambert Parking-Orbit Ejection Foundation
===========================================================

BASELINE
--------
Frozen master HEAD:
cf943b4846ca85d228003aae828e1d108594cf3a

PURPOSE
-------
Add the KSP-independent mathematical conversion from a Lambert departure
excess-velocity vector to a local parking-orbit impulsive burn.

This build intentionally does NOT wire the result into MAP yet.

WHY THIS IS A SEPARATE BUILD
----------------------------
14.22.36 proved that KMC can produce sensible real-body Lambert transfer
solutions from KSP telemetry while leaving node authority on the legacy
Hohmann path.

The next difficult step is not UI. It is the 3D geometry between:
- parent-frame Lambert departure v-infinity
- the vessel's body-centered parking-orbit plane
- the hyperbolic departure asymptote
- the local KSP-style prograde / normal / radial burn basis

14.22.37 isolates and tests that math before any runtime display or node path
is changed.

NEW ENGINE TYPES
----------------
LambertParkingOrbitEjectionPlanner
- accepts the full Lambert departure excess vector
- accepts the real parking-orbit elements
- accepts origin-body radius and gravitational parameter
- finds a parking-orbit burn phase compatible with the desired outgoing
  hyperbolic asymptote
- evaluates both geometric branches
- selects the lower-DV branch
- propagates the parking orbit to the burn UT
- computes required hyperbolic periapsis speed
- subtracts the actual parking-orbit velocity
- decomposes the impulse into:
    prograde
    normal
    radial
- reports an asymptote-geometry residual for validation

LambertParkingOrbitEjectionSolution
- burn UT
- offset from Lambert departure epoch
- parking radius / altitude / speed
- v-infinity
- hyperbolic periapsis speed
- hyperbolic eccentricity
- asymptote angle
- prograde DV
- normal DV
- radial DV
- total DV
- geometry residual

CURRENT DOMAIN
--------------
For this first foundation build:
- parking orbit must be elliptic
- parking eccentricity must be <= 0.05
- parking periapsis must remain above the body surface
- zero/nonfinite v-infinity is rejected
- contradictory reference frames are rejected
- asymptote geometry that cannot intersect the parking-orbit plane is rejected

The 0.05 eccentricity limit deliberately matches the current proven legacy
parking-ejection operating domain.

FRAME ASSUMPTION
----------------
The engine assumes the parking-orbit element axes and Lambert parent-frame axes
share the same canonical inertial orientation supplied by KMC's adapter.

That assumption is precisely why this result remains preview-only until we
compare it against KSP.

UNCHANGED RUNTIME
-----------------
14.22.37 does NOT change:
- MAP
- Lambert preview from 14.22.36
- CREATE KSP NODE
- legacy Hohmann parking ejection
- KSP plugin
- shared protocol
- REFINE KSP NODE

No KSP Plugin DLL replacement is required.
No live KSP test is required for 14.22.37.

AUTOMATED TESTS
---------------
The test runner adds a third executable.

Expected after this build:

NavigationTests:
  17 passed, 0 failed

Transfer search:
  3 passed, 0 failed

Lambert ejection:
  4 passed, 0 failed

The new cases validate:
- a planar circular case with an analytic prograde answer
- a genuine out-of-plane v-infinity requiring normal DV
- an inclined parking plane
- invalid/impossible geometry rejection

NEXT
----
14.22.38 should connect the accepted ejection solution to MAP as a comparison
preview only.

For Duna and Eve it should display:
- Lambert burn UT
- prograde / normal / radial DV
- total DV
- geometry residual

The legacy Hohmann node remains CREATE authority until KSP verifies the
Lambert-derived preview.
