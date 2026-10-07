KMC 14.22.68a - Execution Summary Regression Path Fix

Test-only overlay.
Fixes the transfer-execution source regression so MapPage.cs is found whether
NavigationTests is launched from the repository root or from the test binary directory.

No production KMC code changed.
Expected NavigationTests: 40 passed, 0 failed.
