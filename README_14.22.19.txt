KMC 14.22.19 — Encounter Frame Rotation Fix

Root cause
----------
14.22.18 diagnostics showed the KSP fixed/right-handed vectors and KMC
canonical vectors are separated by the same rigid angular offset at the
same UT. KSP patch timing, SOI bounds, child-body translation, and packet
serialization were already verified.

Fix
---
For child-body encounter samples only:
1. Compute the future child-body center from KSP at the sample UT.
2. Compute that same body center in KMC's proven canonical scene frame.
3. Derive the signed rigid rotation between the two.
4. Apply that exact rotation to BOTH the child-body center and the
   spacecraft parent-frame sample.

No changes to packet format, SOI logic, camera, Mission Control renderer,
or maneuver timing.

Build
-----
Rebuild KMC.Plugin in Release.
Redeploy KMC.Plugin.dll to GameData/KMC/Plugins.

Runtime test
------------
Recreate the same Mun encounter.
Compare KSP and KMC:
- Mun ENC should occupy the same side/phase of Mun's orbit.
- The projected path should approach from the same side.
- The child encounter segment should remain continuous through SOI entry.
- RX should continue increasing.

The 14.22.18 diagnostic logging remains enabled for this validation run.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
