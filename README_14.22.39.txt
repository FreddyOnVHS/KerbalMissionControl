KMC 14.22.39 — Parking-Orbit-Aware Lambert Search
====================================================

BASELINE
--------
Built directly on the local, unpushed 14.22.38 diagnostic build.
Last frozen Git baseline:
919c73641612bfc7b2a07d3bd16c3806f8f18441

PURPOSE
-------
14.22.38 exposed a selection problem on Kerbin -> Eve.

The old Lambert search ranked candidates by:
  departure V-infinity + arrival V-infinity

A candidate can score well there but still require a very expensive local
parking-orbit plane change.

14.22.39 evaluates every sampled Lambert candidate through the accepted
LambertParkingOrbitEjectionPlanner and ranks candidates by the actual local
parking-orbit impulse.

PRIMARY SCORE
-------------
Lowest total local parking-orbit ejection DV.

SECONDARY TIE-BREAKER
---------------------
Lower arrival V-infinity.

Further deterministic ties:
- earlier departure
- shorter flight time
- short-way path

This is still a departure-side optimizer. Arrival capture cost is not yet
modeled.

MAP
---
The old V-infinity-scored Lambert preview remains visible for comparison.

The ejection section now uses the parking-aware winner and is labeled:

  PARKING-AWARE LAMBERT / NO NODE AUTHORITY

It displays:
- selected path
- selected departure UT
- arrival V-infinity
- burn UT / offset
- P/N/R DV
- EJECT SCORE
- geometry residual

NODE AUTHORITY
--------------
UNCHANGED.

CREATE KSP NODE still uses the legacy Hohmann candidate with:
- legacy node UT
- legacy prograde DV
- normal DV = 0
- radial DV = 0

No KSP Plugin DLL replacement is required.
No KMC.shared protocol change is required.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 19 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed

LIVE ACCEPTANCE
---------------
Test Duna, then Eve.

For Eve, compare against 14.22.38:
- selected path
- departure UT
- P/N/R
- ejection score

The key question is whether the huge normal-DV component and ~2.6 km/s
ejection cost are materially reduced.

ENCOUNTER NO is still expected for the legacy CREATE node in this build.

NEXT
----
If the Eve result improves materially, add a separate controlled
CREATE LAMBERT TEST NODE path for KSP patched-conic validation before changing
normal CREATE authority.
