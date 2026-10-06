KMC 14.22.40 — CREATE LAMBERT TEST NODE
=============================================

BASELINE
--------
Frozen master HEAD:
a02b563907895bd3541331a1661667b86a0cb1c6

PURPOSE
-------
Perform the first controlled KSP patched-conic validation of the accepted
parking-orbit-aware Lambert solution.

This build adds a separate test-only control:

  CREATE LAMBERT TEST NODE

The normal CREATE KSP NODE path remains legacy Hohmann authority.

LAMBERT TEST PACKET
-------------------
The test node uses the parking-aware Lambert ejection solution exactly:

Node UT = Lambert parking-ejection burn UT
Prograde DV = Lambert local prograde component
Normal DV = Lambert local normal component
Radial DV = Lambert local radial component
Target = selected destination
Operation = CREATE

Plan IDs use:
  MAP-LAMBERT-TEST-<TARGET>-<TOKEN>

NO PRODUCTION AUTHORITY CHANGE
------------------------------
CREATE KSP NODE remains unchanged:
- legacy Hohmann/ejection node UT
- legacy prograde DV
- normal = 0
- radial = 0

This is intentionally a separate button and separate plan identity.

REFINEMENT
----------
REFINE KSP NODE is deliberately disabled for Lambert test nodes.

If the test node does not encounter the target, KMC shows:
  LAMBERT TEST - NO REFINE

This preserves the exact vector produced by the new navigation engine so KSP
can validate it without the old UT/prograde-only refinement perturbing it.

IMPORTANT LIVE-TEST PROCEDURE
-----------------------------
Before creating a Lambert test node, remove any existing maneuver nodes from
the vessel in KSP.

The current plugin CREATE operation adds a new stock KSP maneuver node; it does
not remove unrelated pre-existing nodes. A clean node list is required so the
patched-conic result measures this Lambert vector alone.

Recommended test sequence:

1. Stable near-circular, low-inclination Kerbin parking orbit.
2. Ensure KSP has NO maneuver nodes.
3. MAP -> TRANSFER -> Duna.
4. Confirm PARKING-AWARE LAMBERT preview is populated.
5. Click CREATE LAMBERT TEST NODE.
6. Confirm KMC shows NODE VERIFIED and KSP NODE MATCHES UPLINKED PLAN.
7. Record:
   - encounter YES/NO
   - closest approach
   - target SOI
   - KSP maneuver-node P/N/R values
8. Delete the test node in KSP.
9. Select Eve.
10. Confirm there are again NO maneuver nodes.
11. Click CREATE LAMBERT TEST NODE.
12. Record the same values.

The Eve case is the key validation because 14.22.39 reduced the selected local
ejection from the pathological ~2.6 km/s result to roughly ~1.085 km/s with
only a small normal component.

AUTOMATED TESTS
---------------
Expected after this build:

NavigationTests: 20 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed

The new NavigationTests case verifies that the test-node packet preserves the
exact parking-aware Lambert burn UT and P/N/R components.

DLL / PROTOCOL
--------------
KSP Plugin DLL replacement: NOT REQUIRED.
KMC.shared protocol change: NONE.
Mission Control rebuild: REQUIRED.

NEXT
----
If KSP confirms the Lambert test nodes produce correct Duna and Eve encounters,
we can decide whether to:
- make parking-aware Lambert the normal CREATE authority, or
- first add an independent KMC-vs-KSP prediction comparison / finer search.

Do not promote Lambert to normal CREATE authority solely because the node loads;
the KSP encounter assessment is the acceptance criterion.
