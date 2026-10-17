KMC 14.22.73a - Partial Apply Repair

USE THIS AFTER the original 14.22.73 script stopped at:
  Could not locate FormatDuration in KMC.MissionControl\Pages\OrbitPage.cs

DO NOT rerun the original 14.22.73 script.

ROOT CAUSE
The generic formatter patch assumed the parameter name was "seconds".
OrbitPage uses "totalSeconds". The original script therefore stopped after
partially applying the build.

73a repairs the partial state deterministically:
- Replaces the complete formatter methods in MAP, MNV, GUID, ORBIT and the
  Ascent footer using method-to-method boundaries.
- Verifies KspTimeDisplay and CalendarDayLengthSeconds were created.
- Verifies standalone UDP 5103 / KMC-MNVV1 code is gone.
- Finishes the regression changes the interrupted script never reached.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_73a_partial_apply_repair.ps1

THEN
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.72b was 44/0:
  45 passed, 0 failed

If tests pass, rebuild KMC.Plugin Debug and copy KMC.Plugin.dll into KSP.
