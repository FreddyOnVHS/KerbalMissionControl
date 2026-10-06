KMC 14.22.46 Fix — Target μ Test/Graceful Fallback
====================================================

The first 14.22.46 automated run failed two target-SOI shooting tests.

ROOT CAUSE
----------
The existing synthetic navigation fixture intentionally left destination
GravParameter unset because earlier shooting only needed destination orbit and
SOI.

14.22.46 introduced target-relative hyperbola/periapsis calculation, which
requires target μ. The solver treated missing target μ as fatal once an
encounter was found.

FIX
---
1. Target periapsis shaping now runs only when live destination μ, radius,
   and desired periapsis are available.

2. If those data are unavailable, the target-SOI encounter solver still
   returns a valid encounter/miss solution instead of failing.

3. The dedicated safe-periapsis automated fixture now supplies an explicit
   destination μ so the new periapsis mathematics is actually tested.

LIVE KSP
--------
Real KSP body telemetry supplies destination μ, so live 14.22.46 behavior is
unchanged by this fix except for improved defensive handling of incomplete
telemetry.

EXPECTED TESTS
--------------
NavigationTests: 25 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

Do not push until automated tests and live Duna periapsis acceptance pass.
