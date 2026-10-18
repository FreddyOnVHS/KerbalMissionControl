KMC 14.23.3a — Stale SCHEM Regression Fix

CAUSE
14.23.3 intentionally replaced two older SCHEM behaviors:

1. FRONT / SIDE / TOP selectors
   -> one fixed SIDE view

2. oriented bounding-box renderer
   -> mesh-derived side-outline polygon renderer

The two original regression tests still asserted those retired UI/rendering
details, so they failed even though the new 14.23.3 tests passed.

FIX
Test-only update:
- foundation regression now verifies the single SIDE presentation and the
  outline/fallback projection path.
- geometry regression still validates v3 geometry transport, but now verifies
  that Mission Control uses the current mesh-outline renderer rather than the
  retired oriented-box renderer.

No production source is changed.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_3a_stale_schem_regression_fix.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
  55 passed, 0 failed
