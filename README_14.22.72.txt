KMC 14.22.72 - GUID Consumes OrbitMap Authoritative Maneuver Vector

BASE
Apply on top of the current 14.22.71b tree.

PURPOSE
Complete the KSP -> OrbitMap -> GUID authoritative maneuver-vector path.

CHANGES
1. ManeuverVectorTelemetryStore now retains multiple samples keyed by:
   - VesselId
   - NodeUniversalTimeSeconds

2. OrbitMapTelemetryReceiver publishes every maneuver node carrying:
   - AuthoritativeBurnVectorAvailable
   - AuthoritativeBurnRight
   - AuthoritativeBurnNose
   - AuthoritativeBurnReferenceForward

3. GuidanceSystem requests the exact authoritative vector for the active plan
   by vessel ID + node UT rather than using an arbitrary latest sample.

4. Adds a regression proving multiple queued nodes do not cross-match.

RUNTIME EXPECTATION
After rebuilding and launching with the 14.22.71 plugin:

GUID should show:
    ATTITUDE KSP-AUTHORITATIVE MANEUVER VECTOR

When KSP is pointed at its maneuver marker:
    ALIGN ERROR ~ 0 deg
    LAT         ~ 0 deg
    VERT        ~ 0 deg

The 14.22.70 reconstructed P/N/R path remains only as fallback.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_72_guid_orbitmap_authoritative_vector.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected NavigationTests if 14.22.71b was 43/0:
  44 passed, 0 failed

PLUGIN
14.22.72 itself does not add another plugin change beyond 14.22.71.
Use the 14.22.71-built KMC.Plugin.dll already copied into KSP.

No transfer solver math changed.
