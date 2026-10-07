KMC 14.22.47 — General 3D Departure Bootstrap
================================================

BASELINE
--------
Frozen 14.22.46:
6f56ba0df518c062c25b349c0d78fd72ec1e50e2

BUG FOUND
---------
A live Kerbin -> Dres test produced:

  LAMBERT EJECTION / PRODUCTION
  P/N/R approximately +577.5 / -3034.8 / 0.0 m/s
  no TARGET-SOI SHOOTING block
  KSP ENCOUNTER NO

This proved the target-SOI shooting solver itself was not failing. It was never
being reached.

The parking-aware finite-SOI coarse stage rejected all candidates, so KMC fell
back to the old plain Lambert-ejection preview and ultimately legacy behavior.

FIX
---
A valid plain Lambert + full 3D local ejection is now allowed to bootstrap the
target-SOI shooting solver directly when parking-aware finite-SOI ranking fails.

The modern solver path is now:

  parking-aware candidate available
      -> finite-SOI correction
      -> target-SOI shooting

  otherwise, valid coarse Lambert + 3D ejection available
      -> DIRECT 3D TARGET-SOI SHOOTING

  only if neither exists
      -> legacy fallback

This prevents high-inclination/eccentric targets from silently dropping out of
the modern encounter solver merely because the intermediate finite-SOI ranking
stage could not score them.

WIDER 3D SHOOTING AUTHORITY
---------------------------
Direct bootstrap can begin with a pathological analytic seed (for example a
very large normal component).

14.22.47 therefore widens the generic target-shooting correction domain:

  component half-range:
    from max(100 m/s, 0.40 * seed DV)
    to   max(300 m/s, 1.50 * seed DV)

  initial DV step cap:
    from 40 m/s to 80 m/s

  max iterations:
    from 72 to 96

This lets the target solver move a poor analytic P/N/R bootstrap across a much
larger portion of 3D burn space, including through zero normal if necessary.

No planet names or stock constants exist in runtime navigation code.

REGRESSION COVERAGE
-------------------
The automated suite adds four representative same-parent geometry classes:

- inner / high inclination (Moho-like)
- eccentric inclined outer (Dres-like)
- giant planet / huge SOI (Jool-like)
- distant eccentric outer (Eeloo-like)

These are test fixtures only. Runtime remains body-agnostic and uses live KSP
telemetry.

LIVE ACCEPTANCE
---------------
Dres is the primary regression.

Before CREATE:
- TARGET-SOI SHOOTING or TARGET-SOI SHOOTING / 3D BOOTSTRAP must appear
- PREDICT ENCOUNTER
- safe target periapsis should appear if target μ/radius telemetry is present

After CREATE:
- NODE VERIFIED
- KSP NODE MATCHES UPLINKED PLAN
- ENCOUNTER YES

After Dres passes, spot-check Moho, Jool, and Eeloo. We do not need to create a
new build between them unless one exposes a separate failure class.

KSP Plugin DLL replacement: NOT REQUIRED.
Shared protocol change: NONE.
Mission Control + Engine rebuild: REQUIRED.

Do not push until Dres passes live.
