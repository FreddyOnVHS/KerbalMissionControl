KMC 14.22.74 - Maneuver Execution Readiness / Integration Pass

BASE
Verified GitHub master before build:
  14.22.73a
  1d9ae755845b65ead901328dfa816e70bb56ec2b

WHY
MAP currently says READY FOR BURN as soon as the node exists, but the Engine
does not consider the maneuver actually ready until the node is verified,
a valid maneuver vector exists, alignment is within the 2-degree hold
tolerance, and valid ignition timing exists.

This build makes that distinction explicit without inventing a new propulsion
or authority gate that does not exist in current GuidanceSystem data.

CHANGES

1. GuidanceSolutionModel gains:
     BurnReadiness

2. GuidanceSystem centrally classifies the completed solution after every
   update:
     UNAVAILABLE
     REPLAN REQUIRED
     NODE NOT LOADED
     NODE NOT VERIFIED
     VECTOR UNAVAILABLE
     ALIGNMENT UNKNOWN
     ALIGN REQUIRED
     TIMING UNAVAILABLE
     READY
     FINAL COUNT
     IGNITION DUE
     BURN IN PROGRESS
     COMPLETE / VERIFYING
     COMPLETE / VERIFIED

3. GUID EXECUTION panel now shows:
     BURN STATUS  <state>

4. Handoff language is corrected:
     MAP: NODE CREATED / READY FOR GUIDANCE
     MNV: NODE VERIFIED / READY FOR GUIDANCE
     GUID: READY only when the Engine readiness criteria are satisfied

5. Adds regression:
     maneuver execution readiness is Engine-owned and explicit

NO BEHAVIOR CHANGE
This does NOT change:
- transfer solver math
- maneuver-node math
- KSP-authoritative vector math
- alignment tolerances
- burn start criteria
- throttle advisory logic
- execution interlock semantics

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_74_maneuver_execution_readiness.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.73a was 45/0:
  46 passed, 0 failed

RUNTIME ACCEPTANCE
Using a verified Duna node:

1. Deliberately point away from maneuver marker:
     MAP/MNV remain READY FOR GUIDANCE
     GUID BURN STATUS = ALIGN REQUIRED

2. Align within 2 degrees, well before ignition:
     GUID BURN STATUS = READY
     Crew command = HOLD MANEUVER VECTOR

3. Enter final 10 seconds:
     GUID BURN STATUS = FINAL COUNT

4. At ignition:
     GUID BURN STATUS = IGNITION DUE

The system remains advisory only; no autopilot behavior is added.
