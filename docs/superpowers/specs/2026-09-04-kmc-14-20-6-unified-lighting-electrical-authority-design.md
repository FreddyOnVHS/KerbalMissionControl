# KMC 14.20.6 — Unified Lighting Electrical Authority Design

## Baseline

Frozen runtime baseline before this milestone:

- 14.20.5 frozen at `c7f4dcad026d2d77f7f3359b7a8b0c67aecc4aa5c`
- 14.20.5 aircraft IVA batch runtime PASS
- 28 automated IVA/tooling tests PASS
- Mk3 60x30 corrective runtime PASS

GitHub `master` remains the source of truth. Before implementation, compare the working tree to the frozen baseline and do not overwrite unrelated newer changes.

## Problem

KMC currently has three partially separate lighting behaviors:

1. Mk1 internal cockpit backlighting is electrically gated from actual ESS truth.
2. External KSP lights/window emissives can be physically inhibited by the 14.19.1 system-authority receiver.
3. Electrical distribution does not currently drive `SystemAuthorityKind.Lights`, so physical external lights can remain illuminated when the actual ESS bus is unpowered.

The newer IVA power-profile batches also do not yet generalize the proven Mk1 internal-lighting behavior across all supported DE_IVAExtension cockpits/capsules.

## Goal

Make internal IVA lighting and external vessel lighting obey the same KMC electrical truth without changing crew command state, without renderer/material hacks, and without weakening fail-open behavior.

## Core Electrical Rule

**KSP determines whether electrical energy really exists. KMC determines how power moves through simulated spacecraft.**

For lighting authority:

- The authoritative lighting supply is the actual `BUS_ESS` state from the KMC electrical-distribution model.
- A bus is electrically available only when:
  - the bus exists,
  - its state is neither `Unpowered` nor `Failed`,
  - its voltage is at or above the established KMC powered threshold of 18.0 V.
- MAIN A/B feed ESS through the existing electrical model. Therefore if both main buses fail and ESS falls to 0 V, lighting authority must also fail automatically.
- Contactors being commanded closed do not imply that the bus has voltage.

## External Vessel Lighting

### Existing mechanism to preserve

`KmcSystemAuthorityReceiver` already knows how to:

- preserve the vessel Light action-group command,
- disable relevant stock light module actions/events,
- force physical `ModuleLight`/`ModuleColoredLensLight` output off,
- force Light-action-group `ModuleColorChanger` emissives off,
- restore output according to the retained crew command,
- fail open when the authority lease expires.

This actuator must not be replaced.

### New authority source

`GncFailureIntegrationController` will derive electrical lighting authority from actual `BUS_ESS` state during every evaluation.

The final inhibit decision for `SystemAuthorityKind.Lights` is:

`explicit KMC Lights inhibit OR known ESS electrical loss`

This preserves existing explicit system-authority failure behavior while adding real electrical dependency.

If ESS evidence is unavailable/unknown, electrical lighting authority fails open and does not manufacture a light failure.

### Restore semantics

- Crew light command ON → ESS loss → physical lights OFF → ESS restore → physical lights return ON.
- Crew light command OFF → ESS loss → ESS restore → lights remain OFF.
- KMC/lease disappears → normal KSP light behavior returns automatically.

## Internal IVA Lighting

### Existing proven Mk1 pattern

ASET owns the crew command and final lighting output:

- crew command: `PERSISTENT_BackLight`
- effective output: `CUSTOM_ALCOR_BACKLIGHT_ON`

KMC must not modify the crew command.

The current Mk1 implementation gates `ALCOR_BACKLIGHT_ON` with KMC ESS truth and has already proven:

- ON → ESS loss → dark → ESS restore → automatically ON
- OFF → ESS loss → ESS restore → remains OFF

### Generalization strategy

Replace the Mk1-only electrical allow variable with a supported-DE-IVA lighting allow variable that:

- returns actual ESS power truth only for positively identified KMC-supported DE_IVAExtension interiors,
- returns `1` for unknown/unmanaged interiors,
- returns `1` when KMC status is unavailable,
- never modifies renderer, material, texture, mesh, or Unity Light objects.

The existing Mk1 behavior must remain identical after generalization.

The supported IVA set for this milestone is the interiors already brought to Mk1-reference electrical parity through 14.20.5:

- DE_mk1CockpitInternal
- Mk1 Command Pod interior
- Mk1 Inline Cockpit interior
- Mk1-3 Command Pod interior
- Mk1 Lander Can interior
- Mk2 Lander Can interior
- Cupola interior
- KV-1
- KV-2
- KV-3
- MEM
- MK2 POD
- Mk2 Spaceplane Cockpit
- Mk2 Inline Cockpit
- Mk3 Cockpit

Exact INTERNAL names must be taken from the current profiles/upstream configs during implementation; do not guess names from display labels.

Mission Control/kOS special cases remain outside 14.20.6.

## Code/Config Boundaries

Expected runtime code changes:

- `KMC.MissionControl/Engineering/GncFailureIntegrationController.cs`
  - derive known/unknown ESS lighting electrical authority
  - combine electrical loss with existing explicit Lights authority
  - continue using the existing system-authority packet/lease

- `KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs`
  - generalize the Mk1 lighting allow variable or add a narrowly named supported-IVA ESS-lighting variable
  - preserve fail-open behavior and 18.0 V threshold

Expected config changes:

- `GameData/KMC/IVA/KmcRpmBridge.cfg`
  - register the generalized lighting variable while preserving the Mk1 legacy alias

- `GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg`
  - preserve ASET native command/output architecture
  - gate supported DE IVAs using the generalized ESS-lighting allow variable

Tests may be added to the existing test structure and/or C# test projects already present in the repository. Do not add a new test framework unless the existing repository cannot express the required regression.

## Non-Goals

14.20.6 will not:

- redesign the electrical distribution model,
- introduce individual cabin-light circuits or breakers,
- assign different lighting buses per cockpit,
- add renderer/material/RenderTexture manipulation,
- change the crew's Light action-group state during power loss,
- modify SAS, gear, brakes, or RCS behavior,
- implement Mission Control/kOSTerminal special cases,
- add detailed RCS modeling.

## Required Automated Regressions

At minimum, tests must prove:

1. Lights authority is inhibited when BUS_ESS is known and electrically dead.
2. Lights authority is restored when BUS_ESS becomes energized again.
3. Unknown/missing ESS evidence fails open.
4. Existing explicit `SystemAuthorityStore` Lights inhibition still works.
5. Electrical availability does not override an explicit Lights inhibit.
6. The Mk1 cockpit remains electrically gated.
7. Every supported DE IVA receives the generalized ESS-lighting allow behavior.
8. Unknown/non-target IVAs remain fail-open.
9. No lighting config introduces renderer/material/texture/mesh manipulation.
10. Existing 14.20.5 IVA tests remain green.

## Runtime Acceptance Test

Use a fresh KSP launch after installing the build.

### External lighting

With crew external lights commanded ON:

1. Nominal power → physical external lights/window emissives ON.
2. Fail MAIN A only → behavior follows actual ESS state; if ESS remains powered, lights remain available.
3. Restore MAIN A.
4. Fail MAIN B only → same rule.
5. Restore MAIN B.
6. Fail MAIN A + MAIN B so the schematic shows BUS_ESS `UNPOWERED` / 0.0 V → physical external lights/window emissives OFF.
7. Restore power → lights automatically return ON because crew command remained ON.
8. Command external lights OFF.
9. Remove and restore electrical power → lights remain OFF after restoration.

### Internal lighting

For representative cockpit families:

- Mk1 Cockpit
- Mk1-3 or Mk1 Pod
- KV-series
- Mk2 aircraft
- Mk3

Test:

1. Internal/panel lighting ON at nominal power.
2. Collapse ESS to 0 V → internal lighting dark.
3. Restore ESS → internal lighting returns ON.
4. Turn internal lighting OFF.
5. Collapse/restore ESS → internal lighting remains OFF.

### Fail-open

While a KMC lighting inhibit is active, stop Mission Control or otherwise allow the lease to expire. Normal KSP light authority must return automatically.

## DLL Requirement

**KSP Plugin DLL Required? YES**

Reason: 14.20.6 changes runtime C# authority logic and the RPM variable handler in `KMC.Plugin`.

Required install procedure after a successful Debug build:

1. Close KSP.
2. Build Debug.
3. Replace installed plugin with `KMC.Plugin\bin\Debug\KMC.Plugin.dll`.
4. Copy updated `GameData\KMC` configs.
5. Restart KSP.
