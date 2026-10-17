KMC 14.22.69b - Transfer Handoff Ownership Boundary Fix

Fixes the 14.22.69a compile failure without making Engine-owned maneuver
properties publicly writable.

Architecture:
- MAP constructs the existing public ManeuverPlanPromotionRequest DTO.
- KMC.Engine owns conversion of that DTO into ManeuverPlanModel through
  ManeuverPlanModel.CreateFromPromotionRequest().
- Mission Control retains the Engine-created model.
- The same promotion request is published through ManeuverPlanPromotionStore.
- GUID continues to use the established PlanId/node verification path.

No KSP plugin change.

Expected NavigationTests:
41 passed, 0 failed.
