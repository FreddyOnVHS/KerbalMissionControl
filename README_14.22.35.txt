KMC 14.22.35 — Lambert Transfer Search Foundation
=================================================

BASELINE
--------
Verified master HEAD before build:
f030050b948e2969281ad3f744c923ede7288b86

PURPOSE
-------
Add the first generic KMC-side transfer-search layer on top of the accepted
Kepler propagation and zero-revolution Lambert solver.

This build is deliberately NOT wired into MAP yet.

The current live navigation path remains:
Hohmann estimate
  -> parking-orbit ejection estimate
  -> KSP node
  -> KSP patched-conic verification/refinement

NEW ENGINE LAYER
----------------
KMC.Engine/Navigation now adds:

TransferSearchRequest
- generic origin body
- generic destination body
- authoritative parent gravitational parameter
- earliest/latest departure UT
- minimum/maximum time of flight
- departure sample count
- time-of-flight sample count
- enable short-way search
- enable long-way search

LambertTransferSearch
For each departure-UT x time-of-flight sample:
1. Propagate the origin body at departure UT.
2. Propagate the destination body at arrival UT.
3. Solve enabled zero-revolution Lambert path(s).
4. Compare Lambert departure velocity with origin-body velocity.
5. Compare Lambert arrival velocity with destination-body velocity.
6. Rank by:
     departure excess speed + arrival excess speed
7. Return the best deterministic candidate.

TransferSearchSolution
Contains:
- origin/destination/parent identity
- departure UT
- arrival UT
- time of flight
- selected Lambert path
- propagated origin/destination states
- Lambert solution
- departure excess velocity/vector magnitude
- arrival excess velocity/vector magnitude
- combined excess-speed score

IMPORTANT TERMINOLOGY
---------------------
The departure and arrival values in this build are parent-frame excess
velocities (v-infinity-style boundary mismatch).

They are NOT yet:
- parking-orbit ejection burn delta-v
- capture burn delta-v
- total mission delta-v

Those require local-body parking/capture geometry and patched-conic routing,
which belong to later milestones.

SCOPE / SAFETY
--------------
The search:
- uses no stock body constants
- accepts parent mu explicitly
- requires origin/destination to share the same parent
- rejects contradictory reference-frame names
- rejects invalid/nonfinite search ranges
- limits each grid axis to 256 samples
- skips individual propagation/Lambert samples that do not produce a valid
  supported solution
- uses deterministic tie-breaking:
  1. lower combined excess-speed score
  2. earlier departure
  3. shorter flight time
  4. short-way path

UNCHANGED RUNTIME
-----------------
14.22.35 does NOT change:
- MAP display
- destination selection
- active Hohmann window calculation
- active parking ejection calculation
- maneuver packets
- KSP plugin
- KSP patched-conic assessment
- REFINE KSP NODE
- encounter behavior

No KSP Plugin DLL replacement should be required.

TESTING
-------
The existing Tools/NavigationTests/Run-Tests.ps1 now:

1. Builds/runs the existing NavigationTests suite.
   Expected baseline after 14.22.34:
   16 passed, 0 failed

2. Builds/runs TransferSearchTests.
   New groups:
   - exact zero-excess circular rendezvous
   - inclined 3D zero-excess rendezvous
   - invalid ranges/hierarchy/frame rejection

The exact circular test is deliberately synthetic:
origin and destination share one circular osculating trajectory, so a
quarter-period short-way Lambert arc must reproduce the body's natural
two-body velocity at both endpoints. Therefore departure and arrival excess
speeds should both be approximately zero.

FILES
-----
KMC.Engine/Navigation/TransferSearchRequest.cs
KMC.Engine/Navigation/TransferSearchSolution.cs
KMC.Engine/Navigation/LambertTransferSearch.cs
KMC.Engine/KMC.Engine.csproj
Tools/NavigationTests/TransferSearchTests.csproj
Tools/NavigationTests/TransferSearchProgram.cs
Tools/NavigationTests/Run-Tests.ps1
Tools/OrbitMap/tests/test_14_22_35_lambert_transfer_search.py
README_14.22.35.txt

NEXT
----
After compile/test acceptance, the next milestone should connect this search
layer to real MAP celestial telemetry as a comparison/preview path.

The first MAP integration should display Lambert search output beside the
existing Hohmann solution without immediately replacing node creation.

That lets KMC compare:
- legacy Hohmann candidate
- Lambert candidate
- KSP-authoritative result

before Lambert becomes the maneuver source.
