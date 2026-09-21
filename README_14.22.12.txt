KMC 14.22.12 - SOI-Anchored Encounter Patch

Purpose
-------
Fix child-body encounter geometry that remained on the wrong scale/side after 14.22.11.

Root cause
----------
Future hyperbolic patched-conic epoch/anomaly values were being used as the phase anchor for the child-body encounter patch. In runtime this could place the Mun-local hyperbola far outside Mun's SOI even though KSP's patch begins at the SOI boundary.

Fix
---
- Hyperbolic child-body encounter patches are anchored at the encountered body's SOI radius at patch StartUT.
- Both mathematical SOI-entry branches are constructed.
- The branch whose first primary-frame point best connects to the previous parent-body patch endpoint is selected.
- Propagation after StartUT uses the SOI-entry mean anomaly as the new phase origin.
- The 14.22.11 geometric mirror workaround is removed.
- Existing moving-body translation and encounter-body visualization remain intact.

Files
-----
MODIFY KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs
MODIFY Tools/OrbitMap/tests/test_14_22_11_encounter_frame_handedness.py
ADD    Tools/OrbitMap/tests/test_14_22_12_soi_anchored_encounter.py

Build
-----
Rebuild KMC.MissionControl in Release.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO

Runtime test
------------
Keep the same Kerbin -> Mun encounter.
Verify:
1. MAP remains LIVE and RX continues to increment.
2. Mun ENC remains at the projected Mun encounter position.
3. The child-body encounter trajectory begins at approximately the Mun SOI ring rather than extending back toward Kerbin.
4. The parent transfer and Mun-local encounter segment join continuously at the SOI transition.
5. The approach side matches KSP Map View.
