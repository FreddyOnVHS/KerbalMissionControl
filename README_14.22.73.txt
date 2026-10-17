KMC 14.22.73 - Maneuver Vector Cleanup + KSP Calendar Time

BASE
Apply on top of the proven 14.22.72b tree.

CLEANUP
Removes the abandoned standalone authoritative maneuver-vector experiment:
- UDP 5103
- KMC-MNVV1
- ManeuverVectorTelemetryReceiver
- MainForm standalone receiver plumbing
- VelocityVectorTelemetrySender standalone node-vector transmission

The Engine ManeuverVectorTelemetryStore remains because the proven OrbitMap
UDP 5110 path publishes authoritative node vectors into it.

TIME FIX
OrbitMap telemetry now carries KSP's active display-calendar day length:
- 21600 s when GameSettings.KERBIN_TIME is enabled
- 86400 s for Earth-style time

Mission Control centrally uses that day length for:
- MAP
- MNV
- GUID
- ORBIT
- Ascent footer

This fixes cases such as:
KSP: 244d 05h ...
KMC old: 61d 05h ...
KMC new: 244d 05h ...

PROTOCOL
CalendarDayLengthSeconds is an optional tail field on OrbitMap packets.
Older packets remain accepted and default to Kerbin time.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_73_cleanup_ksp_time.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected if 14.22.72b was 44/0:
  45 passed, 0 failed

RUNTIME
This build changes KMC.Plugin and KMC.shared.
Rebuild KMC.Plugin.dll and copy the Debug DLL into KSP GameData before testing.

Acceptance:
- KMC starts with no UDP 5103 maneuver-vector receiver.
- GUID still shows KSP-AUTHORITATIVE MANEUVER VECTOR.
- Alignment remains near zero when pointed at KSP maneuver marker.
- KMC day/hour time matches KSP's selected time convention.
