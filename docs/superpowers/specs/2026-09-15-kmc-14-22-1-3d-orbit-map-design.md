# KMC 14.22.1 — 3D Orbit Map Design

## Status

Design approved in chat for implementation planning. Base is frozen KMC 14.21.10 at `34ecdfdcfb44aef63b3444ee98b47ead5c8f3568`.

## Goal

Add a Mission Control **MAP** page that presents a true rotatable 3D orbital view, similar in purpose to KSP Map View but optimized as a lightweight engineering/data display.

The first milestone displays KSP-authoritative orbit, target, maneuver-node, projected patched-conic, and encounter data. It does **not** yet calculate or create new KMC transfer burns.

## User-facing scope

The MAP page shall provide:

- true 3D body-centered orbital view
- current reference body at scene origin
- active vessel marker and current orbit
- apoapsis and periapsis markers
- currently selected KSP target when present
- target orbit when applicable
- existing KSP maneuver node(s)
- projected post-burn orbit patches
- SOI escape / encounter transitions supplied by KSP
- mouse drag to rotate
- mouse wheel to zoom
- view reset / fit controls
- compact numeric panels for current orbit, target, maneuver, and encounter data
- live/stale/unavailable telemetry state indication

Out of scope for 14.22.1:

- KMC-generated Hohmann transfers
- KMC-generated rendezvous or intercept solutions
- maneuver editing in the MAP page
- terrain, clouds, atmosphere rendering, starfield, or photorealistic planets
- whole-save vessel plotting
- arbitrary solar-system scene rendering

## Architecture

The feature is split into three responsibilities:

1. **KSP Plugin telemetry producer**
   - reads active-vessel orbital state from KSP
   - reads selected target state
   - reads maneuver nodes and patched-conic projections
   - publishes a compact orbital-map snapshot

2. **Shared orbital-map protocol/model**
   - versioned protocol separate from the frozen general flight telemetry packet
   - validates finite numeric values and bounded collection sizes
   - carries orbital elements and patch metadata rather than sampled display points

3. **Mission Control MAP page**
   - receives and validates the latest snapshot
   - builds cached 3D conic geometry only when trajectory-defining data changes
   - renders the body, cached orbit geometry, markers, labels, and compact data panels
   - maintains camera state independently of KSP

Mission Control must not reach into KSP directly. KSP publishes authoritative state; Mission Control renders it.

## Telemetry protocol

Create a dedicated versioned orbital-map side channel, conceptually `KMC-ORBITMAP1`.

The initial packet shall carry:

### Frame / identity

- packet timestamp / sequence
- vessel ID
- vessel name
- Universal Time

### Reference body

- body ID/name
- body radius
- SOI radius
- reference-frame information required to interpret orbital elements consistently

### Active vessel orbit

- semi-major axis
- eccentricity
- inclination
- longitude of ascending node
- argument of periapsis
- epoch
- mean anomaly at epoch
- apoapsis
- periapsis
- period when defined
- current body-centered position

### Target

- target present flag
- target type
- target ID/name
- target orbit when applicable
- current target position when applicable

### Maneuver nodes

For each bounded node entry:

- node UT
- prograde delta-v
- normal delta-v
- radial delta-v
- total delta-v
- node position / patch association

### Patched-conic segments

For each bounded future patch:

- ordered patch index
- reference body
- start UT
- end UT when finite/defined
- orbital elements
- transition type
- next body / encounter body when applicable

Mission Control shall reconstruct display curves from these orbital elements. The protocol shall not transmit thousands of pre-sampled 3D points.

## Update cadence

The orbital-map packet shall target approximately **5 Hz** initially.

This cadence is intentionally lower than camera redraw frequency. Camera movement uses cached geometry and does not wait for fresh KSP telemetry.

Existing epoch telemetry may continue to provide higher-frequency UT synchronization independently.

## Coordinate system

The first implementation uses a **body-centered inertial frame**:

- current reference body center = `(0, 0, 0)`
- all active-vessel, target, node, and orbit-patch positions are represented relative to that body
- orbit geometry preserves true relative scale and inclination
- camera transforms are Mission Control presentation only

No non-uniform spatial distortion is allowed in trajectory geometry.

## Camera interaction

Initial controls:

- left-drag: orbit/rotate camera around scene center
- mouse wheel: zoom
- reset view: restore a useful default orientation
- fit orbit: frame current orbit
- fit maneuver: frame current + projected maneuver trajectory when available
- target view control: frame target context when present

Panning is deferred unless required during implementation.

Camera state shall persist through ordinary telemetry refreshes. New orbital data must not reset the user’s viewing angle.

## Rendering style

The MAP page is an engineering display, not a photorealistic scene.

Use:

- simple low-cost shaded sphere for current body
- dark background
- anti-aliased orbit lines
- visually distinct current orbit, target orbit, and projected patches
- simple vessel / target / maneuver / AP / PE markers
- minimal in-view labels

Do not add terrain, atmospheric effects, clouds, textures, or starfield in 14.22.1.

## Layout

Use the existing Mission Control visual language.

The page header is:

`ORBIT MAP` with an FDO/trajectory-style subtitle.

The 3D viewport should occupy roughly 70–75% of usable page height.

Below the viewport, provide four compact information groups:

- **CURRENT ORBIT** — body, vessel, AP, PE, inclination, period
- **TARGET** — target name/type and relevant relative/trajectory data
- **MANEUVER** — time to node, node UT, prograde/normal/radial/total DV, burn estimate when available
- **ENCOUNTER** — encounter body, ETA, encounter PE / SOI information when available

Panels remain in fixed positions. Missing data renders as `NONE`, `NO ENCOUNTER`, or `UNAVAILABLE` rather than changing layout geometry.

The 3D view should limit labels to operationally useful markers such as AP, PE, MNV1, vessel, target, and SOI transition.

## Performance design

Performance is a primary requirement.

The renderer shall use a **retained/cached scene** rather than recalculating orbital geometry on every paint.

### Geometry cache rules

Rebuild cached orbit/patch geometry only when trajectory-defining state changes, including:

- orbital elements
- reference body
- target orbit
- maneuver node list or DV/UT
- patched-conic sequence
- encounter / transition metadata

A timestamp-only update must not force a geometry rebuild.

### Sampling

Initial orbit/patch sampling should use a bounded practical count, approximately 128–256 samples per visible conic segment unless testing justifies a different value.

Adaptive sampling may be added later if required.

### Paint path

The ordinary paint path should primarily:

- transform cached 3D points by the current camera
- project them to screen coordinates
- draw body, lines, markers, labels, and panels

No orbital propagation or conic rebuilding should happen merely because the page repaints or the user rotates the camera.

### Inactive page behavior

The MAP page must not run an expensive active redraw loop while another Mission Control page is selected.

Latest telemetry may remain cached, but 3D redraw work should be throttled/stopped when MAP is not visible.

### Diagnostic acceptance

Development diagnostics should expose counts for:

- telemetry snapshots received
- geometry rebuilds
- viewport redraws

This allows verification that camera-only interaction and timestamp-only telemetry do not trigger trajectory rebuilds.

## Data freshness and failure behavior

The MAP page has three operational states:

### LIVE

Recent valid orbital telemetry. Normal rendering and normal line intensity.

### STALE

Telemetry is delayed beyond the normal freshness window. Retain the last valid scene, visibly mark it stale, and dim trajectory presentation. The frozen scene may still be rotated locally.

### UNAVAILABLE

Telemetry lease expired beyond the allowed stale interval. Numeric fields change to unavailable values and the viewport prominently indicates `ORBIT DATA UNAVAILABLE`.

Malformed packets, NaN/infinite values, impossible bounded collections, or invalid patch ordering shall be rejected before they reach render state.

A rejected update must not destroy the last valid snapshot.

## Testing strategy

### Protocol tests

- serialize/parse round trip
- version rejection
- malformed field rejection
- finite-number validation
- collection bounds
- maneuver-node bounds
- patch bounds and ordering

### Geometry tests

- circular orbit
- elliptical orbit
- inclined orbit
- highly eccentric orbit
- open escape/hyperbolic patch when representable by supplied KSP state
- chained patched-conic segments
- AP/PE marker placement
- node placement

### Camera/projection tests

- rotation stability
- zoom behavior
- correct transform ordering
- near/far clipping behavior
- no geometry mutation from camera movement

### Mission Control integration tests

- LIVE → STALE → UNAVAILABLE transitions
- last-good-scene retention on malformed packet
- geometry cache reuse on timestamp-only updates
- geometry rebuild on real orbital change
- geometry rebuild on node/target/patch change
- inactive-page throttling

### Runtime acceptance

Compare the Mission Control MAP page against KSP Map View for the same active vessel and target.

Verify:

- current orbit orientation and shape
- AP/PE values and locations
- maneuver node position
- projected post-burn trajectory
- target orbit
- SOI transition / encounter patch
- encounter values where available
- smooth rotate/zoom interaction
- no noticeable KSP slowdown attributable to telemetry publication
- no noticeable Mission Control hitching from ordinary camera movement

## Implementation boundary

14.22.1 is successful when Mission Control accurately and efficiently displays the KSP-authoritative 3D orbital state and maneuver projection.

KMC-generated transfer calculations, proposed burns, and maneuver creation are intentionally deferred to a later milestone built on this telemetry/rendering foundation.

## KSP Plugin DLL

**KSP Plugin DLL Required? YES**

The existing plugin does not currently publish the full orbital, target, maneuver-node, and patched-conic state required by this page.