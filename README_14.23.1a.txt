KMC 14.23.1a - NavigationTests Topology Namespace Fix

The 14.23.1 production code compiled successfully. The failure was only in
Tools/NavigationTests/Program.cs because the apply script failed to insert:

using KMC.Shared.Topology;

This patch adds that missing using directive only.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_1a_navigation_test_using_fix.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected:
  51 passed, 0 failed
