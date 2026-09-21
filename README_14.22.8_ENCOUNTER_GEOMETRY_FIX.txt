KMC 14.22.8 - Encounter Geometry / Hyperbolic Time Sampling Fix
===============================================================

Baseline:
14.22.8 KSP-style multi-body system view + hyperbolic protocol fix + invalid future patch guard + compile fix.

ROOT CAUSE
----------
The Mun encounter patch is commonly hyperbolic. The previous Mission Control sampler
ignored the KSP patch StartUT/EndUT for open conics and sampled nearly the entire
mathematical hyperbola by true anomaly. OrbitMapSystemTransform then translated those
points using uniformly inferred universal times. The point geometry and body-position
times therefore did not correspond, producing long nonsensical encounter lines.

FIX
---
- Adds gravity-aware universal-time propagation for elliptical and hyperbolic conics.
- Finite KSP patch StartUT/EndUT is now authoritative for bounded patch sampling.
- Child-body encounter patches and their child-body translation use the same time grid.
- Same-body projected patches use the primary body's transmitted gravitational parameter.
- Keeps the existing open-conic true-anomaly branch only as a fallback when KSP does not
  provide a usable finite time interval.

INSTALL
-------
Extract this ZIP over the KMC repository root and allow folders/files to merge/replace.

REBUILD (Release)
-----------------
1. KMC.MissionControl

No plugin or shared source files change in this patch.

RUNTIME TEST
------------
1. Keep the same Kerbin -> Mun maneuver encounter.
2. Verify RX continues climbing and MAP remains LIVE.
3. Compare the orange KMC projected trajectory with KSP Map View.
4. The Kerbin-side transfer should stop at the Mun SOI transition rather than continuing
   across the system as a giant hyperbola.
5. The Mun-local encounter arc should remain spatially associated with Mun.
6. Send matching KSP and KMC screenshots for final geometry validation.

VERIFICATION PERFORMED
----------------------
- TDD RED: 4/4 new hyperbolic patch timing tests failed before implementation.
- TDD GREEN: 4/4 new hyperbolic patch timing tests passed after implementation.
- Available 14.22.8 structural/regression tests: 22/22 passed.
- C# compilation must still be verified in Visual Studio.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
