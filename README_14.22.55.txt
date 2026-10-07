KMC 14.22.55 — Mandatory B-Plane Terminal Initialization
==========================================================

BASE
----
Apply on top of local 14.22.54.

DUNA REGRESSION
---------------
14.22.54 kept the departure solution sane and still predicted an encounter,
but Duna's arrival geometry regressed badly:

  desired Pe radius ~640 km
  predicted Pe radius ~18.91 Mm
  B-plane error ~18.23 Mm

The B-plane bootstrap was being treated as an optional candidate. It was only
used if it immediately scored better than the existing center-targeted
encounter.

That is not how the MechJeb initialization concept is intended to work.

FIX
---
Once encounter feasibility exists:

1. construct the desired-periapsis B-plane bootstrap;
2. require it to remain inside the existing 1.50x departure-DV trust region;
3. evaluate it;
4. if valid, make it the TERMINAL SOLVE INITIAL STATE unconditionally;
5. then run the bounded periapsis-refinement pass from that arrival basin.

The B-plane initialization no longer needs to "win" before the terminal
optimizer has had a chance to refine it.

MAP
---
When this initialization is applied, the page now shows:

  BPLANE INIT

EXPECTED
--------
NavigationTests: 33 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected line:

  PASS target terminal solve initializes from bounded B-plane bootstrap

LIVE ORDER
----------
1. Duna pre-node regression
2. If Duna arrival Pe is sane, CREATE KSP NODE and require ENCOUNTER YES
3. Then Eve regression
4. Then Dres spot check if needed
5. If all pass, commit/push and establish a new GitHub baseline

KSP Plugin DLL replacement: NOT REQUIRED.
Do not push until Duna/Eve live regressions pass.
