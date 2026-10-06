KMC 14.22.46 — Safe Target Periapsis
=========================================

BASELINE
--------
Frozen 14.22.45:
f480b4365f1c0ec1e4d76e871e0a11d71046e7f2

WHY
---
14.22.45 solved the encounter problem:

- Duna predicted encounter -> KSP encounter YES
- Eve predicted encounter -> KSP encounter YES

But the shooting objective drove target miss distance toward zero. In the live
Duna test, KSP closest approach was ~0.7 km from the body center. That proves
the encounter solver works, but it also means the transfer is effectively a
collision-course solution.

MechJeb's interplanetary solver does not stop at "hit the target body." It
supports a target periapsis radius (peR) and shapes the target-relative
hyperbola accordingly.

14.22.46 adds that layer.

TARGET-SOI ENTRY
----------------
Once the parent-frame shooting trajectory is inside the destination SOI, KMC
now solves the actual target-SOI entry time.

At the SOI boundary it builds the target-relative state:

  r_rel = spacecraft_parent_position - target_parent_position
  v_rel = spacecraft_parent_velocity - target_parent_velocity

Then it uses the live target grav parameter to calculate the osculating
target-relative periapsis radius.

TWO-STAGE SHOOTING OBJECTIVE
----------------------------
Stage 1:
  Establish an actual target-SOI encounter.

  Until both candidates encounter the target, lower parent-frame miss distance
  remains the objective.

Stage 2:
  Once an encounter exists, stop aiming at the body center.

  Minimize:
    | predicted target periapsis radius - desired target periapsis radius |

The existing Lambert mismatch and total DV remain tie breakers.

GENERIC DEFAULT PERIAPSIS
-------------------------
14.22.46 does not hardcode Duna, Eve, atmospheres, or stock-system data.

The initial generic target radius is derived only from live target radius and
SOI:

  desired Pe radius = max(2 * body radius, 1% of target SOI)
  capped at 25% of target SOI

This produces a safe standoff flyby/capture setup without assuming atmosphere
height.

A later UI build can expose a user-entered target periapsis altitude. This
milestone establishes and validates the underlying target-relative geometry.

MAP
---
TARGET-SOI SHOOTING now additionally shows:

  TARGET PE R <desired radius>
  PRED PE R   <predicted radius>
  PRED PE ALT <predicted altitude>
  SAFE FLYBY | COLLISION RISK

PRODUCTION
----------
CREATE KSP NODE still uses the final target-shooting corrected burn.

No KSP plugin change.
No shared protocol change.

AUTOMATED TESTS
---------------
Expected:

NavigationTests: 25 passed, 0 failed
Transfer search: 3 passed, 0 failed
Lambert ejection: 4 passed, 0 failed
Parking-aware search: 3 passed, 0 failed
State propagation: 4 passed, 0 failed

LIVE ACCEPTANCE
---------------
Duna is enough for the first acceptance test.

Before CREATE:
- PREDICT ENCOUNTER
- TARGET PE R should be well outside Duna's physical radius
- PRED PE R should converge near TARGET PE R
- PRED PE ALT should be positive
- SAFE FLYBY

Then click normal CREATE KSP NODE.

KSP acceptance:
- NODE VERIFIED
- KSP NODE MATCHES UPLINKED PLAN
- ENCOUNTER YES
- closest approach should no longer be near zero / center impact.

Do not push until the live Duna test confirms both encounter and non-collision
arrival geometry.
