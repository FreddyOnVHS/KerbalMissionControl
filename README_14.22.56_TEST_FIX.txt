KMC 14.22.56 Test Compatibility Fix
===================================

The 14.22.56 production solver replaced the old one-shot B-plane bootstrap
with RunBPlaneTerminalSearch().

Two older source-structure regression tests from 14.22.55/55b were still
looking for the removed one-shot implementation and therefore failed even
though the new 14.22.56 terminal-search regression passed.

This overlay updates those tests to verify the new architecture:

- B-plane terminal search exists before PASS 2 refinement.
- departure-DV trust-region protection remains.
- non-encounter trial geometry cannot be promoted.
- the pre-terminal encounter incumbent remains available as fallback.

Production navigation code is unchanged.

Expected:
NavigationTests: 35 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed
