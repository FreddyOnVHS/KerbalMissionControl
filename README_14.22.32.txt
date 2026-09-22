KMC 14.22.32 — Operator-Triggered Transfer Refinement
======================================================

BASELINE
--------
Frozen master verified before build:
a21712b5c1e8477df7ce8e84071cbf9467da58cb

PURPOSE
-------
Take the KSP-authoritative miss-distance signal from 14.22.31 and make the
first controlled refinement pass against the real stock KSP maneuver node.

14.22.32 is deliberately operator-triggered. It does not continuously or
silently retune the maneuver.

TRANSFER PAGE
-------------
After a node exists and KSP has produced a transfer assessment:

  [ NODE CREATED ]   [ REFINE KSP NODE ]

REFINE KSP NODE is available only while:
- the tracked node still exists
- KSP transfer assessment is available
- KSP does not already report a target encounter

When KSP reports a real target SOI patch, the second control becomes:

  [ ENCOUNTER ACHIEVED ]

and is inactive.

REFINEMENT SEARCH
-----------------
One explicit click starts one bounded search on the KSP plugin's Unity thread.

The first implementation varies only:
- maneuver node UT
- prograde delta-v

It preserves:
- normal delta-v
- radial delta-v

Search envelope:
- 6 refinement rounds
- initial UT step: +/-1800 s
- initial prograde step: +/-40 m/s
- step sizes halve each round
- minimum UT step: 45 s
- minimum prograde step: 1 m/s

Each trial:
1. changes the existing stock KSP node
2. calls patchedConicSolver.UpdateFlightPlan()
3. evaluates the actual KSP node.nextPatch chain using the 14.22.31
   KSP-authoritative closest-approach assessment
4. retains the trial only if it improves the result

If KSP creates a patch whose referenceBody is the selected destination, the
search stops early and keeps that encounter solution.

If no better UT/prograde combination is found, the original/best node is
restored and retained.

NODE VERIFICATION
-----------------
When refinement finishes, the accepted node values become the tracked planned
values. The normal node-state path then verifies the refined stock KSP node.

The same PlanId is retained; refinement changes the existing node rather than
creating a second maneuver node.

PROTOCOL
--------
KMC-MNV1 remains backward compatible.

ManeuverUplinkPacket now appends an optional Operation field:
- CREATE (default)
- REFINE

The parser still accepts:
- legacy 7-field packets
- 14.22.31 8-field packets
- 14.22.32 9-field packets

SAFETY / SCOPE
--------------
14.22.32 does NOT:
- run refinement automatically in the background
- create repeated nodes during refinement
- vary radial or normal DV
- solve arbitrary Lambert transfers
- handle hierarchy-change routes
- execute the burn

The operator must explicitly click REFINE KSP NODE.

FILES
-----
KMC.shared/ManeuverUplinkPacket.cs
KMC.Plugin/ManeuverUplinkReceiver.cs
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_32_operator_triggered_transfer_refinement.py
README_14.22.32.txt

BUILD / DEPLOY
--------------
Rebuild:
1. KMC.shared
2. KMC.Plugin
3. KMC.MissionControl

Replace:
  GameData/KMC/Plugins/KMC.Plugin.dll

Mission Control must also use the rebuilt KMC.shared.dll.

RUNTIME ACCEPTANCE
------------------
Start from a new near-circular equatorial Kerbin parking orbit.

1. Select Duna.
2. CREATE KSP NODE.
3. Wait for NODE VERIFIED and KSP TRANSFER ASSESSMENT.
4. Confirm ENCOUNTER is NO for the initial approximate solution.
5. Record initial CLOSEST APPROACH / OUTSIDE SOI.
6. Click REFINE KSP NODE once.
7. KSP should retain one maneuver node, not create a second node.
8. Wait for node-state telemetry to settle.
9. Confirm the closest approach improves OR KSP reports ENCOUNTER YES.
10. If ENCOUNTER YES, confirm KSP Map View actually shows a Duna SOI encounter.
11. If still NO, another explicit REFINE click may be tested from the improved
    node, but no automatic repeated refinement should occur.
12. LOCAL MAP behavior must remain unchanged.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? YES


14.22.32 UI REFINEMENT — PAIRED BUTTON WIDTHS
----------------------------------------------
The lower-right paired node controls no longer use a fixed 220 px width.

Mission Control now measures the actual SmallFont text width for:
- NODE CREATED
- NODE UPLINKED
- REFINE KSP NODE
- ENCOUNTER ACHIEVED

When panel space is available, each button receives its required width.
Only if the panel is genuinely too narrow are the two widths proportionally
compressed to fit.

No maneuver math, search behavior, protocol behavior, or KSP authority logic
changed in this refinement.
