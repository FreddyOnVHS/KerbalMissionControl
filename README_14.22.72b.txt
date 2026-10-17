KMC 14.22.72b - NavigationTests Orbit Namespace Fix

Compile-only test fix on top of 14.22.72a.

Adds:
  using KMC.Engine.Orbit;

to:
  Tools/NavigationTests/Program.cs

Reason:
The new 14.22.72 regression directly references
ManeuverVectorTelemetryStore and ManeuverVectorTelemetryModel.

No production code or runtime behavior changed.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_72b_navtest_orbit_namespace_fix.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected:
  44 passed, 0 failed
