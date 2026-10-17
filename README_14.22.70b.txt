KMC 14.22.70b - KSP-Authoritative Maneuver Vector Telemetry

WHY
14.22.70 reconstructed the node direction from the vessel's CURRENT prograde /
normal / radial basis. KSP maneuver P/N/R values are defined at the maneuver
epoch, so a far-future node can disagree severely with that live basis.

AUTHORITATIVE FIX
KMC.Plugin now reads:
    ManeuverNode.GetBurnVector(vessel.orbit)
for the first live KSP maneuver node, normalizes the world-space burn vector,
and resolves it into vessel ReferenceTransform Right/Nose/Forward.

Protocol:
    KMC-MNVV1 on UDP 5102

Mission Control associates the sample by:
- active vessel id
- node UT within 0.25 s
- telemetry freshness <= 0.75 s

GUID uses the KSP-authoritative vector first. The 14.22.70 reconstructed
mixed-P/N/R path remains as a fallback only.

APPLY
From repo root:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_70b_ksp_authoritative_maneuver_vector.ps1

Then build/test:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

RUNTIME
This build CHANGES KMC.Plugin.
Rebuild KMC.Plugin.dll and copy the new DLL into the KSP GameData KMC plugin
location before runtime testing.

Expected runtime when vessel is pointed at the KSP maneuver marker:
- FDAI green cue at/near center
- ALIGN ERROR near 0 deg
- LAT and VERT near 0 deg
- Attitude reference: KSP-AUTHORITATIVE MANEUVER VECTOR

No transfer solver math changed.
