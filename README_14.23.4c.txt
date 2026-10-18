KMC 14.23.4c — SCHEM Regression Expectation Fix

CAUSE
14.23.4b production behavior is correct, but two tests were stale/brittle:

1. The older presentation test still expected the original 7px tiny-detail
   suppression threshold. 14.23.4b intentionally raised it to 11px.

2. The new hull-hierarchy regression searched for a very specific whitespace
   layout around the ternary expression. The code compiled and ran correctly,
   but the source-text test was too formatting-sensitive.

FIX
Test-only:
- update expected suppression threshold from 7px to 11px
- make primary-hull fill/outline assertions semantic rather than whitespace
  dependent

No production code changes.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_4c_schem_regression_expectation_fix.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
  58 passed, 0 failed

No KSP DLL replacement is required.
