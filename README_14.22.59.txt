KMC 14.22.59 - FREE SPLIT HELIOCENTRIC TRANSFER STATE
=====================================================

Purpose
-------
Introduce the MechJeb-style split heliocentric multiple-shooting state into
KMC's coupled finite-SOI optimizer, still in shadow mode with NO NODE AUTHORITY.

Optimizer variables
-------------------
1. Parking-orbit burn UT
2. Prograde DV
3. Normal DV
4. Radial DV
5. Free heliocentric midpoint X
6. Free heliocentric midpoint Y
7. Free heliocentric midpoint Z
8. Arrival UT

The free midpoint is NOT a maneuver. It is a numerical multiple-shooting state
used to split the parent-frame transfer into two Lambert legs and avoid forcing
one near-180-degree Lambert arc to carry the entire finite-SOI boundary-value
problem.

Stage-1 residuals
-----------------
- source-SOI velocity continuity (XYZ)
- split midpoint velocity continuity (XYZ)
- physical target-position miss at optimized arrival UT (XYZ)

Stage-1 feasibility requires:
- source exit outbound
- source velocity continuity within tolerance
- split midpoint velocity continuity within tolerance
- physical target encounter
- target SOI entry inbound

Target B-plane/periapsis terminal conditions remain disabled in 14.22.59.
They are intentionally deferred until split finite-SOI feasibility is green.

Authority
---------
Shadow only. The solver does not replace _lambertEjectionPreview and does not
supply CREATE KSP NODE.

Testing
-------
Run the normal Windows navigation tests, then test Kerbin -> Duna first. The
shadow diagnostic line now includes SPLIT VERR. Success for this build means
source VERR and SPLIT VERR become small while TARGET IF reaches 0 / target IN.
Do not push until the fixed validation matrix is green.
