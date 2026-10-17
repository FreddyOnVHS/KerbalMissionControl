KMC 14.22.71 - Authoritative Maneuver Vector in OrbitMap

PURPOSE
Replace the standalone UDP-5103 maneuver-vector experiment with authoritative
maneuver-vector data carried by the already-established OrbitMap telemetry
channel on UDP 5110.

THIS BUILD
- Extends OrbitMapManeuverNode with:
    AuthoritativeBurnVectorAvailable
    AuthoritativeBurnRight
    AuthoritativeBurnNose
    AuthoritativeBurnReferenceForward
- KSP OrbitMapTelemetrySender calls:
    ManeuverNode.GetBurnVector(active.orbit)
- The returned vector is normalized and resolved into:
    vessel.ReferenceTransform.right
    vessel.ReferenceTransform.up
    vessel.ReferenceTransform.forward
- OrbitMap node serialization grows from 10 to 14 fields.
- Parser remains backward-compatible with existing 10-field nodes.
- Adds a NavigationTests protocol roundtrip / legacy-compatibility regression.

IMPORTANT
14.22.71 intentionally does NOT switch GUID over yet.
It establishes and tests the authoritative data path first.
14.22.72 will consume this OrbitMap vector in GUID after runtime proof.

APPLY
From repo root:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_71_orbitmap_authoritative_vector.ps1

THEN
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected NavigationTests if your current tree has 42 tests:
  43 passed, 0 failed

RUNTIME
This build changes KMC.Plugin and KMC.shared.
Rebuild KMC.Plugin.dll and copy the rebuilt DLL into KSP GameData.

No transfer solver math or Guidance math changed.
