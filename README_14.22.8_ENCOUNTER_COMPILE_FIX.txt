KMC 14.22.8 - Encounter Future-Patch Guard Compile Fix
======================================================

Baseline: 14.22.8 KSP-style multi-body system view + hyperbolic encounter protocol fix + invalid future patch guard.

Fix:
- Removes the duplicate private static bool IsFinite(double value) helper that caused CS0111 in KMC.Plugin/OrbitMapTelemetrySender.cs.
- Keeps the future-patch validation guard intact.
- Keeps optional hyperbolic apoapsis serialization support intact.

INSTALL
-------
Extract this ZIP over the KMC repository root and allow folders/files to merge/replace.

REBUILD (Release)
-----------------
1. KMC.shared
2. KMC.Plugin
3. KMC.MissionControl

Deploy updated KMC.Plugin.dll and KMC.shared.dll to:
GameData/KMC/Plugins/

RUNTIME TEST
------------
1. Start in Kerbin orbit and verify MAP telemetry RX is climbing.
2. Create a maneuver node that produces a Mun encounter.
3. Verify RX continues climbing and MAP does not become STALE/UNAVAILABLE.
4. If the plugin logs "[KMC] Orbit map skipped invalid future patch", retain the log and send it for follow-up geometry validation.

VERIFICATION PERFORMED
----------------------
- TDD regression before fix: duplicate IsFinite helper test FAILED (found 2 definitions).
- After fix: duplicate helper test PASSED.
- 14.22.8 structural/regression tests: 18/18 PASSED.
- C# compilation must still be verified in Visual Studio.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? YES (cumulative package includes protocol fix)
