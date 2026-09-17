KMC 14.22.3 — VSL COORDINATE ALIGNMENT FIX

ROOT CAUSE
----------
The orbit curve/AP/PE were reconstructed in Mission Control from orbital
elements using KMC's canonical XYZ transform, while the VSL marker was sent as
KSP's native getRelativePositionAtUT() vector. Those two coordinate conventions
do not share the same axis mapping, so the vessel marker could appear at the
wrong point on an otherwise correct orbit.

FIX
---
MODIFY:
  KMC.Plugin/OrbitMapTelemetrySender.cs

The active-vessel marker is now constructed from the active orbit's true
anomaly using the exact same conic transform used by Mission Control.
No packet/protocol format was changed.

SCOPE
-----
This build intentionally fixes VSL only.
Target and maneuver-node coordinate alignment are left unchanged for separate
validation later.

TEST
----
Tools/OrbitMap/tests/test_14_22_3_vsl_coordinate_alignment.py

Verification in this environment:
  RED: new regression test failed before production change.
  GREEN: new regression test passed after production change.
  Full available OrbitMap Python regression suite was run afterward.

RUNTIME ACCEPTANCE
------------------
1. Overlay this package on the current 14.22.2 source.
2. Build KMC.Plugin in Release.
3. Copy the rebuilt KMC.Plugin.dll to GameData\KMC\Plugins.
4. Launch the same vessel/orbit used for the screenshot comparison.
5. Compare KMC VSL against the vessel's location in KSP Map View.
6. Rotate the KMC camera and confirm VSL stays on the orbit curve.
7. Confirm RX continues increasing without STALE/UNAVAILABLE.

KSP Plugin DLL Required? YES
Mission Control does not need to be rebuilt for this VSL-only fix.
