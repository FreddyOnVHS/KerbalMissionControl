KMC 14.22.41 UI Refinement — Transfer Planner Overlap Fix
===========================================================

BASE
----
Local, unpushed 14.22.41 Finite-SOI Departure Matching build.

PURPOSE
-------
Fix the TRANSFER planner text collision seen during the successful Duna
finite-SOI live test.

NO NAVIGATION MATH CHANGES.

ROOT CAUSE
----------
The right-side planner accumulated several full-height diagnostic sections:
- destination metadata
- Hohmann window
- coarse Lambert
- parking-aware Lambert
- finite-SOI diagnostics
- legacy parking ejection

The KSP node-status block is independently anchored to the bottom of the
planner. The diagnostic stack eventually grew into that reserved area.

FIX
---
The transfer planner is now rendered as compact summary rows:

Destination:
  PARENT / SMA / ECC
  INC / PERIOD

Hohmann:
  PHASE / REQUIRED / WINDOW
  DEPARTURE UT / TOF / PARENT DV

Lambert:
  PATH / DEPARTURE UT / TOF
  DEP VINF / ARR VINF / SCORE

Parking-aware:
  selected path / departure / arrival VINF
  burn UT / offset / P-N-R vector
  ejection DV / geometry residual

Finite SOI:
  exit UT / time to exit
  position error / velocity error / state score

Legacy create path:
  altitude / VINF / DV
  burn UT / window offset / radius

The redundant "NODE CANDIDATE READY" text line was removed because the
bottom control/status area already communicates node readiness.

A separator line is now drawn above the dynamically bottom-anchored node
status block.

UNCHANGED
---------
- Lambert math
- finite-SOI ranking
- CREATE LAMBERT TEST NODE packet
- legacy CREATE KSP NODE authority
- plugin
- shared protocol
- test-node no-refine safety

LIVE CHECK
----------
Reopen Duna TRANSFER page and confirm:
- no text overlaps
- finite-SOI diagnostics remain readable
- NODE STATUS / KSP transfer assessment remain readable
- buttons remain clickable

Then continue the planned Eve finite-SOI test.
