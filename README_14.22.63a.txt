KMC 14.22.63a — True B-Plane Regression Update

TEST-ONLY overlay on top of 14.22.63.

Changes:
- Updates Tools/NavigationTests/Program.cs TargetBPlaneRegression.
- The expected actual B magnitude now uses the hyperbolic invariant B = |h| / v_inf.
- For the existing fixture: v_inf = sqrt(0.8), |h| = 4.358898943540674.
- Production KMC.Engine/KMC.MissionControl code is unchanged.

After overlay:
  .\Tools\NavigationTests\Run-Tests.ps1

Expected: 35 passed, 0 failed.
