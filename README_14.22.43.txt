KMC 14.22.43 — Transfer Planner Production Cleanup
=====================================================

BASELINE
--------
Frozen 14.22.42:
487c1c6af70c3374bf1981acdbe892c805ac9d57

PURPOSE
-------
Clean up the TRANSFER planner now that normal CREATE KSP NODE has been live-
validated with finite-SOI Lambert authority for both Duna and Eve.

REMOVED
-------
The temporary 14.22.40/14.22.42 validation path is removed:

- CREATE LAMBERT TEST NODE button
- Lambert test click target
- UploadLambertTestNode()
- BuildLambertTestNodePacket()
- MAP-LAMBERT-TEST plan IDs
- Lambert-test-specific submission state
- Lambert-test-specific no-refine label

NORMAL WORKFLOW
---------------
There is now one maneuver creation action:

  CREATE KSP NODE

Its authority remains exactly as established in 14.22.42:

1. finite-SOI-ranked parking-aware Lambert ejection when available
   - burn UT
   - prograde DV
   - normal DV
   - radial DV

2. legacy Hohmann/prograde-only fallback when Lambert is unavailable

LEGACY FALLBACK DISPLAY
-----------------------
The legacy candidate is still calculated so production fallback remains
available.

When a valid Lambert production solution exists, the legacy ejection text is
not shown. This keeps the normal page focused on the authoritative production
solution.

If Lambert is unavailable and the legacy path is usable, the planner may show:

  LEGACY FALLBACK

LABEL CLEANUP
-------------
Old development labels are replaced:

  LAMBERT PREVIEW  COARSE 9x9 / NO NODE AUTHORITY
becomes
  LAMBERT SEARCH  COARSE 9x9

  PARKING-AWARE LAMBERT / NO NODE AUTHORITY
becomes
  LAMBERT EJECTION / PRODUCTION

  FINITE-SOI MATCH / PRODUCTION SOURCE
becomes
  FINITE-SOI MATCH

REFINEMENT
----------
Production Lambert nodes remain protected from the old UT/prograde-only
refiner:

  LAMBERT AUTHORITY - NO REFINE

Legacy fallback nodes retain the previous refinement behavior.

UNCHANGED
---------
- Lambert solver
- parking-orbit ejection math
- finite-SOI evaluator/ranking
- production BuildProductionNodePacket behavior
- legacy fallback behavior
- font-safe TRANSFER layout
- KSP plugin
- shared protocol

KSP Plugin DLL replacement: NOT REQUIRED.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 22 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

The former test-packet test is replaced with a cleanup test confirming that
the temporary test methods are gone while the production packet builder
remains.

LIVE ACCEPTANCE
---------------
This is primarily a cleanup build.

Recommended:
1. Open MAP -> TRANSFER.
2. Select Duna.
3. Confirm there is only one CREATE KSP NODE button.
4. Confirm the page shows production-facing Lambert labels.
5. Click CREATE KSP NODE.
6. Confirm NODE VERIFIED and ENCOUNTER YES.

One Duna live test is sufficient because 14.22.43 does not change navigation
math or production packet construction.

Do not push until the automated suite and this UI/runtime check pass.
