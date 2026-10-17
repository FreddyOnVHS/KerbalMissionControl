KMC 14.22.69a - Transfer Maneuver Handoff Compile Fix

Compile-only correction on top of 14.22.69.

Adds:
    using KMC.Engine.Maneuver;

to MapPage.cs so the transfer handoff bridge can resolve:
- ManeuverPlanModel
- ManeuverPlanPromotionRequest
- ManeuverPlanPromotionStore

No handoff logic, solver math, UI behavior, packet protocol, or plugin code changed.

Expected NavigationTests after compile:
41 passed, 0 failed.
