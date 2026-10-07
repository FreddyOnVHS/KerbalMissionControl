KMC 14.22.56 — Target-SOI B-Plane Constraint Refinement
=========================================================

BASE
----
Apply on top of local 14.22.55b.

WHY
---
Duna still predicted an encounter but missed the requested periapsis badly:

  desired Pe radius ~640 km
  predicted Pe radius ~6.50 Mm
  B-plane error ~5.86 Mm

The one-shot B-plane bootstrap was not sufficient. KMC still returned to a
P/N/R coordinate search that did not explicitly preserve target-SOI geometry.

MECHJEB-ALIGNED FIX
-------------------
KMC now carries the live target-relative SOI-entry velocity from the known
encounter into a bounded terminal B-plane search.

The terminal search varies:

  - target B-plane azimuth: 16 samples
  - arrival epoch: 7 samples
  - 3 progressively tighter refinement passes

For every trial:

1. convert desired periapsis to B-plane impact parameter;
2. construct the finite target-SOI entry point;
3. propagate the destination to the trial arrival epoch;
4. solve Lambert from the origin departure state to that finite SOI point;
5. convert departure v-infinity to the general 3D parking-orbit ejection;
6. reject anything outside the 1.50x departure-DV trust region;
7. run KMC's full finite-SOI trajectory assessment;
8. reject anything that loses the target encounter;
9. rank under the periapsis constraint objective.

The pre-existing feasible encounter remains the incumbent fallback.

MAP
---
When this search improves the terminal solution, diagnostics show:

  BPLANE SEARCH

EXPECTED
--------
NavigationTests: 35 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected line:
PASS target terminal search varies B-plane azimuth and arrival epoch

LIVE ORDER
----------
1. Duna pre-node regression
2. Require sane DV, PREDICT ENCOUNTER, and much smaller Pe/BERR
3. Create Duna node and require KSP ENCOUNTER YES
4. Eve regression
5. Dres regression spot-check
6. If clean, commit and push this navigation stack as the new GitHub baseline

KSP Plugin DLL replacement: NOT REQUIRED.
Do not push until live regressions pass.
