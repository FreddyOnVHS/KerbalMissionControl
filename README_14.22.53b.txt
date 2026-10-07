KMC 14.22.53b - Finite-SOI Bootstrap Fallback Fix

14.22.53 made the coarse 10% finite-SOI feasibility gate mandatory.
That removed every parking-aware bootstrap in several existing synthetic tests.

Correction:
- track best feasible candidate separately;
- among feasible candidates, lowest departure DV wins;
- if none are feasible yet, keep the lowest-DV coarse bootstrap;
- finite-SOI error is only a tie-breaker for that bootstrap;
- later correction/shooting stages establish feasibility.

The 1.35x finite-SOI correction DV guard remains in place.

Expected:
NavigationTests: 31 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

If tests pass, test Dres before creating the node.
Do not push yet.
