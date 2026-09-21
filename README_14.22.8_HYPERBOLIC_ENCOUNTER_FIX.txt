KMC 14.22.8 - Hyperbolic Encounter Telemetry Fix

ROOT CAUSE
When a maneuver created a child-body SOI encounter, KSP could expose the encounter patch as a hyperbolic orbit (eccentricity > 1). OrbitMapTelemetrySender correctly represented that orbit with ApoapsisMeters = NaN because a hyperbolic orbit has no apoapsis. OrbitMapPacket.SerializeOrbit incorrectly treated apoapsis as a required finite field, so packet serialization threw and the sender stopped transmitting. Mission Control then aged the last packet to UNAVAILABLE.

FIX
KMC.shared/OrbitMapPacket.cs now treats ApoapsisMeters as an optional finite field, matching PeriodSeconds/patch end-time behavior. Empty apoapsis serializes and parses back as NaN. Periapsis remains required and finite.

FILES
MODIFY: KMC.shared/OrbitMapPacket.cs
ADD: Tools/OrbitMap/tests/test_14_22_8_hyperbolic_encounter_protocol.py

BUILD / DEPLOY
1. Extract this ZIP over the repository root.
2. Rebuild KMC.shared in Release.
3. Rebuild KMC.Plugin and KMC.MissionControl in Release so both applications consume the corrected shared assembly.
4. Deploy the newly built shared/plugin assemblies using the same process used for 14.22.8.
5. Re-test the same Kerbin -> Mun maneuver encounter.

EXPECTED RUNTIME RESULT
- RX continues increasing after the encounter appears.
- MAP remains LIVE instead of ORBIT DATA UNAVAILABLE.
- Mun encounter patch can proceed to rendering/coordinate validation.

KSP Plugin DLL Required? NO source change; rebuild/redeploy recommended because it consumes KMC.shared.
Mission Control rebuild required? YES, to consume corrected KMC.shared.
KMC.shared.dll change required? YES.
