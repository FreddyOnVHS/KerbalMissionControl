KMC 14.22.4 — TARGET & MANEUVER MARKER COORDINATE ALIGNMENT

PURPOSE
-------
Apply the same proven coordinate-frame correction used for VSL to:
  - vessel target marker position
  - maneuver node marker position

ROOT CAUSE
----------
KMC reconstructs orbit geometry in a canonical orbital-element XYZ frame.
KSP native getRelativePositionAtUT() vectors use a different celestial axis
convention. Mixing those frames displaced markers relative to otherwise-correct
orbit geometry.

MODIFY
------
KMC.Plugin/OrbitMapTelemetrySender.cs
  - Vessel target position now uses CanonicalPositionAtTrueAnomaly().
  - Maneuver node position now converts node UT -> true anomaly, then uses
    CanonicalPositionAtTrueAnomaly().
  - Protocol remains KMC-ORBITMAP1.

ADD
---
Tools/OrbitMap/tests/test_14_22_4_target_maneuver_coordinate_alignment.py

VERIFICATION HERE
-----------------
TDD:
  RED: new regression test fails before implementation.
  GREEN: new regression test passes after implementation.
  Full available OrbitMap Python regression suite is run afterward.

RUNTIME TEST
------------
1. Overlay ZIP on current 14.22.3 source.
2. Build KMC.Plugin in Release.
3. Copy rebuilt KMC.Plugin.dll to GameData\KMC\Plugins.
4. In KSP, select another vessel as target.
5. Compare KMC TGT marker/orbit against KSP Map View.
6. Create one maneuver node.
7. Compare KMC MNV1 marker against KSP Map View.
8. Confirm RX continues climbing and map remains LIVE.
9. Send screenshots of KSP Map View and KMC MAP for validation.

NOTE
----
This build does not change projected patched-conic geometry or transfer
calculations yet. It is marker-frame alignment only.

KSP Plugin DLL Required? YES
Mission Control does not need to be rebuilt for this build.
