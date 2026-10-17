KMC 14.22.71a - OrbitMap Authoritative Vector Apply-Script Fix

Fixes a PowerShell parser error in the 14.22.71 apply script.

Root cause:
The script used C-style quote escaping (") inside a PowerShell double-quoted
string. PowerShell requires backtick escaping or, more simply here, a
single-quoted outer string.

No C# source logic changed from 14.22.71.

Apply from repo root:
  powershell -ExecutionPolicy Bypass -File .\apply_14_22_71_orbitmap_authoritative_vector.ps1

Then:
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

Expected NavigationTests if the current tree had 42 tests:
  43 passed, 0 failed

This build still changes KMC.shared and KMC.Plugin, so rebuild the plugin and
copy the rebuilt KMC.Plugin.dll into KSP for runtime validation.
