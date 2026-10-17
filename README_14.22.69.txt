KMC 14.22.69 - Transfer Maneuver Handoff

Purpose:
Close the MAP -> MNV -> GUID ownership gap for interplanetary transfer nodes.

Root cause:
MAP created a valid KSP node and tracked its PlanId in the transfer status path,
but it never registered that maneuver with KMC's retained maneuver-plan store or
Engine maneuver promotion path. MNV therefore classified it as MANUAL and GUID
continued to own the previous maneuver plan.

Fix:
- On successful MAP transfer-node uplink, synthesize a ManeuverPlanModel using
  the exact MAP PlanId, vessel, node UT, and P/N/R vector.
- Capture/select it through the existing KmcManeuverPlanStore.
- Promote it through the existing ManeuverPlanPromotionStore.
- Republish the existing node-verification state to Guidance.
- No new guidance authority and no special transfer-only execution path.

Expected runtime flow:
MAP: NODE CREATED / READY FOR BURN
MNV: transfer node classified as KMC plan, not MANUAL
GUID: active transfer PlanId, live node recognized, maneuver vector displayed

No KSP plugin changes.
Expected NavigationTests: 41 passed, 0 failed.
