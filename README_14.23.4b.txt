KMC 14.23.4b — Hull Visual Hierarchy

GOAL
Make the SCHEM drawing read as one spacecraft before the eye notices small
individual components.

CHANGES

PRIMARY HULL
The following categories now receive the strongest normal presentation:
- Command
- FuelTank
- Fairing
- Payload
- SolidBooster

They get:
- slightly stronger fill
- brighter outline
- thicker line

SECONDARY IMPORTANT PARTS
Other major subsystem parts remain clearly visible but no longer compete with
the hull for attention.

MINOR PARTS
Structural parts, reaction wheels, batteries, generators, antennas, RCS
thrusters, and unknown small components are dimmed further.

TINY DETAIL SUPPRESSION
Low-priority parts smaller than approximately 11x11 screen pixels are hidden.
Major/primary parts are never removed by this rule.

ENGINE
The 14.23.4a compact attached engine bell is unchanged.

LAYOUT
No centering, scale, transport, topology, or geometry changes.

KSP DLL
No KMC.Plugin changes.
You do NOT need to replace KMC.Plugin.dll.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_4b_hull_visual_hierarchy.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
If 14.23.4a was 57/0:
  58 passed, 0 failed

RUNTIME
Rebuild/run Mission Control only and compare SCHEM.

The main tank/capsule/fairing silhouette should stand out more clearly while
small circular and rectangular details recede.
