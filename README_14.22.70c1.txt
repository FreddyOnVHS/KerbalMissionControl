KMC 14.22.70c1 - UDP Bind Diagnostics Patcher Fix

Fixes the 14.22.70c APPLY SCRIPT only.

Root cause:
RadialTelemetryReceiver and ManeuverVectorTelemetryReceiver both contain the
same compact UdpClient bind statement. The 70c script used String.Replace(),
which replaced every matching occurrence while patching RADIAL, leaving no
unmodified occurrence for the MANEUVER VECTOR step.

70c1 targets each receiver class independently before replacing its bind.

The failed 70c run should not have written VelocityVectorTelemetrySample.cs,
because that file was written only after all three receiver replacements
completed successfully.

Apply:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_70c1_udp_bind_diagnostics_patch_fix.ps1

Then rebuild KMC.MissionControl and launch KMC.
