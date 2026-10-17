KMC 14.22.71b - OrbitMap Legacy Regression Fix

Fixes only the new 14.22.71 NavigationTests regression.

Root cause:
The test tried to replace a hand-formatted serialized node string. The exact
floating-point text did not match Serialize(), so the replacement never
occurred and the parser correctly saw the original 14-field node.

Fix:
Trim the actual serialized maneuver-node field structurally from 14 fields to
10 fields before parsing it as a legacy packet.

No production behavior changed.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_71b_orbitmap_legacy_test_fix.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected:
  43 passed, 0 failed
