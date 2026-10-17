KMC 14.22.70a - Mixed Maneuver Burn Vector Latch

Fixes the compile error from the second PopulateAttitudeGuidance call site and,
more importantly, preserves the exact mixed P/N/R maneuver vector after burn
execution begins.

Apply after 14.22.70 has already been applied:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_70a_mixed_vector_burn_latch.ps1

Then run:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected NavigationTests:
42 passed, 0 failed

Architecture:
- BeginBurn clones the active ManeuverPlanModel inside KMC.Engine.
- BuildBurnExecutionSolution uses that latched plan for attitude guidance.
- The FDAI vector therefore does not revert to prograde when the burn starts.
- Burn-completion reset clears the latched plan.

No KSP plugin changes.
