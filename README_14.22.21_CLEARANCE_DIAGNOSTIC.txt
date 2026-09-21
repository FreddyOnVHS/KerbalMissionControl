KMC 14.22.21 — Encounter Clearance Diagnostic

This build is diagnostic-only. It does NOT change projected orbit geometry
or Mission Control rendering.

Purpose
-------
Determine whether KMC's transmitted encounter geometry really predicts a
collision with Mun, or whether the trajectory is correct and the Mun ENC
visualization is simply drawn at the wrong scale/position.

Log prefix
----------
[KMC-ORBIT-CLEARANCE]

Logged values
-------------
bodyRadius
expectedPeRadius
expectedPeClearance
minKspLocalRadius
minKspUT
minTxRadius
closestUT
clearanceAboveSurface
deltaKspVsTx

Interpretation
--------------
- If minKspLocalRadius and minTxRadius closely agree and clearanceAboveSurface
  is positive, the geometry does NOT predict an impact. A visual/rendering
  problem is making it look like one.
- If minTxRadius is much smaller than minKspLocalRadius, the transmitted
  geometry is wrong.
- If both are below bodyRadius, KSP itself is predicting an impact/collision
  for that patched conic.

Test procedure
--------------
1. Rebuild KMC.Plugin Release.
2. Replace GameData/KMC/Plugins/KMC.Plugin.dll.
3. Start KSP fresh.
4. Create one Mun encounter that appears to hit Mun in KMC but misses in KSP.
5. Let it sit for 2-3 seconds.
6. Exit KSP.
7. Send KSP.log.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
