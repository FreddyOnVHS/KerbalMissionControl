KMC 14.23.2 — Part Geometry / Orientation

BASE
Local 14.23.1 + 14.23.1a, built from frozen 14.22.76
GitHub baseline before local SCHEM work:
bdfe7a76d919d1bfcc698252d94b7b98deaf6f79

WHAT CHANGES

KSP PLUGIN
- Captures renderer bounds for each vessel part.
- Converts renderer bounds into an approximate Part-local bounding box.
- Captures the Part right/up/forward axes in the vessel ReferenceTransform.
- Geometry capture is fail-open: unusual modded renderer data cannot break
  normal topology telemetry.

SHARED TOPOLOGY
Transport version:
  v2 -> v3

New per-part data:
  GeometryAvailable
  BoundsCenter X/Y/Z
  BoundsSize X/Y/Z
  AxisRight X/Y/Z
  AxisUp X/Y/Z
  AxisForward X/Y/Z

Decoder remains compatible with old v1/v2 topology packets. Old packets simply
have GeometryAvailable=false and SCHEM falls back to the 14.23.1 point glyph.

MISSION CONTROL
- SCHEM now projects oriented 3D bounding boxes rather than only center points.
- Scaling uses the actual projected geometry bounds.
- Structural parent/child links remain faintly visible.
- FRONT / SIDE / TOP continue to work unchanged.
- Parts without usable renderer geometry retain the old category glyph.

WHY THIS BUILD
14.23.1 proved that vessel identity, topology, part positions and all three
views work. Runtime screenshots showed the expected limitation: center-point
topology produces a skeleton/starburst. 14.23.2 adds the missing size and
orientation needed to make the craft recognizable.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_2_part_geometry.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
If 14.23.1a was 51/0:
  52 passed, 0 failed

IMPORTANT — THIS BUILD DOES CHANGE KMC.Plugin.dll

Unlike 14.23.1, 14.23.2 changes:
  KMC.Plugin\Topology\VesselTopologyBuilder.cs
and shared topology transport fields.

Therefore you MUST rebuild KMC.Plugin and replace the DLL in KSP before the
new boxes/orientation can appear.

Typical installed DLL:
  D:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\
  GameData\KMC\Plugins\KMC.Plugin.dll

Use your normal plugin build/copy workflow. Based on the existing project,
the usual development output is:
  KMC.Plugin\bin\Debug\KMC.Plugin.dll

RUNTIME ACCEPTANCE
1. Quit KSP before replacing KMC.Plugin.dll.
2. Build KMC.Plugin.
3. Copy the new KMC.Plugin.dll into GameData\KMC\Plugins.
4. Start KSP and load Goliath II HLV.
5. Start Mission Control and open SCHEM.
6. Verify FRONT / SIDE / TOP show part-sized outlines rather than only a
   topology skeleton.
7. Compare broad shape, boosters/tanks/wings and vehicle proportions against
   the actual vessel/Vessel Viewer.

This still intentionally uses approximate renderer bounding boxes, not complete
Unity meshes. If the craft is now recognizable, the next step can improve the
visual style without needing to stream full meshes.
