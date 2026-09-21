KMC 14.22.22 — Fixed Encounter-Body Frame Rendering

Root cause
----------
14.22.21 clearance diagnostics proved the encounter data itself is correct:
KSP and transmitted KMC miss distance agree. The visual error came from
translating each child-SOI sample by a different future Mun position while
displaying one fixed Mun ENC object.

Fix
---
For child-body encounter patches:
- compute the child's canonical position once at SOI entry / first sample UT,
- keep that position as encounterBodyAnchor,
- draw all local child-SOI samples around that fixed anchor,
- transmit that same anchor as ReferenceBodyPosition for every child sample.

This changes only encounter visualization placement. The local conic shape,
patch timing, SOI bounds, packet format, and Mission Control renderer remain
unchanged.

Diagnostics
-----------
The 14.22.21 clearance diagnostic remains enabled for this validation run.

Build
-----
Rebuild KMC.Plugin in Release.
Redeploy KMC.Plugin.dll to GameData/KMC/Plugins.

Acceptance test
---------------
Use the same Mun flyby that KSP shows as a miss.
- KMC should also visually show a miss.
- The local encounter arc should be centered around Mun ENC.
- RX should continue increasing.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
