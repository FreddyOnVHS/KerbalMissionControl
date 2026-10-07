KMC 14.22.55b — B-Plane Encounter Incumbent Guard
====================================================

WHY
---
14.22.55 made the B-plane bootstrap mandatory as the terminal optimizer's
starting state. That is directionally correct, but the implementation could
discard a valid/improving encounter and finish with a worse terminal result.

The same-parent planetary regression matrix caught this on its Dres-like case.

FIX
---
- Save the pre-B-plane encounter as the terminal incumbent.
- A B-plane bootstrap becomes the working terminal initialization only if its
  finite-SOI propagated assessment still predicts an encounter.
- Run terminal periapsis refinement normally.
- After refinement, compare the final terminal result against the saved
  incumbent under the terminal objective.
- If the B-plane path did not actually improve the solution, restore the known
  feasible encounter.

This preserves the MechJeb concept:
B-plane geometry is an optimizer initialization, not permission to throw away
an already-feasible trajectory.

EXPECTED
--------
NavigationTests: 34 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 5 passed, 0 failed
Parking-aware search: 4 passed, 0 failed
State propagation: 4 passed, 0 failed

New expected line:
PASS target terminal solve preserves feasible encounter incumbent

If clean, resume Duna pre-node regression.
Do not push yet.
