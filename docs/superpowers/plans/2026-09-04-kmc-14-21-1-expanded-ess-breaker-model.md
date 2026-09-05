# KMC 14.21.1 — Expanded ESS Breaker Model Implementation Plan

## Goal
Add seven new ESS simulation loads/breakers and raise each ESS feed from 6.0 A to 12.0 A without changing KSP runtime authority.

## Production files modified
- `KMC.Engine/SpacecraftSystems/ElectricalDistributionSystem.cs`
- `KMC.Engine/SpacecraftSystems/SpacecraftSystemsFoundationSystem.cs`

No compiled-project source file is added; no `.csproj` edit is required.

## Tooling added
- `Tools/ElectricalExpansion/apply_14_21_1.py`
- `Tools/ElectricalExpansion/tests/test_14_21_1_expanded_ess_breakers.py`

## Implementation
1. Regression tests first.
2. Raise FEED_ESS_A/B to 12.0 A.
3. Add seven priority-1 BUS_ESS loads through the existing `AddLoad(...)` path.
4. Add matching foundation components.
5. Add BUS_ESS power dependencies.
6. Extend the existing DEBUG self-test to cover the new components and verify a single surviving ESS feed presents 12.0 A available current.
7. Preserve the existing RCS overlay.
8. Do not touch KMC.Plugin, MissionControl system-authority integration or GameData.
9. Run focused tests, frozen IVA tests and a Debug build before push.
10. Runtime acceptance: with one main side lost ESS must remain powered; with both main buses lost ESS must still collapse as before.

## Exact new equipment IDs
- FLIGHT_CONTROL
- REACTION_WHEEL
- ENGINE_CONTROL
- STAGING_CONTROL
- BRAKE_CONTROL
- GEAR_CONTROL
- LIGHTING_ESS

The existing `AddLoad` helper generates:
- BRK_FLIGHT_CONTROL
- BRK_REACTION_WHEEL
- BRK_ENGINE_CONTROL
- BRK_STAGING_CONTROL
- BRK_BRAKE_CONTROL
- BRK_GEAR_CONTROL
- BRK_LIGHTING_ESS

## Deferred
- KSP authority enforcement for all seven systems
- Page 2 breaker UI
- IVA breaker-control MFD
- multiplayer synchronization

**KSP Plugin DLL Required? NO**
