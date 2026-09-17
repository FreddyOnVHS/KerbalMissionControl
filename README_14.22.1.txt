KMC 14.22.1 - TRUE 3D ORBIT MAP
================================

BASE
----
Frozen KMC 14.21.10
SHA: 34ecdfdcfb44aef63b3444ee98b47ead5c8f3568

FEATURE
-------
Adds an enabled Mission Control MAP page with a lightweight true-3D orbital display.
KSP remains authoritative for active-vessel orbit, selected target, maneuver nodes,
patched-conic state, and encounter transitions.

TELEMETRY
---------
Protocol: KMC-ORBITMAP1
UDP port: 5110
Nominal rate: ~5 Hz

PERFORMANCE
-----------
Orbit and patch geometry is cached. Camera rotation, zoom, ordinary repaint, packet
sequence changes, and timestamp-only updates do not intentionally regenerate conics.
The MAP page requests orbit-map redraws only while MAP is active. Diagnostic text on
the page exposes received snapshot count and GEOMETRY REBUILD count.

ADD
---
KMC.shared/OrbitMapPacket.cs
KMC.Plugin/OrbitMapTelemetrySender.cs
KMC.MissionControl/Telemetry/OrbitMapTelemetryReceiver.cs
KMC.MissionControl/Telemetry/OrbitMapSnapshotStore.cs
KMC.MissionControl/Rendering/OrbitMap/OrbitMapVector3.cs
KMC.MissionControl/Rendering/OrbitMap/OrbitMapConicSampler.cs
KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs
KMC.MissionControl/Rendering/OrbitMap/OrbitMapCamera.cs
KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
KMC.MissionControl/Pages/IMissionPagePointerInput.cs
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/*
Tools/OrbitMap/apply_14_22_1.py

MODIFY
------
KMC.shared/KMC.shared.csproj
KMC.Plugin/KMC.Plugin.csproj
KMC.MissionControl/KMC.MissionControl.csproj
KMC.MissionControl/Controls/MissionDisplay.cs
KMC.MissionControl/MainForm.cs

REMOVE
------
None.

TESTS
-----
python -m pytest -q Tools/OrbitMap/tests
python -m pytest -q Tools/ElectricalExpansion/tests
git diff --check

Visual Studio build required on the KSP development machine:
- KMC.Plugin, Release
- KMC.MissionControl, Release

RUNTIME ACCEPTANCE
------------------
1. Open a vessel in stable orbit and compare KMC MAP against KSP MAP VIEW.
2. Verify orbit orientation/shape plus AP and PE.
3. Add a maneuver node and verify node marker, DV values, and projected patch.
4. Select a vessel or body target and verify target data/orbit where applicable.
5. Create an SOI encounter and verify transition/encounter labeling.
6. Rotate with left-drag and zoom with the mouse wheel.
7. Confirm camera movement does not increase GEOMETRY REBUILD count.
8. Leave MAP open for 30 seconds; routine telemetry must not rebuild geometry continuously.
9. Switch away from MAP; redraw activity should stop materially while RX may continue.
10. Stop orbit telemetry and verify LIVE -> STALE -> UNAVAILABLE presentation.
11. Restore telemetry and verify MAP recovers without restarting Mission Control.
12. Reject the build if KSP or Mission Control shows noticeable stutter.

KSP Plugin DLL Required? YES
