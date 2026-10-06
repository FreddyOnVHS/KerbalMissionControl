KMC 14.22.38 — MAP Lambert Ejection Preview
============================================

BASELINE
--------
Frozen master HEAD:
919c73641612bfc7b2a07d3bd16c3806f8f18441

PURPOSE
-------
Connect the accepted 14.22.37 Lambert parking-orbit ejection solver to real
MAP telemetry and display its predicted local burn vector.

This remains PREVIEW ONLY.

CREATE KSP NODE continues to use the legacy Hohmann / prograde parking-ejection
candidate.

NEW LIVE DISPLAY
----------------
Below the existing Lambert transfer preview, MAP now displays:

LAMBERT EJECTION PREVIEW / NO NODE AUTHORITY
BURN UT ...  OFF ...
DV P ...  N ...  R ... m/s
TOTAL ... m/s  GEOM RES ... deg

P = local prograde
N = local parking-orbit normal
R = local radial
TOTAL = vector magnitude of the predicted impulse
GEOM RES = asymptote geometry residual

The display is intentionally compact to reduce the risk of colliding with the
bottom-anchored KSP node-status block.

DATA PATH
---------
Real OrbitMap telemetry
  -> Lambert transfer search
  -> departure excess velocity vector
  -> LambertParkingOrbitEjectionPlanner
  -> local prograde / normal / radial preview
  -> MAP display only

UNCHANGED NODE AUTHORITY
------------------------
UploadTransferNode remains unchanged:

Node UT     = legacy _transferNodeCandidate UT
Prograde DV = legacy _transferNodeCandidate prograde
Normal DV   = 0.0
Radial DV   = 0.0

The Lambert ejection preview is not read by UploadTransferNode and cannot
change the KSP node in this build.

KSP Plugin DLL replacement: NOT REQUIRED.
KMC.shared change: NONE.
Mission Control rebuild: REQUIRED.

AUTOMATED TESTS
---------------
Run:

  .\Tools\NavigationTests\Run-Tests.ps1

Expected:
- NavigationTests: 18 passed, 0 failed
- Transfer search: 3 passed, 0 failed
- Lambert ejection: 4 passed, 0 failed

The new NavigationTests case runs the actual MissionControl adapter from a
Lambert transfer preview into a Lambert parking-ejection preview and verifies:
- valid positive total DV
- finite burn UT
- small geometry residual
- P/N/R component magnitude equals total DV

LIVE KSP ACCEPTANCE
-------------------
This build DOES need a live KSP check.

Recommended sequence:
1. Stable near-circular, low-inclination Kerbin parking orbit.
2. MAP -> TRANSFER -> Duna.
3. Confirm the existing Lambert transfer preview still appears.
4. Confirm the new Lambert ejection preview appears.
5. Record/screenshot:
   - Lambert burn UT
   - burn offset
   - P / N / R DV
   - total DV
   - geometry residual
6. Confirm there is no overlap with the node-status block.
7. Repeat with Eve. Eve is especially useful because 14.22.36 showed the
   legacy UT/prograde refinement could not improve that case.
8. CREATE KSP NODE should still create the legacy node, not the Lambert vector.

Do not push until Duna/Eve preview values and layout are reviewed.

NEXT
----
If the real Duna and Eve burn vectors look physically sensible, the next build
should add a separate explicit "CREATE LAMBERT TEST NODE" path or an equivalent
controlled validation mechanism.

That test-node path should remain distinct from normal CREATE authority until
KSP patched conics verifies the Lambert-derived vector against the target.
