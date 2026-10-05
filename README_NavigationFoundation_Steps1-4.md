# KMC Navigation Foundation — Steps 1–4

Development build based on **14.22.32**, commit `38f30056eb989001859bdacdf135d78f385eda97`.
This name is deliberately separate from the earlier 14.22.33 Duna-targeting package.

## Running the packaged build

1. Extract the ZIP into a new folder and close any other Mission Control instance.
2. Run `MissionControl/KMC.MissionControl.exe`. Keep its two DLLs beside the executable.
3. Use your existing 14.22.32 KSP plugin and the normal connection setup.

**KSP plugin replacement: not required.** Plugin and shared-protocol sources are unchanged.
The packaged shared DLL is supplied alongside Mission Control for its normal runtime dependency.
This package does not install into KSP, modify saves, execute burns, or push changes to GitHub.

## Implemented

- Generic double-precision vectors, time-tagged position/velocity states, orbital elements and celestial body data in `KMC.Engine/CelestialMechanics`.
- Kepler propagation from elliptic/hyperbolic elements to 3D position and velocity. Explicit units/frame convention, bracketed solves, safe failure for invalid data and unsupported exact parabolas.
- Existing Hohmann-window and near-circular parking-ejection calculations moved from `MapPage` into `KMC.Engine/Navigation`.
- An explicit Mission Control telemetry adapter. MAP retains its rendering, controls, node creation, submission locks and refinement workflow.
- Malformed/nonfinite geometry and explicit reference-frame contradictions are rejected before producing a candidate.

The MAP planner still uses the **same circular/coplanar approximation**. The new 3D propagator is a tested foundation API; it does not replace the planner's legacy planar-longitude calculation in this build. This preserves baseline transfer values. There is no Lambert solver, general state-to-state propagation, transfer search, full-vector ejection, hierarchy routing, capture burn or new Duna periapsis targeting yet.

## What to install when building from source

`SourceOverlay` contains changed/new files only. Apply it to the exact baseline above, then rebuild Mission Control and Engine. Do not apply the overlay to a different newer checkout without reviewing the changes. `navigation-foundation.patch` is an alternative; check it with `git apply --check` first, then apply it once. Do not apply both methods.

The core source lives in the local feature branch `codex/navigation-foundation`; this build has not been pushed or merged.

## Automated validation

- All four projects compile in Release with .NET Framework 4.8 and the local KSP references.
- `Tools/NavigationTests/Run-Tests.ps1` builds and runs the dependency-free numerical/integration suite. Visual Studio MSBuild and .NET Framework 4.8 developer tools are required; `-MSBuildPath` can override discovery.
- Eight fixtures captured from the unmodified 14.22.32 assembly compare **every transfer/ejection result field**. Both inward/outward routes, varied epochs, phases, orientation and parking geometry are covered.
- Tests cover circular/elliptic/hyperbolic analytical states, inclinations/retrograde frames, backward time, conserved energy/angular momentum, renderer-position compatibility, invalid inputs, and actual offscreen MAP rendering and maneuver construction.
- Existing Python suite: **359 passed, 10 skipped, 31 pre-existing failures**, with the exact same failure identities after the change. See `Tools/NavigationTests/Fixtures/baseline-python-failures.txt` and the included verification record. This is **not** an all-green repository suite.

No live KSP session has been used to validate this build. Rendering into a test bitmap and comparing legacy calculations do not prove a live encounter.

## In-game acceptance checklist

1. Connect a vessel in the same near-circular, low-inclination parking orbit used for the 14.22.32 test.
2. Open MAP / LOCAL. Check vessel/body geometry, pan/zoom and fit controls.
3. Open TRANSFER and select Duna. Confirm the Hohmann-window and parking-ejection sections, departure/burn UT and delta-v remain sensible compared with the baseline at the same simulation UT.
4. Click CREATE KSP NODE once. Confirm the node exists in KSP and its prograde delta-v/UT match MAP; radial and normal remain zero.
5. Confirm the submitted node remains locked against duplicates and KSP assessment/closest approach updates as before.
6. If the initial trajectory misses, explicitly click REFINE KSP NODE and verify the existing refinement path. Confirm ENCOUNTER ACHIEVED locks refinement once KSP reports an encounter.
7. Switch destinations and back; check that controls follow the selected candidate. Confirm unsupported hierarchy-change routes and eccentric/high-inclination parking orbits remain unavailable.

Do not treat these items as passed until checked in KSP. The next milestone is Lambert solving and transfer-window search after this preserved flow is accepted.
