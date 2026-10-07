KMC 14.22.63 - True Hyperbolic B-Plane at Finite SOI

Purpose
-------
Correct the Stage-2 terminal geometry without changing the proven Stage-1
split-transfer solver or the 14.22.62 robust terminal Jacobian/trust region.

Changes
-------
* Reconstruct the target-relative osculating hyperbola from the finite-SOI r/v.
* Recover v-infinity from specific orbital energy.
* Recover the eccentricity/periapsis basis and true incoming asymptote S-hat.
* Compute the actual B vector from B = (S-hat x h) / v-infinity.
* Use |B| = |h| / v-infinity as a numerical consistency check.
* Build B.T/B.R in the plane perpendicular to the true incoming asymptote.
* Requested-periapsis B magnitude continues to use live target mu and the
  trajectory's v-infinity; no body-specific constants or destination branches.
* Shadow mode only. No KSP node authority.

Expected validation
-------------------
Re-run the six-body matrix. A near-zero B magnitude error should now track a
near-zero propagated periapsis error, including strong finite-SOI bending cases
such as Eve and Jool.
