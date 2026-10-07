KMC 14.22.62 - Robust Terminal Jacobian / Trust-Region Recovery

Purpose
-------
Keep the proven 14.22.59 Stage-1 free-split heliocentric feasibility solve and
14.22.61 general B-plane geometry unchanged. Improve only Stage-2 numerical
robustness for hard terminal geometries such as Moho and Eeloo.

Changes
-------
- Adaptive terminal finite-difference Jacobian with progressively smaller
  perturbations when an SOI-boundary perturbation is invalid.
- Central differences when both sides are valid; one-sided derivatives when
  only one side remains valid.
- An unavailable derivative column no longer aborts the entire terminal
  Jacobian. Damped normal equations may proceed with the remaining independent
  columns when at least four are usable.
- Rejected/invalid terminal trial steps are retried at successively half-size
  steps before shrinking the trust region.
- Trust-region and damping recovery are preserved; Stage-1 feasibility remains
  protected.
- Live shadow diagnostics now report JAC n/8, rejected-step count, and terminal
  trust radius.
- No destination-specific logic.
- NO NODE AUTHORITY. Production maneuver generation remains unchanged.

Validation in build environment
-------------------------------
Focused 14.22.57 through 14.22.62 structural regression suite: 27/27 PASS.
A Windows/.NET Framework compile and KSP runtime test are still required.

Recommended runtime order
-------------------------
1. Run .\\Tools\\NavigationTests\\Run-Tests.ps1
2. Test Kerbin -> Moho and capture the coupled shadow block.
3. Test Kerbin -> Eeloo and capture the coupled shadow block.
4. If both converge, rerun Dres, Duna, Eve, and Jool for regression coverage.

Do not push until the complete validation matrix is green.
