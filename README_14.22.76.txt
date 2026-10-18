KMC 14.22.76 - Post-Burn Cleanup / Queue Handoff

BASE
14.22.75b2
cd2f1d54770eb3485bb5ef077ddc13b23e890a80

This build deliberately combines the largest safe coherent scope for one
milestone: completed maneuver -> node cleanup -> next-plan handoff.

Included:
- GUID EXECUTION adds NEXT ACTION.
- GUID says REMOVE COMPLETED NODE after a completed burn.
- When cleanup is confirmed and another retained KMC node exists, GUID says
  ACTIVATE KMC #n.
- If no next KMC node exists, GUID says CLEANUP COMPLETE / QUEUE COMPLETE.
- MNV auto-selects the completed active KMC node while it remains in KSP.
- DELETE changes to REMOVE COMPLETE NODE for that completed maneuver.
- Removal command gets visible feedback.
- MNV footer says CLEANUP COMPLETE - ACTIVATE KMC #n or QUEUE CLEAR.
- One regression covers the whole handoff.

Not included:
- automatic node deletion
- automatic next-plan activation
- solver changes
- guidance-vector changes
- burn timing changes
- cutoff changes
- deferred pre-ignition engine capability telemetry

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_76_post_burn_queue_handoff.ps1

Test:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.75b2 was 49/0:
  50 passed, 0 failed
