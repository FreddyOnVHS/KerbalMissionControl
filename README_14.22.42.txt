KMC 14.22.42 — Lambert Production Node Authority
===================================================

BASELINE
--------
Frozen 14.22.41:
df24d331bc69ccf458a9d87d05a1665445ede508

PURPOSE
-------
Promote the finite-SOI-ranked parking-aware Lambert solution from test-only
node creation to the normal CREATE KSP NODE path.

NORMAL CREATE AUTHORITY
-----------------------
CREATE KSP NODE now chooses:

1. Finite-SOI-ranked parking-aware Lambert solution, when available.
   Node uses exact:
   - Lambert burn UT
   - prograde DV
   - normal DV
   - radial DV

2. Legacy Hohmann/prograde-only candidate, only when the Lambert production
   candidate is unavailable.

The fallback remains generic and preserves the previous proven behavior.

TEST BUTTON
-----------
CREATE LAMBERT TEST NODE remains visible in this milestone.

This is intentional. It lets the live acceptance test compare the production
CREATE path against the already-proven test path before the test control is
removed in a cleanup milestone.

NODE TRACKING
-------------
Production Lambert nodes now have their own submitted-authority state so:

- NODE VERIFIED remains locked to the full P/N/R Lambert vector.
- normal live solution drift is compared against the Lambert ejection, not
  against the legacy Hohmann candidate.
- test Lambert nodes remain separately identifiable.

REFINEMENT
----------
The old KSP-node refinement routine changes UT/prograde only.

It is therefore NOT allowed to modify:
- Lambert test nodes
- production Lambert authority nodes

UI shows:
  LAMBERT AUTHORITY - NO REFINE

RefineTransferNode also has a defensive code-level guard even if invoked by
another path.

Legacy fallback nodes may still use the old refinement behavior.

UI
--
The final font-safe 14.22.41 TRANSFER layout is preserved.

The finite-SOI label now reads:
  FINITE-SOI MATCH / PRODUCTION SOURCE

DLL / PROTOCOL
--------------
KSP Plugin DLL replacement: NOT REQUIRED.
KMC.shared protocol change: NONE.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 22 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

The new NavigationTests case checks:
- production CREATE prefers exact Lambert P/N/R
- legacy fallback remains available
- fallback remains zero-normal / zero-radial

LIVE ACCEPTANCE
---------------
Use a clean maneuver-node list.

DUNA:
1. Select Duna.
2. Click normal CREATE KSP NODE — NOT the Lambert test button.
3. Confirm:
   - NODE VERIFIED
   - KSP NODE MATCHES UPLINKED PLAN
   - ENCOUNTER YES
4. Confirm the node has the full Lambert P/N/R vector.

EVE:
1. Delete the Duna node.
2. Select Eve.
3. Click normal CREATE KSP NODE.
4. Confirm the same:
   - NODE VERIFIED
   - KSP NODE MATCHES UPLINKED PLAN
   - ENCOUNTER YES

Do not push until both production CREATE tests pass.

NEXT
----
After normal CREATE succeeds for Duna and Eve:
- remove CREATE LAMBERT TEST NODE
- simplify the TRANSFER labels
- retain legacy Hohmann as silent fallback
- then consider capture/arrival optimization and broader planet regression
  coverage.
