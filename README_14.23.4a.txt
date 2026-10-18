KMC 14.23.4a — Engine Bell Scale / Attach

PURPOSE
Refine the stylized engine bell introduced in 14.23.4.

CHANGES
- reduce bell height to about 74% of the live engine envelope
- reduce nozzle exit width to about 68% of the full engine envelope width
- narrow the throat/top
- bias the bell upward slightly so it visually attaches to the engine mount

The rest of SCHEM is unchanged.

NO KSP PLUGIN CHANGE
This is Mission Control rendering only.
You do NOT need to rebuild or replace KMC.Plugin.dll.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_4a_engine_bell_scale_attach.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
If 14.23.4 was 56/0:
  57 passed, 0 failed

RUNTIME
Rebuild/run Mission Control and compare the engine:
- it should be noticeably smaller
- it should sit closer to the vehicle
- it should still read clearly as a rocket nozzle
