KMC 14.22.54 — MechJeb-Style B-Plane Arrival Bootstrap
==========================================================

BASE
----
Built on the clean local 14.22.53b state.

WHY
---
14.22.53b fixed the pathological Dres departure seed:

  production DV ~2.1 km/s
  target encounter predicted
  source-SOI seed feasible / minimum-DV

But arrival geometry remained poor:

  desired target Pe ~328 km
  predicted target Pe ~8.95 Mm
  B-plane error ~8.62 Mm

KMC was measuring B-plane error but still asking the coordinate optimizer to
discover the correct flyby geometry from a center-targeted encounter.

MECHJEB-STYLE FIX
-----------------
After an encounter is established:

1. use the transfer arrival v-infinity direction;
2. convert desired target periapsis to required B-plane impact parameter;
3. construct upstream target-SOI entry points at that impact parameter;
4. sample 12 B-plane azimuths around the incoming v-infinity direction;
5. solve a Lambert trajectory to each finite target-SOI entry point;
6. convert each departure v-infinity into KMC's general 3D parking ejection;
7. keep the lowest-DV valid bootstrap;
8. accept it only if it stays inside the existing 1.50x shooting DV trust region;
9. run the existing bounded periapsis refinement from that improved seed.

This follows MechJeb's arrival initialization concept without copying its SQP
implementation line-by-line. KMC samples B-plane azimuth because its terminal
optimizer is a bounded coordinate search.

MAP
---
When the B-plane bootstrap is accepted, the target-shooting diagnostics append:

  BPLANE SEED

EXPECTED TESTS
--------------
NavigationTests: 32 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected line:

  PASS target B-plane bootstrap constructs incoming SOI entry

LIVE DRES
---------
Before CREATE KSP NODE:

- production DV remains in the sane ~2-3 km/s class
- PREDICT ENCOUNTER
- BPLANE SEED ideally appears
- predicted Pe and BERR should fall substantially from the 14.22.53b values
- final shooting DV remains under displayed CAP

If BPLANE SEED does not appear, do not relax the DV guard. Inspect why the
finite target-SOI bootstrap was rejected.

KSP Plugin DLL replacement: NOT REQUIRED.
Do not push until Dres live validation passes.
