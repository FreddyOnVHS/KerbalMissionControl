KMC 14.23.2b - Part-Owned Renderer Bounds

CAUSE
14.23.2 used:
  part.GetComponentsInChildren<Renderer>(true)

That can include renderers from attached child Parts. A parent Part could
therefore inherit geometry from a large branch of the vessel, producing the
large nested rectangles seen in FRONT/SIDE/TOP.

FIX
For each Renderer, determine the nearest Part ancestor:
  renderer.GetComponentInParent<Part>()

Only include the Renderer when:
  rendererOwner == part

This keeps renderer geometry that belongs to the current Part while excluding
geometry owned by attached child Parts.

SCOPE
- KMC.Plugin only
- no shared transport changes
- no Mission Control renderer changes
- no maneuver/guidance/system changes

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_2b_part_owned_renderer_bounds.ps1

THEN
1. Rebuild KMC.Plugin.
2. Quit KSP.
3. Replace:
   GameData\KMC\Plugins\KMC.Plugin.dll
4. Restart KSP and test SCHEM FRONT/SIDE/TOP again.

Expected visual result:
- huge nested rectangles should disappear
- each box should more closely approximate one KSP Part
- overall craft outline should become much more recognizable
