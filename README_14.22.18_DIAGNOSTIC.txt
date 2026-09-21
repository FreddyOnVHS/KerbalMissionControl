KMC 14.22.18 — KSP-vs-KMC Frame Comparison Probe

This is diagnostic-only. It does not alter rendered geometry.

What the first probe proved
---------------------------
- Mun encounter patch entry/exit are on the KSP SOI boundary.
- KSP fixed parent-frame points equal the transmitted packet points.
- The remaining problem is a frame mismatch between KSP-derived samples and
  KMC's canonical reconstructed scene.

What this probe adds
--------------------
At the exact same UT, it logs:
- KSP fixed local / body / parent position
- KMC canonical local / body / parent position
- angular separation between each pair

Fields:
  canonicalLocal
  canonicalBody
  canonicalParent
  angleLocalDeg
  angleBodyDeg
  angleParentDeg

How to test
-----------
1. Rebuild KMC.Plugin Release.
2. Replace GameData/KMC/Plugins/KMC.Plugin.dll.
3. Start KSP fresh.
4. Create one Mun encounter that renders incorrectly.
5. Leave it for 2-3 seconds.
6. Exit KSP.
7. Send KSP.log.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
