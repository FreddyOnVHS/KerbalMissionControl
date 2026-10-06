KMC 14.22.46 UI Fix — Target-SOI Block Visibility
=================================================

PROBLEM
-------
The 14.22.46 live Duna page showed the older FINITE-SOI LOCAL CORRECTION block
but hid the newer TARGET-SOI SHOOTING block, even though there was visually
enough space.

CAUSE
-----
The target-shooting block requires up to six rows. The page first consumed
space rendering the older finite-SOI diagnostics, then rejected the target
block because its conservative space check failed.

FIX
---
When a target-SOI shooting result exists:

- FINITE-SOI LOCAL CORRECTION is not rendered.
- TARGET-SOI SHOOTING becomes the authoritative visible diagnostic block.
- The target block allows an exact fit at the content boundary.

This is a UI-only change.

UNCHANGED
---------
- Lambert search
- finite-SOI math
- target-SOI shooting math
- safe periapsis math
- production burn UT
- P/N/R values
- node authority
- KSP plugin
- shared protocol

AUTOMATED TESTS
---------------
Expected unchanged:
NavigationTests: 25 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE CHECK
----------
Open Duna TRANSFER before creating the node.

Expected visible block:
TARGET-SOI SHOOTING
MISS ...
ARR UT ... PREDICT ENCOUNTER
TARGET PE R ...
PRED PE R ...
PRED PE ALT ... SAFE FLYBY
ITER ... EVAL ...

Do not push until the target block is visible and the live Duna node remains
an encounter.
