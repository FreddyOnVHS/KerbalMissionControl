# Navigation Foundation Implementation Plan

> For agentic workers: use superpowers:executing-plans task by task; steps are tracked below.

**Goal:** implement approved steps 1–4 and deliver a tested build preserving MAP's approximate transfer workflow.
**Architecture:** generic celestial mechanics and planner in Engine; packet adapter in MissionControl; UI consumes results. Preserve legacy numerical formulas independently of the new 3D propagation API.
**Tech Stack:** C#, .NET Framework 4.8, MSBuild, dependency-free C# behavioral test executable, existing Python tests.
**Spec:** ../specs/2026-10-05-navigation-foundation-design.md

## Global constraints
Baseline 38f30056; no plugin/protocol/rendering behavior changes; SI units; explicit coordinate convention; no body-specific logic; no Lambert or hierarchy solver in this milestone.

## Review focus
- Nonfinite times/elements must not produce an uploadable solution.
- Missing origin orbit/period must return failure without throwing.
- Near-parabolic and high-eccentricity inputs must converge or fail explicitly.
- Inward transfer signs and nearest burn-pass choice must match baseline.
- Adapter must preserve epoch, radians/degrees and reference-body identity.

## Task 1: baseline and test harness
- [x] Capture all legacy planner result fields for deterministic inward/outward fixtures before edits.
- [x] Add Tools/NavigationTests dependency-free console project and a test runner; missing foundation APIs fail compilation initially.
- [x] Record baseline build and Python regression results.

## Task 2: generic elements and propagation
Files: KMC.Engine/CelestialMechanics/{Vector3d,StateVector,OrbitalElements,CelestialBodyState,KeplerPropagator}.cs; Engine csproj; Tools/NavigationTests/Program.cs.
Interface: `KeplerPropagator.TryPropagate(OrbitalElements orbit, double mu, double ut, out StateVector state)`; vector operations and immutable time-tagged state.
- [x] Test circular quarter period (r=(0,1,0), v=(-1,0,0) for mu=a=1); ellipse a=2,e=.5 at apsides; known hyperbolic H=1 at mu=1,a=-2,e=1.5; orientation, conserved energy/angular momentum, backward UT and invalid inputs.
- [x] Run missing-API failures, implement bracketed Kepler solves and 3D position/velocity rotation, then pass tests.

## Task 3: extract planner and adapt MAP
Files: Engine/Navigation/{HohmannTransferPlanner,TransferWindowSolution,ParkingOrbitEjectionSolution}.cs; MissionControl/Navigation/OrbitMapNavigationAdapter.cs; MapPage.cs; project includes.
Interfaces: `TryCalculateTransferWindow(CelestialBodyState origin, CelestialBodyState destination, double ut, out TransferWindowSolution solution)` and `TryCalculateParkingOrbitEjection(OrbitalElements parkingOrbit, double radius, CelestialBodyState originBody, TransferWindowSolution transfer, out ParkingOrbitEjectionSolution solution)`.
- [x] Add baseline-fixture parity and safe-rejection tests before extraction.
- [x] Move formulas/results, preserving supported valid-input behavior; adapt data once at the UI boundary.
- [x] Relocate source-inspection tests that assumed math lived in MapPage; retain their original assertions against the appropriate component.
- [x] Run behavioral suite and compare Python results to baseline.

## Task 4: review and package
- [x] Build Release for all projects, run behavioral and Python suites; inspect diff and get independent whole-change review.
- [x] Address actionable findings with regression coverage.
- [x] Produce MissionControl binaries and changed-source overlay with README, verification record and in-game acceptance checklist. No live game installation.

## Execution ledger
- User explicitly approved implementing steps 1–4 in one pass after the scope proposal; proceed within that authorization without repeated design permission gates.
- Existing nested KMC checkout clean but stale; fetched origin/master and created codex/navigation-foundation at 38f30056. Native worktree tool targets the unrelated outer Unity repository, so use the clean KMC feature branch in place.
- Baseline: all four projects build Release, exit 0. Python runtime found; pytest initially missing.
- Interfaces checked: element/body models feed planner, adapter converts shared packet types, UI keeps transport responsibilities.

- Tasks 1–3 complete: baseline fixtures captured before production edits; numerical and integration tests pass; MAP uses the new adapter/engine.
- Review fixes: malformed negative elliptical axes and explicit frame-name conflicts now reject safely; blank redundant names retained for compatibility. Regression tests observed red then green.
- Task 4 verification: all four Release projects rebuilt; 13 behavior groups pass. Full Python suite remains 359 pass / 10 skip / 31 identical pre-existing failures, no new failures. Independent reviewer confirmed fixes resolved.
- Ruling: existing historical Python failures do not block delivery of the requested development build; report them explicitly and do not claim all-green repository or runtime acceptance. No merge/push or live installation.
