KMC 14.22.8 - Invalid Future Patch Guard

Problem
-------
When KSP creates a Mun encounter, its patched-conic chain can include a future
patch whose orbital elements are not all finite yet. OrbitMapPacket.Serialize()
requires the reconstructable orbit fields to be finite. One bad future patch
therefore poisoned the entire telemetry packet, stopped RX, and made the MAP
go STALE/UNAVAILABLE.

Fix
---
OrbitMapTelemetrySender now validates each future patch orbit before adding it
to the packet. If KSP exposes a non-finite future patch, KMC logs a warning and
stops that future chain at the last valid patch instead of dropping the whole
telemetry packet.

This preserves all valid earlier trajectory/encounter geometry and keeps the
orbit-map telemetry alive.

Also includes the prior 14.22.8 hyperbolic apoapsis protocol correction:
ApoapsisMeters is optional for hyperbolic patches.

Build
-----
Rebuild KMC.shared, KMC.Plugin, and KMC.MissionControl in Release.
Deploy KMC.Plugin.dll and KMC.shared.dll to GameData/KMC/Plugins.

Runtime acceptance
------------------
1. Start in Kerbin orbit and verify RX climbs.
2. Create a maneuver producing a Mun encounter.
3. Verify RX continues climbing and the MAP remains LIVE.
4. Compare KMC with KSP Map View for the Kerbin transfer and Mun encounter.
5. If the log contains "Orbit map skipped invalid future patch", send that
   warning plus screenshots; it identifies KSP's incomplete tail patch without
   sacrificing the valid trajectory.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? YES
