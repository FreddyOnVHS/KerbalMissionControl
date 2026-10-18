KMC 14.22.75b1 - Corrected Manual Node Burn-Estimate Patch

This replaces the failed 14.22.75b apply script.

The failed script stopped BEFORE writing ApsisManeuverPlanner.cs, so no
rollback is required.

Root cause of apply failure:
The first patch script modified method text in memory and then failed while
locating the next manual method.

75b1:
- locates all method boundaries before modifying source
- patches from bottom to top
- verifies exactly all three manual maneuver methods were changed
- refuses to write if any BURN ESTIMATE UNAVAILABLE hard rejection remains
  in the manual-method section

Production behavior intended is unchanged from 75b:
A manual node remains valid/uploadable even if burn duration cannot be
estimated. Burn duration, ignition lead and ignition MET remain unavailable.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_75b1_manual_node_burn_estimate_decouple.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 75a was 48/0:
  49 passed, 0 failed
