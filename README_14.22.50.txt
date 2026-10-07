KMC 14.22.50 — Safe Propagation Revert + Target-SOI Failure Diagnostics
=======================================================================

BASE
----
Apply this overlay directly on top of the current unpushed 14.22.49 local tree.
Do NOT reset the repository first.

FIX 1 — REMOVE THE 14.22.49 CRASH PATH
--------------------------------------
StateVectorPropagator.cs is restored to the frozen 14.22.46/14.22.48
single-step universal-variable implementation.

Removed:
- automatic 2/4/8/16/32/64/128/256 segmented recovery
- up to 510 extra propagation segments for a single failed propagation

This removes the multiplicative recovery path that could hang/crash the UI
inside target-SOI search.

FIX 2 — EXPOSE THE ORIGINAL DRES REJECTION
-------------------------------------------
TargetSoiShootingSolver now has a diagnostic overload. If the initial shooting
seed cannot be evaluated, it performs ONE bounded diagnostic replay using the
normal propagator and reports the failed stage.

Possible MAP diagnostics include:
- INVALID INPUT / MISSING BODY DATA
- ARRIVAL UT <= BURN UT
- PARKING STATE PROPAGATION
- PARKING P/N/R FRAME
- SOURCE SOI EXIT
- ARRIVAL BEFORE SOURCE SOI EXIT
- ORIGIN PARENT STATE AT EXIT
- PARENT-FRAME COAST TO ARRIVAL
- DESTINATION STATE AT ARRIVAL
- NONFINITE ARRIVAL ASSESSMENT
- ASSESSMENT FINALIZATION

MapPage now displays:

  TARGET-SOI SHOOTING REJECTED
  <exact stage>

instead of silently falling back with no explanation.

SCOPE
-----
No KSP Plugin DLL change.
No body-specific logic.
No changed transfer objective.
No asynchronous UI work yet.
No new segmented propagation.

EXPECTED AUTOMATED TESTS
------------------------
NavigationTests: 28 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

New NavigationTests line:
  PASS target SOI shooting reports rejection diagnostics

Static 14.22.50 diagnostic guards: 4 passed.

LIVE DRES TEST
--------------
1. Open Transfer Planner.
2. Select Dres.
3. KMC should no longer enter the 14.22.49 segmented-propagation stall/crash.
4. Do NOT create the node yet.
5. If shooting still rejects the seed, capture the new:

     TARGET-SOI SHOOTING REJECTED
     <stage>

That stage is the next bug to fix.

Do NOT push 14.22.50 yet.
