KMC 14.22.48 — CUMULATIVE General 3D Hyperbolic Departure
=================================================================

IMPORTANT
---------
This package replaces the earlier 14.22.48 ZIP.

The first 14.22.48 package accidentally included older project/test files,
which caused the local automated suite to regress from:

  NavigationTests 27 -> 22
  Lambert ejection 5 expected -> 4

That was a packaging error, not a navigation-math test failure.

THIS ZIP IS CUMULATIVE
----------------------
It contains the complete local 14.22.47 navigation state plus the 14.22.48
general 3D hyperbolic-departure implementation.

Expected automated results:
  NavigationTests: 27 passed, 0 failed
  Transfer search: 3 passed, 0 failed
  Lambert ejection: 5 passed, 0 failed
  Parking-aware search: 3 passed, 0 failed
  State propagation: 4 passed, 0 failed

The NavigationTests output must include:
  PASS 3D direct shooting bootstrap survives parking-aware rejection
  PASS same-parent planetary geometry regression matrix

The LambertEjectionTests output must include:
  PASS Lambert ejection supports a normal-only 3D asymptote

If the suite instead reports 22 NavigationTests or only 4 Lambert ejection
tests, an old file is still present and the overlay was not fully applied.

After automated PASS, test Dres before pushing.
