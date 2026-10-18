KMC 14.23.3 — Clean Single-View Schematic

DIRECTION
This build changes SCHEM from an engineering topology visualization into a
clean vessel graphic closer to the Vessel Viewer presentation.

The user selected one consistent SIDE view rather than FRONT/SIDE/TOP.

WHAT CHANGES

KSP PLUGIN
- Reads actual mesh vertices for renderer geometry owned by each Part.
- Transforms sampled vertices into the vessel ReferenceTransform.
- Projects each Part into the SIDE plane:
    horizontal = vessel Z
    vertical   = vessel Y
- Computes a convex hull per Part.
- Reduces each hull to at most 24 points.
- Stores/transmits only the simplified 2D outline, not full meshes.

This keeps packet size reasonable while producing much cleaner shapes than
bounding boxes.

SHARED TOPOLOGY
Transport version:
  v3 -> v4

New:
  VesselOutlinePoint
  VesselTopologyNode.SideOutlinePoints

Old v1-v3 packets remain readable. If no outline is present, Mission Control
uses a tiny fallback glyph.

MISSION CONTROL
- SCHEM is now ONE fixed SIDE view.
- FRONT/SIDE/TOP buttons are removed.
- Parent/child structural-link lines are removed.
- Each Part is drawn as a projected mesh hull polygon.
- Major parts receive stronger outlines and faint fill.
- Minor parts are drawn more dimly.
- Craft is automatically fit and centered.
- Status shows outline/fallback part counts.

WHY CONVEX HULLS
Full Unity mesh streaming would be much larger and noisier than needed for a
mission-control schematic. A 2D hull captures the recognizable outer shape of
each part while keeping the display clean and the transport lightweight.

This is intentionally closer to Vessel Viewer's visual goal than the previous
box renderer.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_3_clean_single_view_schematic.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.23.2c was 53/0:
  55 passed, 0 failed

IMPORTANT
This build changes:
  KMC.Plugin
  KMC.shared
  KMC.MissionControl

Therefore rebuild the solution/plugin and replace KMC.Plugin.dll in KSP.

Typical DLL:
  KMC.Plugin\bin\Debug\KMC.Plugin.dll

Installed location:
  D:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\
  GameData\KMC\Plugins\KMC.Plugin.dll

RUNTIME ACCEPTANCE
1. Quit KSP.
2. Apply patch.
3. Run NavigationTests.
4. Rebuild KMC.Plugin.
5. Replace installed KMC.Plugin.dll.
6. Start KSP and load the same vessel.
7. Open SCHEM.

Expected:
- no FRONT/SIDE/TOP controls
- no starburst structural lines
- one centered side view
- individual parts read as hull-like shapes instead of rectangles
- broad craft silhouette should be much closer to Vessel Viewer

NEXT
If the hull approach is clean enough, subsequent builds can add:
- green system-state highlighting
- failed/dead part coloring
- selected subsystem overlays
without changing the underlying geometry transport again.
