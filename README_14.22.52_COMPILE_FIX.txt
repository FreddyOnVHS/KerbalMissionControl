KMC 14.22.52 Compile Fix
========================

Fixes a NavigationTests-only compile error.

The test attempted to assign engine-owned read-only properties on
TargetSoiShootingAssessment from the separate NavigationTests assembly.

Production solver code is unchanged.
No API visibility is weakened.
The regression now verifies the public DV trust-region behavior only;
periapsis feasibility remains exercised by the existing live shooting tests.

Expected full suite after applying over 14.22.52:
- NavigationTests: 31 passed, 0 failed
- Transfer search: 3 passed, 0 failed
- Lambert ejection: 5 passed, 0 failed
- Parking-aware search: 3 passed, 0 failed
- State propagation: 4 passed, 0 failed
