# KMC 14.21.0 — Electrical System Expansion Architecture

## Approved architecture

KSP remains the physical ElectricCharge truth. KMC owns the synthetic electrical distribution simulation.

The existing breaker state path remains authoritative:

`CommandedClosed -> ActualClosed -> IndicatedClosed -> Conducting -> Powered/Unpowered`

IVA MFDs will eventually be the normal player breaker-control surface. KMC determines actual/indicated/conducting truth. The KMC electrical UI remains primarily supervisory.

## Electrical pages

### Page 1 — ONE-LINE
Keep the existing high-level sources, MAIN A/B, ESS, contactors/feed paths and major loads.

### Page 2 — LOADS / BREAKERS
Three columns:

`MAIN BUS A | ESSENTIAL BUS | MAIN BUS B`

Existing MAIN A:
- GUID COMPUTER A
- COMM TRANSCEIVER A
- PROP FEED PUMP A
- CABIN FAN A
- THERMAL HEATER A

ESS:
- PRIMARY FLIGHT COMPUTER — existing
- ESS INSTRUMENTATION — existing
- RCS CONTROL / VALVES — existing
- SAS / FLIGHT CONTROL ELECTRONICS — new
- REACTION WHEEL POWER — new
- ENGINE CONTROL / IGNITION — new
- STAGING / SEPARATION — new
- BRAKE CONTROL — new
- GEAR CONTROL / ACTUATION — new
- EXTERNAL / EMERGENCY LIGHTING — new

Existing MAIN B:
- GUID COMPUTER B
- COMM TRANSCEIVER B
- PROP FEED PUMP B
- CABIN FAN B
- THERMAL HEATER B

## ESS feeder sizing — Option A

- FEED_ESS_A: 12.0 A
- FEED_ESS_B: 12.0 A

Either single surviving feed must carry the normal ESS branch.

Existing ESS demand:
- Primary Flight Computer: 3.0 A
- ESS Instrumentation: 1.0 A
- RCS Control / Valves overlay: 1.0 A

New ESS demand:
- FLIGHT_CONTROL: 1.0 A
- REACTION_WHEEL: 1.0 A
- ENGINE_CONTROL: 0.75 A
- STAGING_CONTROL: 0.25 A
- BRAKE_CONTROL: 0.5 A
- GEAR_CONTROL: 0.5 A
- LIGHTING_ESS: 0.5 A

New demand = 4.5 A.
Normal ESS demand including RCS = 9.5 A.
Single-feed utilization = 9.5 / 12.0 = 79.17%, below the existing 80% HIGH LOAD threshold.

Synthetic amps remain weighting/capacity values. KSP does not receive physical amp values.

## Exact generated breaker IDs

KMC's existing `AddLoad(...)` path generates `BRK_` + equipment ID. Preserve that mechanism.

- BRK_FLIGHT_CONTROL
- BRK_REACTION_WHEEL
- BRK_ENGINE_CONTROL
- BRK_STAGING_CONTROL
- BRK_BRAKE_CONTROL
- BRK_GEAR_CONTROL
- BRK_LIGHTING_ESS

Existing BRK_RCS_CONTROL remains unchanged.

## Locked simplifications

- all gear together
- all brakes together
- all exterior/emergency lights together
- all staging/separation together
- engine control/ignition together
- PROP FEED PUMP A/B remain simple
- COMM A/B remain simple
- GUID A/B remain as built
- utilities remain as built
- ESS instrumentation remains as built
- RCS remains blanket binary; detailed manifold/directional modeling deferred

## Performance / future multiplayer constraints

- KMC owns simulation authority.
- KSP supplies telemetry and enforces consequences.
- Do not add normal-tick vessel-wide discovery.
- Cache topology/module references where applicable.
- Prefer event-driven/dirty recalculation.
- Do not assume KMC and KSP must be on the same PC.
- Multiplayer transport/synchronization is not implemented in this milestone.

## 14.21.1 boundary

14.21.1 adds simulation state only. It does not wire these new breaker states into real KSP SAS, reaction wheels, engines, staging, brakes, gear or lights yet.

**KSP Plugin DLL Required? NO**
