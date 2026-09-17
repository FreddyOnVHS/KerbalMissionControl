KMC 14.22.1 ORBIT MAP PATCH PROPAGATION CORRECTIVE

ROOT CAUSE
KSP.log shows OrbitMapTelemetrySender.BuildPatches calling
Orbit.getRelativePositionAtUT(patch.StartUT). For partially initialized
landed/future patched-conic states, KSP throws NullReferenceException from
Orbit.get_semiLatusRectum(). The sender then emits no packet, so Mission
Control correctly transitions LIVE -> STALE -> UNAVAILABLE.

MODIFY
- KMC.Plugin/OrbitMapTelemetrySender.cs
- Tools/OrbitMap/tests/test_14_22_1_orbit_map_protocol.py

FIX
Future patch display geometry is reconstructed in Mission Control from the
patch orbital elements. The plugin no longer asks KSP to propagate each
patch at StartUT solely to fill unused patch-position fields. It supplies a
finite zero vector for those unused fields instead, leaving active-vessel,
target, and maneuver-node positions unchanged.

VERIFICATION IN CHAT ENVIRONMENT
- Focused orbit-map protocol tests: 4/4 PASS
- Full OrbitMap tests: 16/16 PASS
- ElectricalExpansion regression: 81/81 PASS
- git diff --check: PASS

WINDOWS / VISUAL STUDIO
Rebuild KMC.Plugin in Release and copy the new KMC.Plugin.dll to:
GameData/KMC/Plugins/KMC.Plugin.dll

RUNTIME RETEST
1. Load the same vessel/state that previously failed.
2. Open KMC MAP and leave it open at least 30 seconds.
3. RX should continue increasing at about 5 Hz.
4. ORBIT DATA STALE / UNAVAILABLE must not appear while KSP remains active.
5. Check KSP.log for absence of repeated "Orbit map telemetry send failed"
   exceptions from BuildPatches.

KSP Plugin DLL Required? YES
