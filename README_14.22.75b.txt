KMC 14.22.75b - Manual Node / Burn Estimate Decoupling

ROOT CAUSE CONFIRMED FROM RUNTIME
COMPUTE produced:
  EVIDENCE: MASS/THRUST TELEMETRY CANNOT PRODUCE A BURN-DURATION ESTIMATE.

UPLOAD MNV then produced:
  MANEUVER PLAN IS NOT AVAILABLE

That is why GUID showed no active maneuver node.

CODE CAUSE
The three manual planners returned PLAN UNAVAILABLE whenever
EstimateBurnDuration() returned NaN.

A valid KSP maneuver node does NOT require a burn-duration estimate.
It requires:
- vessel identity
- node UT
- P/N/R Delta-V

The old code incorrectly coupled node validity to burn-duration availability.

FIX
Manual:
- PROGRADE / RETROGRADE
- NORMAL / ANTI-NORMAL
- RADIAL OUT / IN

no longer fail solely because burn duration cannot be estimated.

When estimate is unavailable:
- Plan.Available remains true
- Node UT remains valid
- requested Delta-V remains valid
- EstimatedBurnDurationSeconds = NaN
- IgnitionLeadSeconds = NaN
- IgnitionMissionTimeSeconds = NaN
- evidence explicitly reports that burn timing is unavailable

No fake burn duration or ignition lead is invented.

GUID consequence:
After UPLOAD MNV and KSP node verification, GUID can receive and display the
maneuver vector. Burn readiness may report TIMING UNAVAILABLE until a valid
burn estimate exists. This is intentional and safer than inventing timing.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_75b_manual_node_burn_estimate_decouple.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.75a was 48/0:
  49 passed, 0 failed

RUNTIME
1. MANUAL PRO/RETRO
2. DV = 100
3. NODE T+ = 120
4. COMPUTE

Expected:
- plan AVAILABLE = YES
- node UT populated
- BURN DURATION = ---
- IGNITION LEAD = ---
- footer eventually PLAN READY - CLICK UPLOAD MNV

5. Click UPLOAD MNV

Expected:
- UPLINK SENT - AWAITING PLUGIN ACK
- then NODE LOADED / NODE VERIFIED
- GUID receives the node and maneuver vector

This build changes only manual maneuver planning validity. It does not alter
automatic apsis maneuver burn-estimate requirements.
