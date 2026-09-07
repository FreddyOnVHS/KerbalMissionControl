KMC 14.21.8 — POWER BREAKER STATUS PAGE
=======================================

BASE
----
Frozen KMC 14.21.7
SHA: 38fe2ec6af11bb3ffe9fa4100e5bcab4fd62929a

PURPOSE
-------
Add a read-only POWER breaker-status subpage tied directly to KMC's
authoritative synthetic electrical distribution.

POWER SUBPAGE ORDER
-------------------
1/3 ONE-LINE
2/3 BREAKERS
3/3 DETAIL

NAVIGATION LAYOUT
-----------------
POWER reserves a dedicated right-side navigation rail so the three page
buttons do not overlap ONE-LINE, BREAKERS, DETAIL, or the distribution-event
header.

Nav rail width: 220
Nav/content gap: 18
Tab width: 180

BREAKER LAYOUT
--------------
LEFT:   MAIN BUS A
MIDDLE: ESSENTIAL BUS
RIGHT:  MAIN BUS B

The top of each bus shows live source/feed switch status:

MAIN A:
GEN A / BAT A

ESS:
ESS A / ESS B

MAIN B:
GEN B / BAT B

Each source/feed item shows:
CMD / IND

XFER switches are intentionally not shown.

Each branch breaker displays:
CMD / IND / STATE / LOAD

CMD:
breaker.CommandedClosed -> CLOSED / OPEN

IND:
breaker.IndicatedClosed -> CLOSED / OPEN

STATE:
breaker.Conducting -> POWERED / UNPOWERED
missing breaker/load evidence -> UNKNOWN

LOAD:
conducting -> modeled load.DemandAmps, shown as x.xA
not conducting -> 0.0A
missing evidence -> --

LOAD is KMC's modeled synthetic branch demand, not a physical ammeter
measurement.

20 LIVE BREAKERS
----------------
MAIN A (5)
BRK_GUID_A
BRK_COMM_A
BRK_PUMP_A
BRK_CABIN_FAN_A
BRK_THERMAL_HEATER_A

ESS (10)
BRK_FLIGHT_COMPUTER
BRK_INSTRUMENTATION_ESS
BRK_FLIGHT_CONTROL
BRK_REACTION_WHEEL
BRK_ENGINE_CONTROL
BRK_STAGING_CONTROL
BRK_BRAKE_CONTROL
BRK_GEAR_CONTROL
BRK_LIGHTING_ESS
BRK_RCS_CONTROL

MAIN B (5)
BRK_GUID_B
BRK_COMM_B
BRK_PUMP_B
BRK_CABIN_FAN_B
BRK_THERMAL_HEATER_B

READ-ONLY SCOPE
---------------
14.21.8 does NOT add player breaker operation.
It does NOT add a second breaker-state store.
It does NOT alter F10 failure injection.
It does NOT change KMC.Plugin.

F10 breaker trips and upstream bus-loss conditions are reflected because the
renderer reads the existing SyntheticElectricalDistributionModel.

ADD
---
KMC.MissionControl/Rendering/Power/PowerBreakerPanelRenderer.cs
Tools/ElectricalExpansion/apply_14_21_8.py
Tools/ElectricalExpansion/tests/test_14_21_8_power_breaker_page.py
README_14.21.8.txt

REPLACE / MODIFY
----------------
KMC.MissionControl/Pages/PowerPage.cs
KMC.MissionControl/KMC.MissionControl.csproj

REMOVE
------
Nothing from the frozen 14.21.7 base.

APPLY
-----
From repository root:

python Tools/ElectricalExpansion/apply_14_21_8.py

The canonical apply script writes the exact final 14.21.8 PowerPage and
PowerBreakerPanelRenderer payloads and adds the renderer to the project file.

FOCUSED TEST
------------
python -m pytest -q Tools/ElectricalExpansion/tests/test_14_21_8_power_breaker_page.py

REGRESSION TEST
---------------
python -m pytest -q Tools/ElectricalExpansion/tests

BUILD / RUNTIME
---------------
Build KMC.MissionControl in Visual Studio.

Verify:
1. Tabs read 1/3 ONE-LINE, 2/3 BREAKERS, 3/3 DETAIL.
2. Navigation rail is fully visible and does not overlap page content.
3. BREAKERS shows MAIN A left, ESS middle, MAIN B right.
4. GEN/BAT/ESS feed CMD/IND strips are live and visually separated.
5. All 20 breaker rows are present.
6. Normal branch: CLOSED / CLOSED / POWERED / x.xA.
7. F10 trip: CMD remains CLOSED, IND becomes OPEN, STATE UNPOWERED, LOAD 0.0A.
8. Dead upstream bus with healthy closed breaker: CLOSED / CLOSED /
   UNPOWERED / 0.0A.
9. Existing ONE-LINE page still renders.
10. Existing DETAIL page and source paging still work.

DO NOT PUSH / FREEZE
--------------------
Do not push until focused tests, ElectricalExpansion regressions, Visual Studio
build, runtime validation, and final staged-diff review all pass.

KSP Plugin DLL Required? NO


CANONICAL CLEANUP V2 NOTE
-------------------------
When cleaning an already-patched 14.21.8 working tree, replace the production
renderer with the canonical final file included in this package:

KMC.MissionControl/Rendering/Power/PowerBreakerPanelRenderer.cs

This is the final runtime-tested feed-strip/breaker renderer and must match
the payload embedded in apply_14_21_8.py.
