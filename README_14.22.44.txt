KMC 14.22.44 — Finite-SOI Local Correction
================================================

BASELINE
--------
Frozen Git baseline:
487c1c6af70c3374bf1981acdbe892c805ac9d57  (14.22.42)

Local parent:
14.22.43 Transfer Planner Production Cleanup (UNPUSHED)

WHY THIS BUILD EXISTS
---------------------
14.22.42 proved normal CREATE KSP NODE could produce Duna and Eve encounters.

During the 14.22.43 cleanup acceptance run at a later game epoch, Duna produced:

  NODE VERIFIED
  KSP NODE MATCHES UPLINKED PLAN
  ENCOUNTER NO
  CLOSEST APPROACH ~117.13 Mm
  TARGET SOI ~47.92 Mm
  OUTSIDE SOI ~69.20 Mm

The packet/uplink path was therefore correct. The selected coarse Lambert
candidate simply did not have enough finite-SOI accuracy at that epoch.

14.22.44 addresses that generic trajectory robustness problem.

NEW ENGINE
----------
FiniteSoiDepartureOptimizer

Input:
- selected Lambert transfer
- selected parking-orbit ejection
- live parking orbit
- live origin-body μ/SOI
- live parent μ

Variables:
- burn UT
- prograde DV
- normal DV
- radial DV

Objective:
- FiniteSoiDepartureAssessment.NormalizedStateError

Method:
- deterministic bounded pattern search
- axial +/- trial moves
- step reduction when a pass finds no improvement
- maximum 36 iterations
- generic bounds derived from parking period and ejection magnitude
- no stock body names or body-specific constants

The optimizer never accepts a candidate with a worse finite-SOI score.

PRODUCTION AUTHORITY
--------------------
The normal CREATE KSP NODE path now uses:

  corrected finite-SOI ejection
      if correction is available

otherwise:

  coarse finite-SOI Lambert ejection

otherwise:

  existing legacy Hohmann fallback

The temporary Lambert test button remains removed from the 14.22.43 cleanup.

MAP DIAGNOSTICS
---------------
The production ejection block displays the corrected burn UT and corrected
P/N/R.

The finite-SOI block now exposes correction performance:

  FINITE-SOI LOCAL CORRECTION
  SCORE <initial> -> <corrected>  ITER <n>  EVAL <n>
  POS ERR ...
  VEL ERR ...
  EXIT UT ...

If the optimizer finds no better point, the heading remains FINITE-SOI MATCH
and the production solution is not worsened.

UNCHANGED
---------
- Lambert solver
- coarse 9x9 transfer search
- parking-aware coarse candidate selection
- KSP plugin
- shared protocol
- legacy fallback
- font-safe transfer-page layout
- production node packet format

KSP Plugin DLL replacement: NOT REQUIRED.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 23 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE ACCEPTANCE
---------------
Use the same current game state that failed Duna in 14.22.43 if possible.

1. Open MAP -> TRANSFER -> Duna.
2. Before creating the node, record:
   - initial score
   - corrected score
   - corrected P/N/R
3. Click normal CREATE KSP NODE.
4. Confirm:
   - NODE VERIFIED
   - KSP NODE MATCHES UPLINKED PLAN
   - ENCOUNTER YES

If Duna still misses, DO NOT PUSH. Send the transfer page screenshot. The
before/after optimizer diagnostics will tell us whether the local objective is
improving enough or whether the next correction must operate on the Lambert
departure/time-of-flight variables themselves.

Do not push 14.22.44 until live KSP acceptance passes.
