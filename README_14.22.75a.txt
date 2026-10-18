KMC 14.22.75a - MNV Compute / Upload Action Feedback Fix

BASE
Apply on top of the current local 14.22.75 build.
GitHub source reviewed from frozen 14.22.74 baseline:
  0cd5aa976dba73c86475f30c41ab15d31c55d7ed

ROOT CAUSE
The buttons are wired correctly:
  COMPUTE    -> OnManeuverComputeClick
  UPLOAD MNV -> OnManeuverUploadClick

However, operator feedback was effectively invisible.

UPLOAD MNV already returns useful results such as:
  NO ENGINE MANEUVER PLAN
  MANEUVER PLAN IS NOT AVAILABLE
  KSP UNIVERSAL TIME IS NOT AVAILABLE
  VESSEL ID IS NOT AVAILABLE
  UPLINK SENT - AWAITING PLUGIN ACK
  UPLINK FAILED: ...

MainForm previously discarded resultText.

Manual MNV COMPUTE can also legitimately wait for matched KMC-EPOCH1
telemetry, but the operator had no immediate confirmation that COMPUTE was
accepted.

FIX
- Adds ManeuverOperatorFeedbackStore (UI-only, 8-second lifetime).
- COMPUTE immediately shows:
    COMPUTE REQUESTED - WAITING FOR ENGINE PLAN
- UPLOAD MNV displays the exact existing receiver result.
- MNV footer shows action feedback even when no Engine plan exists.
- Feedback is presentation only and never becomes maneuver truth.
- Store clears at startup/shutdown.

NO LOGIC CHANGE
No changes to:
- ApsisManeuverPlanner
- ManeuverPlanningSystem
- request types or values
- manual T+ anchoring
- KMC-EPOCH1 requirements
- maneuver uplink packet
- plugin protocol
- KSP node creation

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_75a_mnv_action_feedback.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.75 was 47/0:
  48 passed, 0 failed

RUNTIME
1. Select MANUAL PRO/RETRO.
2. Enter DV and NODE T+.
3. Click COMPUTE.
   Footer must immediately show COMPUTE REQUESTED.
4. Wait for Engine update.
   Main MNV plan fields should show the resulting plan/status.
5. Click UPLOAD MNV.
   Footer must show the exact result:
   - success: UPLINK SENT - AWAITING PLUGIN ACK
   - or the specific rejection reason.

This build is diagnostic/QOL only; it does not mask an underlying planning
failure. If COMPUTE is being rejected by epoch/vessel prerequisites, the
screen will now make that visible.
