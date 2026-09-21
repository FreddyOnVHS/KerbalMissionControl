KMC 14.22.15 — KSP BCI Frame Calibration Fix

Purpose
-------
Fix the remaining projected Mun encounter orientation mismatch without
guessing signs, anomalies, or hyperbolic branches.

Reference behavior
------------------
Established KSP tooling treats Orbit.getRelativePositionAtUT(ut).xzy and
Orbit.getOrbitalVelocityAtUT(ut).xzy as the authoritative body-centered
inertial (BCI) position/velocity in KSP world axes.

Implementation
--------------
- KSP remains authoritative for every projected patch sample at each UT.
- Build one packet-wide KSP->KMC orthonormal frame transform from the
  active vessel's current KSP BCI position/velocity and the already-proven
  KMC canonical current position/tangent.
- For each projected patch sample:
    * obtain KSP local patch position directly,
    * obtain the encountered child body's KSP future position directly,
    * combine them in KSP BCI space,
    * apply the same calibrated transform once.
- No future true-anomaly reconstruction.
- No SOI branch guessing.
- No per-patch handedness flip.
- No packet/protocol change.

Build
-----
Rebuild KMC.Plugin in Release.
Redeploy KMC.Plugin.dll to GameData/KMC/Plugins.

Runtime acceptance
------------------
Use the same Kerbin -> Mun encounter.
Compare KSP and KMC:
- projected Mun must be on the same side of Kerbin,
- transfer must approach that future Mun from the same side,
- SOI crossing and Mun encounter arc must remain continuous,
- RX must keep increasing.

KSP Plugin DLL Required? YES
Mission Control rebuild required? NO
KMC.shared.dll change required? NO
