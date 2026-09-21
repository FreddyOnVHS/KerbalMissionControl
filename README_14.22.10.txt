KMC 14.22.10 - Encounter Body / Trajectory Phase Alignment Fix

BASELINE
- Applies on top of KMC 14.22.9 Encounter Body Visualization.
- Repo-root drag/drop overlay.

ROOT CAUSE
The future encounter body marker was propagated from Orbit.meanAnomalyAtEpoch while the
current child-body marker was anchored from KSP's authoritative true-anomaly position in
KMC's canonical frame. That allowed the future Mun phase to drift away from the projected
patch/encounter location even though both were on the same orbital path.

FIX
- Treat the transmitted child-body PositionX/Y/Z as the authoritative phase anchor at the
  packet UniversalTimeSeconds.
- Recover the current true anomaly from that canonical position.
- Propagate forward from packet UT to each projected patch sample UT.
- Use the exact same anchored propagation for the encounter body/SOI marker.
- Current child-body markers use the transmitted authoritative position directly.

MODIFIED
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs
- KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs
- Tools/OrbitMap/tests/test_14_22_9_encounter_body_visual.py

ADDED
- Tools/OrbitMap/tests/test_14_22_10_encounter_phase_alignment.py

BUILD
- Rebuild KMC.MissionControl in Release.
- No KSP Plugin DLL change.
- No KMC.shared.dll change.

RUNTIME TEST
1. Keep/recreate the same Kerbin -> Mun maneuver encounter.
2. Compare KSP Map View and KMC MAP.
3. Verify the Mun ENC disk/SOI ring lies at the same future phase on Mun's orbit as KSP.
4. Verify the orange encounter patch meets/crosses the SOI ring around Mun ENC instead of
   appearing displaced along Mun's orbit.
5. Verify RX keeps increasing and geometry rebuild remains low when the node is not edited.
