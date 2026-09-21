KMC 14.22.23 — Closest-Approach Encounter Visual Fix

Baseline:
GitHub master HEAD 7264c88c1aed22afe501ca3f41e58ffadaaa0714

Fix:
- Restores the future Mun's canonical position at EACH child-patch sample UT.
- Removes the incorrect fixed SOI-entry encounterBodyAnchor.
- Keeps the actual projected trajectory in the correct parent-frame position.
- Mission Control now places Mun ENC at the reference-body position from the
  sample with minimum vessel-to-body distance (closest approach), not sample 0.
- EncounterUniversalTimeSeconds is the closest-approach sample UT.

Why:
The orange trajectory point and the Mun ENC marker now refer to the SAME UT at
closest approach, so their visual separation corresponds to the real flyby
clearance instead of comparing different times.

Build:
- KMC.Plugin Release
- KMC.MissionControl Release

Deploy:
- KMC.Plugin.dll

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
