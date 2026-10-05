# Navigation foundation verification — 2026-10-05

Baseline: `38f30056eb989001859bdacdf135d78f385eda97` (14.22.32).
Branch: `codex/navigation-foundation`.

## Build

Visual Studio 18 Community MSBuild, .NET Framework 4.8, Release:

`MSBuild.exe KMC.slnx /t:Rebuild /p:Configuration=Release /v:minimal /nologo`

Exit 0; KMC.shared, KMC.Engine, KMC.MissionControl and KMC.Plugin produced successfully. No warnings/errors reported. Plugin, shared protocol and renderer source diffs against baseline are empty.

## Behavioral checks

`Tools/NavigationTests/Run-Tests.ps1`: **13 groups passed, 0 failed**.

- Circular quarter period, backward UT and multiple periods.
- Elliptical periapsis/apoapsis positions and velocities.
- Hyperbolic analytic H=1 position/velocity, incoming/outgoing symmetry.
- Inclined, retrograde and rotated frames.
- Energy/angular-momentum invariants for e = 0, .7, .99, .999999, 1.000001, 1.5, 4, at five forward/backward times each.
- Invalid/nonfinite elements, time and gravitational parameter; exact parabolic rejection.
- Position comparison with the existing renderer for four orbit shapes at four times.
- Eight frozen baseline transfer/ejection fixtures, all result fields compared.
- Existing parking-orbit limits, missing orbit/period and period fallback.
- Nonfinite planner data cannot yield an uploadable candidate.
- Real MAP draws both tabs and constructs the baseline maneuver, then clears it for invalid telemetry; no uplink is sent.
- Negative elliptic axes rejected for parking, origin and destination.
- Explicit reference-frame contradictions rejected at engine and packet boundaries.

Initial missing APIs failed the test build before implementation. The two review regression groups were observed failing before their guards were added (11 passed, 2 failed), then passing afterward (13 passed, 0 failed).

## Existing repository tests

`python -m pytest -q` before production edits: **359 passed, 10 skipped, 31 failed**.

`python -m pytest -q -p no:cacheprovider` after implementation and again after review fixes: **359 passed, 10 skipped, 31 failed**. The failure identity set is identical to baseline: **zero added or resolved failures**. Pytest's raw exit code remains 1, and is not presented as a green suite. Each failure is listed in `Tools/NavigationTests/Fixtures/baseline-python-failures.txt`. Many tests are historical source-shape assertions for superseded MAP implementations. Their wider cleanup is outside this change.

Two math source-inspection test files were adjusted to inspect the new Engine/adapter locations. Their assertions and pre-existing failure were retained.

## Independent review

Read-only review of the new engine, adapter, UI extraction and tests found two actionable invalid-input issues: negative elliptic axes and explicit frame contradictions. Both were reproduced with failing tests, fixed, and confirmed resolved by the reviewer, who independently reran the 13 passing groups. No remaining actionable review findings.

## Limits of this evidence

- No live KSP runtime test or actual encounter verification performed for this build.
- Renderer comparison is frame compatibility evidence, not independent KSP validation.
- No claim of precision certification at floating-point limits arbitrarily close to e=1.
- Legacy planar/Newton approximation is deliberately preserved, including its existing limitations.
- New propagation accepts elements and produces state vectors; general state-to-state propagation and exact parabolic handling are not implemented.
- Lambert solving, full-vector ejection, hierarchy routing and arrival/capture targeting are later milestones.

See the build README for the pending live-game checklist.
