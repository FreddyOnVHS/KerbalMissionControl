KMC 14.22.36 — MAP Lambert Comparison Preview
==============================================

BASELINE
--------
Verified master HEAD before build:
8aabce949697b8e03ad7d77521f3598cb0e5d39a

PURPOSE
-------
Connect real OrbitMap celestial telemetry to the Lambert transfer search and
display a comparison on MAP / TRANSFER.

This is DISPLAY ONLY. CREATE KSP NODE remains on the existing Hohmann +
parking-ejection path.

DISPLAY
-------
For a supported same-parent destination:

LAMBERT PREVIEW  COARSE 9x9 / NO NODE AUTHORITY
PATH
DEPARTURE UT
FLIGHT TIME
DEP VINF
ARR VINF
SCORE
MU source

SEARCH
------
- 9 departure samples around the Hohmann window.
- 9 flight-time samples from 70% to 130% of Hohmann transfer time.
- short-way and long-way zero-revolution Lambert solutions.
- cached so the search is not repeated every draw frame.

PARENT MU
---------
The adapter first uses the common parent body's transmitted GravParameter when
that parent exists in OrbitMapPacket.Bodies.

If the central parent is omitted from the catalog (normal for the stock star
when the vessel is orbiting a planet), KMC derives parent mu from transmitted
orbital period and semi-major axis using Kepler's third law.

No stock body constants or body-specific names are used.

UNCHANGED AUTHORITY
-------------------
Hohmann window
 -> parking-orbit ejection
 -> CREATE KSP NODE
 -> KSP patched-conic verification
 -> optional REFINE KSP NODE

Lambert preview does not populate or modify ManeuverUplinkPacket.

KSP Plugin DLL replacement: NOT REQUIRED.
KMC.shared change: NONE.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Run from repository root:

  .\Tools\NavigationTests\Run-Tests.ps1

Expected:
- NavigationTests: 17 passed, 0 failed
- Transfer search: 3 passed, 0 failed

LIVE KSP ACCEPTANCE
-------------------
1. Use a stable near-circular, low-inclination Kerbin parking orbit.
2. Open MAP / TRANSFER and select Duna.
3. Confirm the legacy Hohmann section is unchanged.
4. Confirm the Lambert preview appears with:
   path, departure UT, flight time, DEP VINF, ARR VINF, score, MU source.
5. Take a screenshot or record those values.
6. Click CREATE KSP NODE and confirm the node is still the legacy Hohmann /
   parking-ejection node, not the Lambert preview.
7. Confirm KSP assessment and REFINE still behave as before.
8. Check carefully for text/button overlap before this build is frozen.

NEXT
----
Use the live comparison to decide whether the coarse search needs refinement.
Then convert Lambert departure excess velocity into a real local parking-orbit
ejection vector. Lambert should not become node authority until that conversion
is validated against KSP.
