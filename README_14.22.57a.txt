KMC 14.22.57a - Coupled Finite-SOI Shadow Diagnostic Priority Fix

Purpose
-------
Make the 14.22.57 coupled finite-SOI shadow diagnostics visible on the Transfer Planner when the legacy TARGET-SOI SHOOTING block would otherwise consume the remaining vertical space.

Changes
-------
- Moves coupled finite-SOI shadow diagnostics ahead of the legacy target-SOI shooting diagnostics.
- Compresses the shadow display from seven lines to five lines.
- Preserves the shadow-only authority boundary: NO NODE AUTHORITY.
- Does not change interplanetary solver math.
- Does not change production maneuver selection or CREATE KSP NODE behavior.
- Does not modify the KSP plugin DLL.

Expected Duna display
---------------------
COUPLED FINITE-SOI OPTIMIZER / SHADOW
DV <bootstrap> -> <final> m/s  FEAS <PASS/FAIL> <stage>
SOURCE IF <position error> / <velocity error> m/s  OUT|NOT OUT
TARGET IF <interface error>  PE ERR <periapsis error>  IN|NOT IN [B-PLANE INIT]
ITER <n>  EVAL <n>  NO NODE AUTHORITY

Test
----
Run:
  .\\Tools\\NavigationTests\\Run-Tests.ps1

Then launch KMC and reproduce the Duna transfer. Capture the coupled shadow block.

Do not push this experimental build.
GitHub master remains frozen at 14.22.46 / 6f56ba0df518c062c25b349c0d78fd72ec1e50e2.
