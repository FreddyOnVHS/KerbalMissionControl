# KMC 14.22.8 — KSP-Style Multi-Body System View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend the existing MAP page so the current primary body, its immediate child bodies, child-body orbits, vessel transfer trajectory, and child-SOI encounter patch render together in one KSP-style system view.

**Architecture:** KSP remains authoritative for body hierarchy, body orbital state, maneuver nodes, and patched-conic metadata. The plugin sends compact body metadata plus authoritative parent-frame body positions; Mission Control reconstructs and caches orbit geometry and translates child-referenced encounter patches into the current primary-body frame.

**Tech Stack:** KSP 1.12.5 plugin C#, UDP telemetry, KMC.shared packet model, WinForms/GDI+ Mission Control renderer, existing OrbitMap conic/camera/cache infrastructure, Python structural regression tests.

**Spec:** `docs/superpowers/specs/2026-09-17-kmc-14-22-8-ksp-style-multibody-map-design.md`

## Global Constraints

- Baseline is `master` commit `541075cbc02e8375a5a556380de2d2d8c71929f8`.
- Render only the current primary body plus its immediate child bodies in 14.22.8.
- No textures, terrain, atmosphere, clouds, or starfield.
- Preserve cached geometry; camera-only movement must not rebuild orbit geometry.
- Preserve LIVE / STALE / UNAVAILABLE semantics and existing MAP controls.
- Keep the telemetry collection bounded and reject malformed or duplicate body entries.
- Do not render unresolved cross-SOI patches in the wrong frame.

---

### Task 1: Extend the orbit-map packet with immediate child bodies

**Files:**
- Modify: `KMC.shared/OrbitMapPacket.cs`
- Test: `Tools/OrbitMap/tests/test_14_22_8_multibody_protocol.py`

**Interfaces:**
- Produces: `OrbitMapPacket.Bodies : List<OrbitMapBody>` and `OrbitMapBody` with `Name`, `ParentName`, `RadiusMeters`, `SoiRadiusMeters`, `Orbit`, `PositionX/Y/Z`.

- [ ] Write a failing protocol test requiring body round-trip, bounds enforcement, duplicate-name rejection, and finite-number validation.
- [ ] Run the focused test and verify RED.
- [ ] Add `MaxBodies`, `Bodies`, `OrbitMapBody`, serialization/parsing, bounds validation, and duplicate-name validation while keeping `KMC-ORBITMAP1`.
- [ ] Run the focused protocol test and existing protocol tests; verify GREEN.
- [ ] Commit the task.

### Task 2: Publish primary + immediate child-body telemetry from KSP

**Files:**
- Modify: `KMC.Plugin/OrbitMapTelemetrySender.cs`
- Test: `Tools/OrbitMap/tests/test_14_22_8_body_sender.py`

**Interfaces:**
- Consumes: `OrbitMapPacket.Bodies` and `OrbitMapBody` from Task 1.
- Produces: primary-body entry plus immediate children (`child.referenceBody == currentPrimary`) with parent-frame orbital elements and current parent-frame canonical positions.

- [ ] Write a failing sender test requiring `BuildBodies`, bounded child enumeration, parent names, and canonical positions.
- [ ] Run the focused test and verify RED.
- [ ] Add `BuildBodies(vessel, packet, ut)` and call it before packet serialization.
- [ ] Include the current primary as a root entry and immediate children from `FlightGlobals.Bodies`; use the existing canonical orbital transform for body positions.
- [ ] Run sender tests and OrbitMap regression tests; verify GREEN.
- [ ] Commit the task.

### Task 3: Add reusable primary-frame body/patch transforms

**Files:**
- Modify: `KMC.MissionControl/Rendering/OrbitMap/OrbitMapConicSampler.cs`
- Add: `KMC.MissionControl/Rendering/OrbitMap/OrbitMapSystemTransform.cs`
- Modify: `KMC.MissionControl/KMC.MissionControl.csproj`
- Test: `Tools/OrbitMap/tests/test_14_22_8_system_transform.py`

**Interfaces:**
- Produces: body-position-at-UT calculation from `OrbitMapBody.Orbit` and `Translate(points, offset)` for child-local patches.

- [ ] Write failing tests for child-body position-at-epoch, translation, and child-local patch translation.
- [ ] Run focused transform tests and verify RED.
- [ ] Implement `OrbitMapSystemTransform` using the same conic math conventions as `OrbitMapConicSampler`.
- [ ] Add the new source file to the Mission Control project.
- [ ] Run transform/conic tests and verify GREEN.
- [ ] Commit the task.

### Task 4: Extend scene cache for child bodies and cross-SOI patches

**Files:**
- Modify: `KMC.MissionControl/Rendering/OrbitMap/OrbitMapSceneCache.cs`
- Test: `Tools/OrbitMap/tests/test_14_22_8_scene_cache.py`

**Interfaces:**
- Produces: `OrbitMapSceneBody` entries containing body metadata, primary-frame center position, and cached orbit points.
- Produces: translated patch arrays for patches referenced to recognized immediate child bodies.

- [ ] Write failing tests requiring child body scene entries, cached child orbits, recognized child-patch translation, and safe omission of unresolved child patches.
- [ ] Run focused scene-cache tests and verify RED.
- [ ] Add child-body scene model and populate it from packet bodies.
- [ ] Stop filtering all non-primary patches; resolve child body by name, sample locally, translate by child center at patch sample epoch, and omit only unresolved patches.
- [ ] Include body metadata/orbits in trajectory fingerprint so geometry rebuilds only when meaningful data changes.
- [ ] Run scene-cache tests and full OrbitMap tests; verify GREEN.
- [ ] Commit the task.

### Task 5: Render child bodies/orbits and encounter geometry

**Files:**
- Modify: `KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs`
- Test: `Tools/OrbitMap/tests/test_14_22_8_renderer.py`

**Interfaces:**
- Consumes: `OrbitMapSceneSnapshot.ChildBodies` from Task 4.
- Produces: KSP-style primary-centered system view with child orbit lines, body markers, and transformed encounter patches.

- [ ] Write failing renderer tests requiring child orbit draw, child-body draw/label, and preserved active/target/patch rendering.
- [ ] Run focused renderer tests and verify RED.
- [ ] Add a dim child-orbit style and simple child-body circle/sphere markers with labels.
- [ ] Draw order: child orbits, primary, child bodies, target, active orbit, projected patches, markers, view indicator, panels.
- [ ] Preserve existing primary-body occlusion and control strip behavior.
- [ ] Run renderer and full OrbitMap tests; verify GREEN.
- [ ] Commit the task.

### Task 6: Improve encounter panel for child-body patches

**Files:**
- Modify: `KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs`
- Test: `Tools/OrbitMap/tests/test_14_22_8_encounter_panel.py`

**Interfaces:**
- Produces: encounter panel body name, transition type, and encounter periapsis when finite/available from the child-referenced patch.

- [ ] Write failing encounter-panel tests for recognized child body and finite encounter PE.
- [ ] Run focused test and verify RED.
- [ ] Extend `BuildEncounterText` without fabricating unavailable data.
- [ ] Run renderer/panel tests and verify GREEN.
- [ ] Commit the task.

### Task 7: Package validation and regression coverage

**Files:**
- Add: `Tools/OrbitMap/tests/test_14_22_8_package.py`
- Add: `README_14.22.8.txt`

**Interfaces:**
- Produces: repo-root drag-and-drop package covering all shared/plugin/Mission Control changes.

- [ ] Write a package test requiring all modified/added files and project entries.
- [ ] Run package test and verify RED before packaging.
- [ ] Write `README_14.22.8.txt` with ADD/MODIFY list, build instructions, runtime acceptance, and DLL requirements.
- [ ] Run the full available OrbitMap Python suite plus existing ElectricalExpansion regression suite.
- [ ] Run XML parse checks for modified `.csproj` files and `git diff --check` equivalent on the staged file set.
- [ ] Build `KMC_14.22.8_KSP_STYLE_MULTIBODY_MAP_DRAG_DROP.zip` rooted at the repository.
- [ ] Re-run package test against the final ZIP and verify GREEN.

### Task 8: User Visual Studio/runtime acceptance

**Files:** None.

- [ ] User builds `KMC.shared`, `KMC.Plugin`, and `KMC.MissionControl` in Release.
- [ ] User deploys the rebuilt KSP plugin/shared DLLs as required by the normal KMC install.
- [ ] Create a Kerbin→Mun encounter and compare KSP Map View with KMC MAP.
- [ ] Verify Kerbin centered, Mun/Minmus orbits visible, child body positions plausible, Kerbin departure path visible, Mun-local encounter patch displayed around Mun, encounter panel identifies Mun, RX remains live, and geometry rebuild count stays stable when the maneuver is unchanged.
- [ ] Repeat with Minmus if practical.
