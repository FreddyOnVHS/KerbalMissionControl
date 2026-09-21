KMC 14.22.24 — Maneuver Patch Coloring + Encounter PE Fix

Baseline
--------
GitHub master HEAD:
39bc8c06d3b58b00beb7b480dd7220529f779907

Changes
-------
1. KSP-style projected patch coloring
   - First primary-body projected leg: orange
   - Child-SOI encounter leg: purple
   - Later primary-body projected leg(s): green

   Patch color metadata exists only inside Mission Control scene cache.
   No packet/protocol change.

2. Encounter PE panel fix
   - The ENCOUNTER panel now resolves the actual child-body encounter patch.
   - PE is read from that patch's Orbit.PeriapsisMeters.
   - It no longer accidentally reports the Kerbin transfer patch periapsis.

Unchanged
---------
- projected trajectory geometry
- closest-approach Mun ENC placement from 14.22.23
- KSP plugin telemetry
- packet format
- camera / controls

Build
-----
Rebuild KMC.MissionControl in Release.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
