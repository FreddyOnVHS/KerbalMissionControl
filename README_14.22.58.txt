KMC 14.22.58 - Coupled Finite-SOI Nonlinear Optimizer / Stage 1
================================================================

PURPOSE
-------
Replace the 14.22.57 two-evaluation foundation with the first real coupled
finite-SOI feasibility optimizer. Shadow mode only; no node authority.

WHAT CHANGED
------------
- Solves burn UT + prograde/normal/radial departure impulse together.
- Uses a 9-component normalized residual vector:
    * source interface position XYZ
    * source interface velocity XYZ
    * target parent-frame position miss XYZ
- Builds a central finite-difference Jacobian.
- Uses damped Gauss-Newton normal equations with a bounded trust region.
- Uses explicit feasibility tolerances; FEAS PASS can no longer mean merely
  that an initialization stage completed.
- Requires source SOI exit outbound and target approach inbound.
- Departure delta-v remains the objective when constraint scores tie.
- Only after finite-SOI feasibility is genuinely green may the existing
  B-plane bootstrap be considered as a terminal seed.
- Does not modify TargetSoiShootingSolver and does not supply maneuver/node
  authority.

EXPECTED DUNA DIAGNOSTICS
-------------------------
The coupled shadow block should now show substantially more than 2 evaluations.
The important progression is SOURCE IF and TARGET IF falling together. FEAS
must remain FAIL until the explicit source continuity tolerances and a real
inbound target-SOI encounter are satisfied.

TEST
----
Run from the repository root:

  .\Tools\NavigationTests\Run-Tests.ps1

Then open Kerbin -> Duna in the Transfer Planner and send the complete
COUPLED FINITE-SOI OPTIMIZER / SHADOW block.

DO NOT PUSH THIS EXPERIMENTAL BUILD.
GitHub master remains the frozen 14.22.46 baseline until promotion is explicit.
