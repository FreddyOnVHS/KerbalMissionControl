KMC 14.22.75b2 - Manual Node Regression Anchor Fix

This is TEST-ONLY.

Root cause:
The 14.22.75b1 regression searched for the first occurrence of:
  CalculateManualProgradeRetrograde

That matched the planner CALL SITE near the top of ApsisManeuverPlanner.cs,
not the actual method definition. The end marker similarly matched an earlier
ValidateTargetPeriapsis reference.

The regression therefore inspected the wrong source range and falsely failed:
  all three manual maneuver axes must preserve valid node without burn estimate

Fix:
Anchor on exact method definitions:
  private ManeuverPlanModel CalculateManualProgradeRetrograde(
  private static bool ValidateTargetPeriapsis(

No production code changes.
No maneuver behavior changes.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_75b2_manual_node_regression_fix.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected:
  49 passed, 0 failed
