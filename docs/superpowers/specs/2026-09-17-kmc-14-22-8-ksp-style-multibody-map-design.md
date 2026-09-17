# KMC 14.22.8 — KSP-Style Multi-Body System View Design

Date: 2026-09-17
Baseline: `master` at `541075cbc02e8375a5a556380de2d2d8c71929f8`

## Goal

Extend the existing 14.22.7 MAP page from a single-body local display into a KSP-style system-scale view that can show the current primary body, its immediate child bodies, their orbits, the active vessel transfer trajectory, and cross-SOI encounter patches in one coherent parent-body frame.

The design must preserve the existing low-overhead rendering model: expensive geometry is cached and rebuilt only when telemetry changes materially; camera motion only transforms cached points.

## Approved User Experience

For a Kerbin-centered view, KMC should behave like KSP Map View:

- Kerbin remains centered.
- Immediate child bodies such as Mun and Minmus are visible.
- Child-body orbital paths are drawn around Kerbin.
- Child bodies are drawn at their current/projection epoch positions.
- The active vessel orbit and maneuver transfer path are visible in the same parent frame.
- When the trajectory enters a child body's SOI, the local encounter patch is transformed into the parent system frame and drawn around the encountered body.
- Encounter periapsis/apoapsis markers are shown relative to the encountered body where applicable.
- Existing mouse rotate/zoom controls, body occlusion behavior, MAP control strip, and `RESET VIEW -> AZ 360 / EL 00` remain intact.

Visual fidelity is intentionally schematic. No textured planets, terrain, clouds, atmosphere, or starfield are required.

## Architecture

Use a hybrid authoritative-data model.

KSP remains the source of truth for:

- celestial-body hierarchy,
- child-body orbital state,
- vessel patched-conic chain,
- maneuver nodes,
- patch reference bodies and transition metadata,
- timing/epoch information.

Mission Control remains responsible for:

- reconstructing/caching orbit geometry,
- transforming child/local patch geometry into the primary-body frame,
- camera projection,
- labels and markers,
- redraw throttling.

The plugin does not send pre-sampled render polylines. It sends compact orbital/body metadata and enough authoritative state to anchor transforms reliably.

## Telemetry Protocol

Extend `KMC-ORBITMAP1` with a bounded celestial-body collection. Keep the existing vessel, target, maneuver-node, and patch payloads.

Add a body model containing at minimum:

- body name,
- parent body name,
- radius,
- sphere-of-influence radius,
- orbital elements relative to parent,
- epoch and mean anomaly at epoch,
- current/projection position in the parent frame when available.

Only the current primary body and its immediate children are transmitted for the first implementation. This keeps packet size and rendering cost bounded while matching the user's approved Kerbin/Mun/Minmus workflow.

The packet remains finite/bounded and rejects malformed/non-finite required values as the existing protocol does.

## Coordinate Model

The current primary body is the system-view origin.

Child bodies are reconstructed in the primary frame from their parent-relative orbital elements and authoritative epoch/state. Each child therefore has:

1. a cached orbit curve in the primary frame, and
2. a body-center position in the primary frame for the current telemetry epoch.

For vessel patches:

- patches whose reference body is the primary body are sampled directly in the primary frame;
- patches whose reference body is an immediate child are sampled in that child's local frame, then translated by the child body's primary-frame position at the corresponding patch time;
- encounter markers are computed in the child's local frame and transformed by the same translation.

This produces one coherent system-scale scene without switching coordinate systems on screen.

For 14.22.8, only one hierarchy level below the current primary is required. Recursive multi-level transforms are explicitly deferred unless runtime testing proves them necessary for the selected KSP scenario.

## Rendering and Performance

Performance remains a hard requirement.

Mission Control will cache:

- active-vessel orbit geometry,
- each immediate child's orbit geometry,
- projected vessel patch geometry,
- encounter/local-patch geometry transformed into the primary frame.

Geometry rebuild triggers include changes to:

- primary body,
- child-body list,
- child-body orbital elements,
- maneuver solution,
- patch sequence or patch orbital elements,
- target/body selection when it affects visible geometry.

Camera rotation and zoom never trigger orbital recomputation.

The MAP page continues to throttle expensive work while inactive.

Initial sampling limits:

- child closed orbit: approximately 128–192 samples,
- vessel/projected patch: up to approximately 192–256 samples per visible patch,
- number of immediate children bounded by the telemetry protocol.

These limits may be tuned after runtime profiling but should not be exceeded casually.

## Scene Model Changes

`OrbitMapSceneSnapshot` should gain a bounded set of child-body render entries. Each entry should include:

- body name,
- radius,
- current primary-frame position,
- cached primary-frame orbit polyline,
- optional SOI radius for encounter visualization.

Projected patches should no longer be filtered out solely because their `ReferenceBodyName` differs from the primary body. Instead, Mission Control resolves the matching child body and transforms that patch into the primary frame.

If a patch references a body that is not present in the transmitted immediate-child set, the renderer must fail safely: keep valid geometry, omit the unresolved patch, and surface the encounter metadata rather than inventing a transform.

## Renderer Behavior

Draw order should remain simple and predictable:

1. background/frame,
2. child-body orbit paths,
3. primary body,
4. child bodies,
5. target orbit when applicable,
6. active vessel orbit,
7. projected maneuver/transfer patches,
8. encounter/local patches,
9. markers and labels,
10. view indicator and data panels.

Body occlusion remains active for trajectories behind the primary body. Child-body local encounter segments should also be visually associated with the child body but do not require full per-child hidden-surface occlusion in the first version unless it is cheap to add cleanly.

## Encounter Panel

The existing encounter panel should continue to use KSP patch metadata, but for a recognized child-body encounter it should show at least:

- encountered body name,
- transition type,
- encounter/patch availability state.

If reliable encounter periapsis can be computed from the child-referenced patch elements, display it. Do not fabricate data when the patch is incomplete.

## Error Handling

Preserve the existing LIVE / STALE / UNAVAILABLE behavior.

Additional multi-body rules:

- malformed child-body entries are rejected during packet parse,
- duplicate body names within one packet are rejected,
- unknown parent relationships are ignored safely,
- an unresolved cross-SOI patch is omitted rather than drawn in the wrong frame,
- bad child-body data must not destroy the last good scene snapshot.

## Testing

### Protocol tests

- body collection serialize/parse round-trip,
- bounds enforcement,
- malformed/duplicate body rejection,
- finite-number validation.

### Transform/math tests

- child-body orbit reconstruction around primary,
- child-body position at epoch,
- local child-referenced patch translated into primary frame,
- encounter marker translated using the same body-center transform.

### Scene-cache tests

- child-body geometry rebuilds only when body/orbit data changes,
- camera-only changes do not rebuild geometry,
- unresolved child patch is omitted safely,
- same-body current behavior remains unchanged.

### Renderer/integration tests

- immediate child orbits/bodies are present,
- projected Kerbin-side transfer remains visible,
- child-SOI encounter patch is rendered in the same overall system view,
- existing control strip/reset behavior is unchanged,
- RX may increase continuously while geometry rebuild count remains low/stable.

## Runtime Acceptance

Use a Kerbin-to-Mun encounter as the first acceptance scenario.

Compare KSP Map View and KMC MAP for the same maneuver and verify:

- Kerbin centered,
- Mun orbit visible,
- Mun at the expected relative orbital position,
- vessel departure trajectory matches KSP conceptually and numerically,
- encounter transition is reported as Mun,
- Mun-local encounter patch appears around Mun in the same system view,
- encounter periapsis, if displayed, agrees with KSP within reasonable numeric precision,
- RX remains live,
- geometry rebuild count stays stable when the maneuver is not being edited,
- camera interaction remains smooth.

After Mun validation, repeat with Minmus if practical.

## Deferred Scope

Not part of 14.22.8:

- KMC-generated transfer calculations,
- automatic maneuver creation/upload,
- full Kerbol-system rendering at once,
- recursive arbitrary-depth celestial hierarchy,
- photorealistic body rendering,
- terrain/atmosphere/starfield,
- multiplayer synchronization changes.

## Build Impact

- KSP Plugin DLL Required? **YES**
- Mission Control rebuild required? **YES**
- `KMC.shared.dll` change required? **YES**
