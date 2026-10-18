KMC 14.22.75 - Final Count / Ignition Cue

BASE
Verified GitHub master:
  14.22.74
  0cd5aa976dba73c86475f30c41ab15d31c55d7ed

CODE REVIEW RESULT
GuidanceSystem already owns the required behavior:
- IgnitionStandbySeconds = 10.0
- FINAL COUNT / IGNITION STANDBY inside T-10 s
- IGNITION DUE / IGNITE / HOLD MANEUVER VECTOR at T-0

Therefore this build does NOT duplicate or change Engine timing logic.

CHANGES
GUID crew-command panel now renders:

During FINAL COUNT:
  IGNITION STANDBY
  T- 8.4 S

Color: ORANGE
Alert box: blinking at the existing render refresh rate

At IGNITION DUE:
  IGNITE NOW
  HOLD MANEUVER VECTOR

Color: RED
Alert box: blinking

The normal READY state remains:
  HOLD MANEUVER VECTOR
  green when aligned

Adds regression:
  GUID final count and ignition cues are explicit

NO BEHAVIOR CHANGE
No changes to:
- transfer solver
- maneuver node
- node verification
- authoritative maneuver vector
- 2-degree hold alignment tolerance
- 5-degree burn alignment limit
- 10-second final-count threshold
- 2.5-second burn-start detection window
- throttle advisory semantics
- actual burn detection

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_75_final_count_ignition_cue.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.74 was 46/0:
  47 passed, 0 failed

RUNTIME ACCEPTANCE
With a verified maneuver aligned:

1. Before T-10 s:
   BURN STATUS READY
   HOLD MANEUVER VECTOR

2. Inside T-10 s:
   BURN STATUS FINAL COUNT
   IGNITION STANDBY
   T- x.x S
   orange blinking alert

3. At T-0:
   BURN STATUS IGNITION DUE
   IGNITE NOW
   HOLD MANEUVER VECTOR
   red blinking alert

This remains advisory only. No autopilot is added.
