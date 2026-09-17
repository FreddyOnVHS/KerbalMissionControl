KMC 14.22.4 — MANEUVER NODE TRUE-ANOMALY COMPILE FIX

ROOT CAUSE
----------
KSP 1.12.5's Orbit class does not expose getTrueAnomalyAtUT(), so the first
14.22.4 package could not compile (CS1061).

FIX
---
KMC.Plugin/OrbitMapTelemetrySender.cs
  - Removes the unavailable getTrueAnomalyAtUT() call.
  - Adds a local true-anomaly-at-UT solver using the orbital elements KSP
    already exposes: meanAnomalyAtEpoch, epoch, eccentricity, semiMajorAxis,
    and referenceBody.gravParameter.
  - Supports both elliptic and hyperbolic conics.
  - Maneuver node position still feeds CanonicalPositionAtTrueAnomaly(), so
    target/node markers remain in the same KMC coordinate frame as VSL/orbits.

TEST
----
Tools/OrbitMap/tests/test_14_22_4_node_true_anomaly_compile_fix.py

VERIFICATION IN THIS ENVIRONMENT
--------------------------------
TDD:
  RED: test failed against the unavailable API call.
  GREEN: test passed after replacing it with the local solver.
  Targeted 14.22.4 tests: 6/6 PASS.
  Full available OrbitMap Python regression suite: 31/31 PASS.

YOUR NEXT STEP
--------------
1. Overlay this ZIP on the current 14.22.4 source.
2. Rebuild KMC.Plugin in Visual Studio.
3. If build succeeds, replace GameData\KMC\Plugins\KMC.Plugin.dll.
4. Runtime-test target + maneuver markers against KSP Map View.

KSP Plugin DLL Required? YES
