KMC 14.22.1 ORBIT MAP PATCH — SAFE APSIDES

Root cause confirmed from KSP.log:
  OrbitMapTelemetrySender.BuildPatches()
    -> BuildOrbit()
    -> Orbit.get_ApA()
    -> NullReferenceException

KSP exposes partially initialized future patched-conic Orbit objects.
Their basic orbital elements can be available while convenience getters
such as ApA/PeA/period still dereference incomplete internal state.

CHANGE:
  KMC.Plugin/OrbitMapTelemetrySender.cs
    - BuildOrbit no longer calls Orbit.ApA, Orbit.PeA, or Orbit.period.
    - Apoapsis/periapsis are derived from semi-major axis, eccentricity,
      and reference-body radius.
    - Period is left unavailable for this transport path rather than
      invoking the unsafe KSP getter.
    - BuildPatches continues to avoid getRelativePositionAtUT().

TEST:
  Tools/OrbitMap/tests/test_14_22_1_orbit_map_patch_safe_apsides.py

USER VERIFICATION:
  1. Replace the files in the repo.
  2. Rebuild KMC.Plugin in Release.
  3. Copy the new KMC.Plugin.dll into GameData\KMC\Plugins.
  4. Run KSP and leave MAP open >30 seconds.
  5. Confirm RX keeps increasing and LIVE does not fall to STALE/UNAVAILABLE.
  6. If it still fails, upload the fresh KSP.log.

KSP Plugin DLL Required? YES
