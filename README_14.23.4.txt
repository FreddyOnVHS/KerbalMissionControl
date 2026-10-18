KMC 14.23.4 — Schematic Presentation Polish

BASE
Local 14.23.3 + 14.23.3a SCHEM work.

GOAL
Keep the clean single side view, but improve visual hierarchy so the graphic
reads more like one spacecraft and less like equally weighted individual parts.

CHANGES

1. ENGINE BELL
Liquid-engine parts no longer use their raw boxy convex hull for display.
They are drawn as a clean tapered bell/nozzle inside the engine's real
projected bounds.

The live vessel geometry still determines:
- engine position
- engine width
- engine height
- overall spacecraft scale

The renderer only stylizes the engine silhouette.

2. STRONGER MAJOR HULL
Major parts use:
- stronger outline
- slightly heavier line
- subtle fill

3. DIMMER MINOR PARTS
Low-priority details such as:
- structural pieces
- reaction wheels
- batteries
- generators
- antennas
- RCS thrusters
- unknown parts

are rendered more faintly.

4. TINY-DETAIL SUPPRESSION
A minor part smaller than about 7x7 screen pixels is omitted from the
schematic. Important/major parts are never removed by this rule.

5. LAYOUT
No position/centering changes.
The current vessel placement was confirmed visually and is preserved.

NO TRANSPORT CHANGE
No KSP plugin or topology packet changes in 14.23.4.

Therefore:
  KMC.Plugin.dll DOES NOT need to be rebuilt/replaced for this build.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_4_schematic_presentation_polish.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
If 14.23.3a was 55/0:
  56 passed, 0 failed

RUNTIME
Rebuild/run Mission Control and open SCHEM.

Look specifically at:
- bottom engine now reading as a tapered rocket nozzle
- major tanks/capsule remaining prominent
- small parts being less visually dominant
- overall vessel silhouette reading more cleanly
