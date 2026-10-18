KMC 14.23.2c — Native Local Mesh Bounds

PURPOSE
Fix the remaining oversized/nested SCHEM rectangles seen after 14.23.2b.

ROOT CAUSE
14.23.2/2b still used:

  renderer.bounds

Unity Renderer.bounds is a WORLD-SPACE AXIS-ALIGNED bounding box (AABB).
For rotated or scaled model transforms, Unity expands that box to remain
world-axis aligned. Transforming those already-expanded corners back into the
Part frame produces inflated part dimensions.

FIX
SCHEM now derives geometry from native LOCAL model bounds:

Ordinary mesh parts:
  MeshFilter -> sharedMesh -> mesh.bounds

Skinned mesh parts:
  SkinnedMeshRenderer -> localBounds

For each renderer:
  native local bounds corner
    -> renderer/model TransformPoint()
    -> world
    -> Part.transform.InverseTransformPoint()
    -> owning-Part local bounds

This preserves actual model orientation and scale without ever using the
world-space renderer.bounds AABB.

The 14.23.2b owner filter remains:
  renderer.GetComponentInParent<Part>() == part

So child-part meshes are still excluded.

UNITY COMPATIBILITY
The MeshFilter/sharedMesh and SkinnedMeshRenderer/localBounds lookups use
string/reflection where appropriate. This avoids adding more compile-time
dependencies on Unity split modules.

SCOPE
Changed:
  KMC.Plugin\Topology\VesselTopologyBuilder.cs
  Tools\NavigationTests\Program.cs

Not changed:
  topology packet format (still v3)
  Mission Control SCHEM renderer
  maneuver/guidance math
  electrical/propulsion logic
  control authority

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_2c_native_local_mesh_bounds.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.23.2 was 52/0:
  53 passed, 0 failed

RUNTIME
This changes KMC.Plugin, so:

1. Quit KSP.
2. Rebuild KMC.Plugin.
3. Replace:
     GameData\KMC\Plugins\KMC.Plugin.dll
4. Restart KSP.
5. Load the same vessel.
6. Open SCHEM and check FRONT / SIDE / TOP.

EXPECTED VISUAL CHANGE
- the very large inflated outer rectangles should shrink/disappear
- rotated parts should have more credible proportions
- nested rectangles should correspond more closely to actual individual Parts
- broad vehicle silhouette should become more recognizable

If some individual modded parts still produce poor boxes after this, the next
step would be part-specific geometry filtering or simplified mesh-edge
projection—not another global bounds transformation.
