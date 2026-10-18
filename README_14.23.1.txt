KMC 14.23.1 - Vessel Schematic Foundation

BASELINE
14.22.76
bdfe7a76d919d1bfcc698252d94b7b98deaf6f79

IMPORTANT DISCOVERY
KMC already has the transport foundation needed for this feature.

The KSP plugin already:
- builds a full VesselTopology
- records PartId and ParentPartId
- records VesselX / VesselY / VesselZ for every part
- transmits that topology over UDP 5082
- resends it every 2 seconds

14.23.1 therefore does NOT add another KSP sender and does NOT modify the
proven plugin topology builder. Mission Control simply begins consuming the
existing topology stream.

BUILD CONTENTS
- New SCHEM navigation tab
- Page title: VESSEL SCHEMATIC
- Mission Control UDP topology receiver on the existing topology port
- Thread-safe latest-schematic store
- Native KMC vessel schematic renderer
- FRONT / SIDE / TOP clickable views
- Parent/child structural links
- Part-center projection using vessel-relative X/Y/Z
- Simple category glyphs:
    command       diamond
    engine/SRB    triangle
    tank          circle
    solar panel   cross-line
    other parts   square
- Root part highlighted
- Vessel / part count / stage / revision / topology age status
- Regression test verifies topology position round-trip and SCHEM wiring

NOT IN 14.23.1
- Unity mesh streaming
- exact part outlines/bounds
- rotation / zoom
- selectable parts
- failure coloring
- electrical overlays
- propulsion overlays
- automatic commands

This is deliberately a geometry-pipeline proof. Once runtime confirms that
the active craft is recognizable in FRONT/SIDE/TOP, later 14.23.x builds can
improve shape fidelity and add KMC system overlays.

APPLY
  powershell -ExecutionPolicy Bypass -File .\apply_14_23_1_vessel_schematic_foundation.ps1

TEST
  powershell -ExecutionPolicy Bypass -File .\Tools\NavigationTests\Run-Tests.ps1

EXPECTED
Previous baseline: 50 passed, 0 failed
14.23.1 adds one regression:
  51 passed, 0 failed

RUNTIME
1. Rebuild/copy KMC.Plugin if your normal build workflow requires it. The
   topology sender itself is already present in the baseline plugin.
2. Start KSP in flight with an active vessel.
3. Start Mission Control.
4. Open SCHEM.
5. Confirm vessel name and part count appear.
6. Click FRONT, SIDE, TOP and verify each projection changes.
7. Compare the structure to the active KSP vessel.

The first runtime target is structural recognizability, not Vessel Viewer-level
mesh fidelity yet.
