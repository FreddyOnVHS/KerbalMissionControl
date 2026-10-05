# Navigation foundation: steps 1–4

## Approved intent
Preserve the working MAP transfer workflow from master 38f30056eb989001859bdacdf135d78f385eda97 while creating reusable celestial mechanics and moving the existing approximate planner out of the UI. This is the first implementation milestone; Lambert, search, full maneuver vectors, encounter prediction and hierarchy routing follow later.

## Boundaries
- `KMC.Engine/CelestialMechanics`: double-precision vectors, time-tagged state, elements, body data and a two-body Kepler propagator. These sources use only System, without KSP, Unity, drawing, telemetry or UI types.
- `KMC.Engine/Navigation`: the existing Hohmann window and near-circular parking-orbit ejection approximation, with generic inputs/results. Preserve formulas, supported-domain limits, display values and nearest parking-pass selection for valid baseline inputs.
- `KMC.MissionControl/Navigation`: explicitly map OrbitMap telemetry into generic data. No mathematical frame inference from the packet's renderer positions.
- `MapPage`: retain selection, drawing, controls, packet construction, submission locks, uplink and refinement. Call the engine via the adapter.
- Leave renderer, plugin, wire protocol and KSP-authoritative encounter assessment intact.

## Numerical contract
SI units (m, s, m/s, m³/s²); time is KSP UT without calendar conversion. Elements retain explicitly named degrees for orientation and radians for mean anomaly. State vectors use a right-handed inertial XYZ frame with XY as reference plane and +Z normal; positive inclination rotates toward +Z, followed by ascending-node rotation. The reference body name identifies the origin. This matches the element reconstruction convention already used by the renderer, not necessarily raw KSP/Unity world axes.

Propagate elliptic (a > 0, 0 <= e < 1) and hyperbolic (a < 0, e > 1) elements to position AND velocity at any representable supported UT. Use bracketed Kepler solves with explicit convergence failure. Exact parabolic elements (e = 1) are rejected because finite semi-major axis/mean-anomaly telemetry does not represent them. Invalid/nonfinite data returns failure and no solution. Do not silently clamp invalid data into an orbit. General state-to-state universal propagation is a future extension; this milestone provides state vectors from the element-based telemetry already available.

The extracted legacy planner retains its original planar longitude approximation and Newton helper in a clearly named compatibility implementation. The new general 3D propagator must not silently change the old planner's displayed phases or burn times. The planner continues to accept only same-parent elliptic body orbits, parking eccentricity <= 0.05 and normalized inclination <= 10 degrees. It rejects malformed data safely.

## Verification and delivery
Capture numerical outputs from the unmodified baseline before refactoring. Exercise both inward/outward transfers, varied epoch/orientation, synthetic body names and the parking-orbit limits. Compare all solution fields after migration. Test propagation against analytic circular/apsis/known-anomaly cases, inclined/retrograde rotations, hyperbolic incoming/outgoing states, energy/angular momentum and time-reversal sampling. Compare positions against the existing conic sampler on its stable domain; this is compatibility evidence, not a fresh KSP runtime validation.

Build all four .NET Framework 4.8 projects with local Visual Studio and KSP references. Run repository Python regressions; record pre-existing failures separately from introduced failures. Ship a runnable Mission Control package, changed-source overlay, source patch and a focused in-game checklist. Do not install into the live game, push or merge automatically.

Reference: NASA/JPL SPICE CONICS documents the element-to-position/velocity contract and conic input validation: https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/conics_c.html. No external orbital library is required.
