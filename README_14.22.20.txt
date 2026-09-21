KMC 14.22.20 — Unified Canonical Projected Geometry

Root cause
----------
14.22.19 diagnostics proved the Mun-side child patch could be aligned into
KMC's canonical frame, while the Kerbin-side projected maneuver patch was still
being transmitted in KSP's fixed/right-handed frame. The projected trajectory
was therefore split across two coordinate conventions.

Fix
---
Every projected patch now uses one coordinate pipeline:
- KSP remains authoritative for patch chain, StartUT/EndUT and phase at UT.
- KSP phase is obtained with TrueAnomalyAtT(getObtAtUT(ut)).
- KMC converts that phase through CanonicalPositionAtTrueAnomaly().
- Primary-body patches transmit canonicalLocal directly.
- Child-body patches add the child body's canonical position at the same UT.
- All projected points therefore arrive in Mission Control in the same
  canonical frame as the current orbit, Mun/Minmus orbits, VSL, AP and PE.

Removed
-------
- 14.22.19 signed frame-rotation workaround.

Unchanged
---------
- packet format
- Mission Control renderer
- camera
- SOI timing
- patch chain selection
- encounter labels

Build
-----
Rebuild KMC.Plugin in Release.
Redeploy KMC.Plugin.dll to GameData/KMC/Plugins.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
