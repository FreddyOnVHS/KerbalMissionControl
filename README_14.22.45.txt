KMC 14.22.45 — Target-SOI Shooting Solver
=============================================

BASE
----
Frozen Git baseline:
487c1c6af70c3374bf1981acdbe892c805ac9d57 (14.22.42)

Local chain:
14.22.43 cleanup (unpushed)
14.22.44 finite-SOI local correction (unpushed)
14.22.45 target-SOI shooting (this build)

WHY
---
14.22.44 improved the later-epoch Duna miss from ~69.20 Mm outside the SOI
to ~26.71 Mm outside, but still failed to produce an encounter.

The remaining problem was architectural:
the optimizer minimized source-SOI handoff quality, not actual target miss.

MechJeb's InterplanetaryTransfer solver treats the transfer as a coupled
boundary-value problem from the parking orbit, through the source SOI, to the
target SOI.

14.22.45 adopts that principle for KMC.

PIPELINE
--------
Lambert remains the bootstrap:

  Hohmann search center
    -> coarse Lambert search
    -> parking-orbit ejection
    -> finite-SOI local correction
    -> TARGET-SOI SHOOTING
    -> production node
    -> KSP verification

TARGET-SOI SHOOTING
-------------------
Variables:
- burn UT
- prograde DV
- normal DV
- radial DV
- arrival UT

Each trial:
1. propagates the parking orbit to burn UT
2. applies the full P/N/R impulse
3. propagates through source-body gravity to the real source SOI
4. converts the state into the parent frame
5. propagates the actual spacecraft parent-frame state to trial arrival UT
6. propagates the destination body to the same UT
7. measures ACTUAL TARGET MISS DISTANCE
8. checks miss distance against the live target SOI radius

Lambert is also solved from the actual source-SOI exit position to the live
target position as a secondary continuity diagnostic.

PRIMARY OBJECTIVE
-----------------
Minimize:

  | spacecraft_position(arrival) - target_position(arrival) |

The solver prefers lower actual miss distance. Source-SOI Lambert velocity
mismatch and total DV are only tie breakers.

This means the optimizer is now directly solving the encounter geometry that
KSP ultimately cares about.

NO BODY-SPECIFIC LOGIC
----------------------
The solver contains no stock body names or stock constants.

It consumes:
- live source orbit / μ / SOI
- live destination orbit / SOI
- live parent μ
- current parking orbit
- Lambert seed

This remains compatible with arbitrary same-parent modded celestial systems.

MAP
---
New block:

  TARGET-SOI SHOOTING
  MISS <initial> -> <corrected> / SOI <target soi>
  ARR UT <...>  VERR <...>  PREDICT ENCOUNTER|MISS
  ITER <...>  EVAL <...>

The production ejection P/N/R and burn UT displayed above are the final
shooting-corrected values.

PRODUCTION AUTHORITY
--------------------
CREATE KSP NODE now prefers:
1. target-SOI shooting corrected ejection
2. finite-SOI corrected ejection
3. coarse finite-SOI Lambert ejection
4. legacy Hohmann fallback

KSP Plugin DLL replacement: NOT REQUIRED.
Shared protocol change: NONE.
Mission Control + Engine rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Expected:
NavigationTests: 24 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE ACCEPTANCE
---------------
Use the same later-epoch Duna state if possible.

Before creating the node:
- TARGET-SOI SHOOTING should appear
- send a screenshot showing:
  - initial miss
  - corrected miss
  - target SOI
  - PREDICT ENCOUNTER or PREDICT MISS
  - final P/N/R

If it says PREDICT ENCOUNTER:
- click normal CREATE KSP NODE
- KSP should report NODE VERIFIED and ENCOUNTER YES

If KMC says PREDICT ENCOUNTER but KSP says ENCOUNTER NO, do not push and send
both screenshots. That would isolate the remaining difference to our parent-
frame propagation versus KSP patched-conic propagation.

Do not push 14.22.45 until live KSP acceptance passes.
