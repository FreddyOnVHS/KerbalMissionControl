KMC 14.22.70d - Maneuver Vector UDP Port Fix

ROOT CAUSE
KMC already uses UDP 5102 for ElectricalControlReceiver / KMC-ELEC1.

14.22.70b accidentally assigned the new KSP-authoritative maneuver-vector
telemetry receiver to the same UDP 5102 port.

During MainForm startup:
1. ManeuverVectorTelemetryReceiver bound UDP 5102.
2. ElectricalControlReceiver then attempted to bind UDP 5102.
3. Windows rejected the second bind with AddressAlreadyInUse.

FIX
Move KSP-authoritative maneuver-vector telemetry:
    UDP 5102 -> UDP 5103

Updated on both sides:
- KMC.Plugin/VelocityVectorTelemetrySender.cs
- KMC.MissionControl/Transport/VelocityVectorTelemetrySample.cs

Existing electrical control remains unchanged on UDP 5102.

APPLY
From repo root:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_70d_maneuver_vector_port_fix.ps1

THEN
Rebuild KMC.MissionControl and KMC.Plugin.

RUNTIME
Replace KMC.Plugin.dll in KSP with the newly rebuilt DLL.
Both sender and receiver must use UDP 5103.

No maneuver math, transfer math, or guidance math changed.
