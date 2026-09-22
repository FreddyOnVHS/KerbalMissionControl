KMC 14.22.30 — Transfer Node Creation (Final Refinement)
=========================================================

BASELINE
--------
GitHub master verified before refinement:
413c23fd14989eeb07a58884e69e9f321bb2fc1c
(14.22.29 frozen baseline)

PURPOSE
-------
Finalize the first MAP transfer-planner maneuver-node creation path.

This consolidated package includes:
- MAP CREATE KSP NODE path
- sender-only bridge into the existing maneuver uplink protocol
- legacy KMC.MissionControl.csproj Compile Include fix
- duplicate-node lock after a candidate is uplinked/verified

DUPLICATE NODE SAFETY
---------------------
After CREATE KSP NODE successfully sends a candidate, that same candidate is
locked against repeated clicks.

While ACK/verification is pending:
  [ NODE UPLINKED ]

After KSP reports NODE VERIFIED:
  [ NODE CREATED ]

The control is inactive in both states.

Selecting a different destination resets the submitted-candidate identity.
A materially changed current solution also becomes eligible for a new node.
For stability against tiny live-planner drift, the current candidate is treated
as the same submitted solution while it remains within:
- 120 seconds of submitted node UT
- 5 m/s of submitted prograde DV

If it moves beyond those tolerances the page shows:
  NODE STATUS  NEW SOLUTION READY
and CREATE KSP NODE becomes available again.

ARCHITECTURE
------------
No new KSP command protocol was added.

The transfer planner sends a ManeuverUplinkPacket to the already-existing
ManeuverUplinkPacket.CommandPort.

The existing KSP ManeuverUplinkReceiver remains authoritative for:
- stock ManeuverNode creation
- setting node DeltaV
- UpdateFlightPlan()
- ACK
- NODE VERIFIED telemetry

MissionControlReceiver remains the single ACK/node-state listener.

PROJECT FILE FIX
----------------
KMC.MissionControl uses a legacy explicit-Compile .csproj. This final package
includes:
  <Compile Include="Transport\TransferPlannerManeuverUplink.cs" />

This fixes the CS0103 compile failure seen in the first 14.22.30 package.

FILES
-----
KMC.MissionControl/KMC.MissionControl.csproj
KMC.MissionControl/Pages/MapPage.cs
KMC.MissionControl/Transport/TransferPlannerManeuverUplink.cs
Tools/OrbitMap/tests/test_14_22_30_final_node_lock.py
README_14.22.30.txt

BUILD / DEPLOY
--------------
1. Extract this ZIP over the repository root.
2. Overwrite matching files.
3. Rebuild KMC.MissionControl.
4. No KSP plugin DLL replacement is required.

RUNTIME ACCEPTANCE
------------------
1. Kerbin parking orbit -> Duna.
2. Confirm CREATE KSP NODE appears for a valid ejection solution.
3. Click it once.
4. Confirm KSP creates exactly one node.
5. Confirm KMC progresses to NODE VERIFIED.
6. Confirm the button now reads NODE CREATED and cannot be clicked.
7. Leave the page running briefly; verify it does not re-enable merely because
   of tiny live timing drift.
8. Select another destination; confirm CREATE KSP NODE can become available for
   that destination's valid solution.
9. Return to LOCAL and confirm KSP authoritative patched conics remain visible.
10. Existing LOCAL map behavior remains unchanged.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
