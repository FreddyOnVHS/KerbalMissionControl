KMC 14.22.17 — Encounter Diagnostic Probe

This is a diagnostic build, not a geometry fix.

Purpose
-------
Capture exactly where KSP and KMC diverge during a Mun encounter without
making another speculative coordinate-frame change.

For each child-SOI patch it logs five samples:
- patch StartUT / EndUT
- reference body / eccentricity / SMA / SOI
- KSP raw patch-relative position (.xzy)
- KSP raw child-body center (.xzy)
- raw combined parent-frame point
- fixed right-handed local position
- fixed right-handed child-body center
- fixed combined parent-frame point
- transmitted parent-frame point
- local radius and distance error from SOI

Log prefix
----------
[KMC-ORBIT-DIAG]

How to test
-----------
1. Rebuild KMC.Plugin Release.
2. Replace GameData/KMC/Plugins/KMC.Plugin.dll.
3. Start KSP fresh.
4. Create ONE Mun encounter that KMC renders incorrectly.
5. Let it sit for 2-3 seconds.
6. Exit KSP.
7. Send KSP.log back to ChatGPT.

Do not change Mission Control for this build.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
