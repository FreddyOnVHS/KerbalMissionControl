KMC 14.22.14 — Authoritative Encounter Canonical-Frame Fix

Purpose
-------
Correct 14.22.13 authoritative KSP patch samples that were being transmitted
in KSP's native/world coordinate convention while Mission Control renders the
scene in KMC's canonical orbital frame.

Change
------
OrbitMapTelemetrySender now:
- samples KSP authoritative position and velocity at each UT,
- derives true anomaly from radius plus radial-velocity direction,
- reconstructs that exact authoritative phase with KMC's proven canonical
  orbital transform,
- applies the same conversion to the future encounter body's center.

This keeps KSP authoritative for timing/branch while keeping every rendered
object in one coordinate convention.

Build
-----
Rebuild KMC.Plugin in Release.
Redeploy KMC.Plugin.dll to GameData/KMC/Plugins.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
