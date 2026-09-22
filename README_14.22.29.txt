KMC 14.22.29 — Parking-Orbit Ejection Solution
================================================

BASELINE
--------
GitHub master verified before build:
467866f0ac35adf04517597a1a651e585e0f733d

PURPOSE
-------
Convert the 14.22.28 same-parent Hohmann window into an approximate spacecraft
ejection solution from the active vessel's current parking orbit.

SCOPE
-----
This is still a planning-only build. It does NOT create a KSP maneuver node.

The ejection solution is enabled only when:
- the interplanetary transfer window is a supported SAME PARENT solution
- the vessel's current parking orbit is elliptic and near-circular (e <= 0.05)
- the parking orbit is approximately prograde/coplanar (inclination <= 10 deg)

CALCULATION
-----------
14.22.29 treats the absolute parent-frame Hohmann delta-v as required
hyperbolic excess speed Vinf relative to the origin body.

Using the origin body's KSP-transmitted gravitational parameter and the
active vessel parking-orbit radius:

  parking speed = sqrt(mu / r)

  hyperbolic periapsis speed =
      sqrt(Vinf^2 + 2*mu/r)

  vessel ejection DV =
      hyperbolic periapsis speed - parking speed

It also computes the hyperbolic asymptote angle:

  e_h = 1 + r*Vinf^2/mu
  nu_inf = acos(-1/e_h)

For the current prograde/coplanar approximation the target burn radius is
nu_inf behind the desired Vinf direction. The planner propagates the vessel
orbit near the ideal transfer departure UT and selects the nearest orbital
pass through that approximate burn-radius direction.

DISPLAY
-------
Adds:
- parking altitude
- required Vinf and parent-tangent direction
- parking-orbit speed
- hyperbolic ejection speed at periapsis
- spacecraft burn delta-v
- asymptote/burn-radius geometry
- candidate burn UT nearest the ideal transfer window
- signed offset between candidate burn pass and ideal window

LIMITATIONS
-----------
This is not yet a Lambert solver or an exact KSP maneuver.

It does NOT yet:
- account for arbitrary parking-orbit inclination / LAN geometry
- optimize eccentric parking orbits
- compensate the transfer window after shifting to the nearest parking pass
- create a maneuver node
- validate the resulting KSP patched conics
- support hierarchy-change routes

FILES IN THIS BUILD
-------------------
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_29_parking_orbit_ejection.py
README_14.22.29.txt

BUILD / DEPLOY
--------------
1. Unzip over repository root.
2. Rebuild KMC.MissionControl.
3. No KSP plugin DLL replacement is required.

RUNTIME ACCEPTANCE
------------------
Use a near-circular equatorial Kerbin parking orbit.

1. Select Duna.
2. Confirm 14.22.28 HOHMANN WINDOW values remain present.
3. Confirm PARKING EJECTION block appears.
4. Verify PARK ALT agrees with the active vessel parking altitude.
5. Verify VINF equals the absolute PARENT DV.
6. Verify VESSEL DV is greater than Vinf but physically reasonable for Kerbin
   escape from the displayed parking altitude.
7. Verify BURN RADIUS and BURN UT are finite.
8. Select Eve and confirm Vinf direction changes to -PARENT TANGENT.
9. Mun/Bop hierarchy-change routes must remain unsupported.
10. LOCAL map behavior must remain unchanged.

Do not report runtime PASS until the C# build and KSP/KMC runtime checks pass.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
