KMC 14.22.7 — MAP CONTROL STRIP CLEANUP

PURPOSE
-------
Make the MAP controls visually distinct and guarantee that each label fits
inside its own button without text or border overlap.

CONTROL STRIP
-------------
[ RESET VIEW ]    [ FIT ORBIT ]    [ FIT MANEUVER ]    [ TARGET ]

MODIFY
------
KMC.MissionControl/Pages/MapPage.cs
  - Measures each label using the actual Mission Control font.
  - Gives each label 14 px horizontal padding on both sides.
  - Uses a normal 16 px gap between buttons.
  - Centers label text horizontally and vertically.
  - Includes a viewport-width guard that can reduce spacing safely before any
    control can overlap or run outside the map viewport.

UNCHANGED
---------
- RESET VIEW remains AZ 360 / EL 00.
- 14.22.6 projected maneuver trajectory behavior is unchanged.
- KSP plugin/telemetry code is unchanged.

ADD
---
Tools/OrbitMap/tests/test_14_22_7_map_control_strip.py

VERIFICATION IN THIS ENVIRONMENT
--------------------------------
TDD:
  RED: new control-strip test failed before implementation.
  GREEN: new control-strip test passed after implementation.
  Full available OrbitMap Python regression suite: 44/44 PASS.

YOUR TEST
---------
1. Drag/drop this package onto the current repo root.
2. Build KMC.MissionControl in Release.
3. Open MAP.
4. Confirm the controls read:
     [ RESET VIEW ]    [ FIT ORBIT ]    [ FIT MANEUVER ]    [ TARGET ]
5. Confirm every label is centered with visible padding.
6. Confirm no button rectangles or text overlap.
7. Confirm RESET VIEW still returns to AZ 360 / EL 00.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
