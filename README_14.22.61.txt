KMC 14.22.61 - General B-Plane Terminal Geometry (shadow only)

Purpose
-------
Preserve the proven 14.22.59/60 finite-SOI split-transfer feasibility solver
and improve only Stage 2 terminal geometry.

Changes
-------
- Stage 1 split-transfer formulation is unchanged.
- Stage 2 replaces the single scalar periapsis residual with a two-component
  radial B-plane residual (B.T / B.R) aimed at the nearest point on the
  desired-B circle. This improves conditioning without imposing an arbitrary
  B-plane clock angle.
- Desired B magnitude is derived from live target entry state, target mu,
  target SOI radius, and requested periapsis. No destination constants.
- Periapsis remains the physical terminal acceptance check.
- Terminal finite differences now support one-sided derivatives when one
  perturbation temporarily crosses the target-SOI feasibility boundary.
- Shadow diagnostics add B MAG ERR, B.T, and B.R.
- Still NO NODE AUTHORITY. Production TargetSoiShootingSolver remains intact.

Validation target
-----------------
Run the same matrix without destination-specific tuning:
Duna, Eve, Dres, Moho, Jool, Eeloo.
Do not promote based on one destination.
