KMC 14.22.8 — KSP-STYLE MULTI-BODY SYSTEM VIEW

BASELINE
--------
GitHub master: 541075cbc02e8375a5a556380de2d2d8c71929f8 (KMC 14.22.7)

PURPOSE
-------
Extend MAP from one body-centered local view to a KSP-style system view.
The current primary remains centered while its immediate child bodies, their
orbits, the vessel transfer path, and child-SOI encounter patches can appear in
one coherent parent-body frame.

MODIFY
------
KMC.shared/OrbitMapPacket.cs
  - Adds bounded Bodies telemetry (MaxBodies=16).
  - Adds OrbitMapBody with parent, radius/SOI, grav parameter, orbit, and
    authoritative parent-frame position.
  - Rejects duplicate/malformed body entries.

KMC.Plugin/OrbitMapTelemetrySender.cs
  - Publishes current primary plus immediate child bodies only.
  - Child positions use the same canonical conic frame as KMC orbit geometry.

KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs
  - Caches child-body orbit geometry.
  - Preserves primary-centered patches.
  - Translates recognized child-referenced encounter patches into the primary
    system view instead of dropping them.

KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
  - Draws child-body orbit paths and simple labeled child bodies.
  - Encounter panel can show child-body periapsis when available.

KMC.MissionControl/KMC.MissionControl.csproj
  - Includes OrbitMapSystemTransform.cs.

ADD
---
KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs
Tools/OrbitMap/tests/test_14_22_8_*.py
README_14.22.8.txt

PERFORMANCE
-----------
- Only the current primary plus immediate children are transmitted/rendered.
- Child closed orbits use 160 samples.
- Projected patches remain capped at 192 samples.
- Camera-only motion continues to reuse cached geometry.
- MAP inactive-page throttling is unchanged.

BUILD
-----
Build Release:
  KMC.shared
  KMC.Plugin
  KMC.MissionControl

Because the shared packet contract changes, deploy the rebuilt KMC.shared.dll
where your normal KMC/KSP installation expects it together with the rebuilt
KMC.Plugin.dll.

RUNTIME ACCEPTANCE
------------------
1. Start in Kerbin orbit.
2. Target Mun and create a Mun encounter.
3. KMC MAP should keep Kerbin centered.
4. Mun and Minmus orbit paths should be visible when zoomed out sufficiently.
5. Mun/Minmus body markers should sit on their orbit paths.
6. The Kerbin-side transfer should remain visible.
7. The Mun-referenced encounter patch should appear around Mun in the same
   overall system view when KSP supplies that patch.
8. ENCOUNTER should identify Mun and show PE when available.
9. RX should continue increasing.
10. GEOMETRY REBUILD should remain stable while the maneuver is unchanged.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? YES
