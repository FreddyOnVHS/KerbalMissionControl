KMC 14.22.66 — Interplanetary Solver Consolidation
Baseline: 14.22.65a / c7d67808775e625be4e14563f93283998194e4c0

Purpose
-------
Consolidate the now-proven coupled finite-SOI interplanetary solver after the
six-body production-node validation matrix. This build does not change solver
math, target geometry, maneuver-authority acceptance thresholds, KSP terminal
periapsis correction, or fallback behavior.

Changes
-------
- Introduces CoupledFiniteSoiResult as the production result type.
- Introduces CoupledFiniteSoiOptimizer.TrySolve as the production solver API.
- Active MAP planner code now uses production names:
    _coupledFiniteSoiResult
    MapCoupledFiniteSoiAdapter
    CoupledFiniteSoiOptimizer.TrySolve
- Retains CoupledFiniteSoiShadowResult / TrySolveShadow / shadow adapter as
  compatibility wrappers for older tests and callers. The historical source
  filename is intentionally retained to avoid unnecessary project-file churn.
- Replaces stale SHADOW UI wording with PRODUCTION or CANDIDATE.
- Adds TARGET PE / PRED PE diagnostics before node creation.
- Makes maneuver source explicit as AUTH COUPLED or AUTH FALLBACK.
- Keeps production authority order unchanged:
    coupled -> Lambert/target-shooting fallback -> legacy fallback.
- Adds a compiled NavigationTests regression for the production API transition.
- Updates historical focused source regressions whose old assertions required
  permanent shadow/no-authority behavior.

Not changed
-----------
- Coupled optimizer equations or tolerances.
- Free split heliocentric state.
- True hyperbolic B-plane geometry.
- Robust terminal Jacobian/trust-region logic.
- KSP-authoritative terminal periapsis correction.
- KMC.Plugin source or DLL.
- Destination-specific logic: none added.

Expected validation
-------------------
Run:
  .\Tools\NavigationTests\Run-Tests.ps1

NavigationTests should report 37 passed, 0 failed. The existing transfer,
Lambert-ejection, parking-aware, and state-propagation suites should remain green.

Because no plugin source changes in 14.22.66, KMC.Plugin.dll does not need to be
replaced for this build.
