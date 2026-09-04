# KMC 14.20.6 — Unified Lighting Electrical Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make external vessel lighting and supported DE_IVAExtension internal lighting obey actual KMC ESS electrical power while preserving crew commands, existing fail-open behavior, and the proven 14.19.1 light actuator.

**Architecture:** Reuse the existing `KmcSystemAuthorityReceiver` for physical external-light suppression and add only the missing electrical-authority derivation in `GncFailureIntegrationController`. Generalize the existing Mk1 RPM backlight allow path in `KmcRpmLightingScopeVariableHandler`, register it in `KmcRpmBridge.cfg`, and keep `KmcRpmCockpitLighting14_18_10.cfg` as the ASET output gate so supported DE IVAs use actual ESS truth, while unknown/unmanaged IVAs remain fail-open.

**Tech Stack:** C#/.NET KMC Mission Control + KSP plugin, RasterPropMonitor/ASET ModuleManager CFG, existing Python IVA audit tests, existing repository test/build tooling.

**Spec:** `docs/superpowers/specs/2026-09-04-kmc-14-20-6-unified-lighting-electrical-authority-design.md`

## Global Constraints

- Frozen baseline before implementation: `c7f4dcad026d2d77f7f3359b7a8b0c67aecc4aa5c`.
- GitHub `master` is the source of truth. Before implementation, verify the working tree is based on the frozen baseline and do not overwrite unrelated newer changes.
- **KSP determines whether electrical energy really exists. KMC determines how power moves through simulated spacecraft.**
- Lighting electrical truth comes from actual `BUS_ESS` state/voltage, not contactor command state.
- Powered threshold remains 18.0 V.
- Preserve current 14.19.1 `KmcSystemAuthorityReceiver`; do not replace the physical-light actuator.
- Preserve crew light command state during electrical loss.
- Preserve lease-based fail-open behavior.
- No renderer/material/RenderTexture/texture/mesh manipulation.
- No detailed RCS modeling.
- Mission Control/kOS special cases remain out of scope.
- Do not push until automated tests, Debug build, and runtime acceptance pass.
- **KSP Plugin DLL Required? YES**

---

### Task 1: External Lights — Derive Electrical Authority from BUS_ESS

**Files:**
- Modify: `KMC.MissionControl/Engineering/GncFailureIntegrationController.cs`
- Test: use the existing C# test project that currently exercises `GncFailureIntegrationController` / system-authority output. If no direct test exists, add the smallest test file in the existing MissionControl test project rather than adding a new framework.

**Interfaces:**
- Consumes:
  - current electrical distribution snapshot/model already used by the controller for RCS electrical authority
  - `SystemAuthorityStore.IsInhibited(vesselId, SystemAuthorityKind.Lights)`
  - existing `SystemAuthorityPacket` publication path
- Produces:
  - final Lights inhibit = `explicitLightsInhibit || knownEssElectricalLoss`
  - unknown/missing ESS evidence must not create an electrical inhibit

- [ ] **Step 1: Identify the current working RCS electrical-authority helper/path**

Read the complete RCS authority logic in `GncFailureIntegrationController.cs`. Record the exact existing members used to locate `BUS_ESS`, interpret bus state, voltage, breaker/load state, and publish authority. Do not invent parallel electrical semantics if the controller already exposes a reusable helper.

- [ ] **Step 2: Write a failing test for known dead ESS**

Add a regression that constructs/controller-feeds a vessel state with:

```text
BUS_ESS exists
state = Unpowered (or equivalent existing enum)
voltage = 0.0 V
explicit Lights inhibit = false
```

Assert that the emitted/published `SystemAuthorityKind.Lights` authority is inhibited.

Run only this test.

Expected: **FAIL** because current Lights authority ignores electrical bus loss.

- [ ] **Step 3: Write a failing test for restored ESS**

Test transition/state with:

```text
BUS_ESS exists
state = energized/normal
voltage >= 18.0 V
explicit Lights inhibit = false
```

Assert Lights is not inhibited.

If the test passes before production change, keep it as a guard but make sure Step 2 remains the required RED test.

- [ ] **Step 4: Write a failing/guard test for unknown ESS evidence**

Test:

```text
BUS_ESS missing / electrical snapshot unavailable
explicit Lights inhibit = false
```

Assert Lights is not electrically inhibited.

This protects fail-open semantics.

- [ ] **Step 5: Write a guard test for explicit Lights inhibit**

Test:

```text
BUS_ESS energized
explicit Lights inhibit = true
```

Assert Lights remains inhibited.

- [ ] **Step 6: Implement the minimal controller change**

In `GncFailureIntegrationController.cs`, derive a tri-state or equivalent:

```csharp
bool? essPowered
```

using the existing electrical model semantics:

```text
true  = BUS_ESS known and electrically available
false = BUS_ESS known and dead/failed/below 18.0 V
null  = evidence unavailable/unknown
```

Then compute:

```csharp
bool explicitLightsInhibit =
    systemAuthorityStore.IsInhibited(vesselId, SystemAuthorityKind.Lights);

bool electricalLightsInhibit =
    essPowered.HasValue && !essPowered.Value;

bool finalLightsInhibit =
    explicitLightsInhibit || electricalLightsInhibit;
```

Publish `finalLightsInhibit` through the existing System Authority packet/lease path. Do not modify `KmcSystemAuthorityReceiver`.

- [ ] **Step 7: Run the focused external-light tests**

Expected: all Task 1 tests PASS.

- [ ] **Step 8: Run all relevant C# tests**

Run the repository’s existing MissionControl/authority test command(s).

Expected: 0 failures.

- [ ] **Step 9: Commit Task 1**

```bash
git add KMC.MissionControl/Engineering/GncFailureIntegrationController.cs <existing-test-project-path>
git commit -m "fix: drive light authority from ESS power"
```

Do not push.

---

### Task 2: Internal IVA Lighting — Generalize Mk1 ESS Allow Variable

**Files:**
- Modify: `KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs`
- Modify: `GameData/KMC/IVA/KmcRpmBridge.cfg`
- Modify: `GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg`
- Test: `Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_6.py` (new)

**Interfaces:**
- Consumes:
  - existing RPM variable registration/evaluation path
  - existing `KMC_ESS_POWERED` semantics / status packet interpretation
  - exact INTERNAL names from current profiles
- Produces:
  - one generalized supported-DE-IVA ESS-lighting allow variable
  - `1` for supported IVA + ESS powered
  - `0` for supported IVA + ESS known dead
  - `1` for unknown/unmanaged IVA
  - `1` when KMC status is unavailable

- [ ] **Step 1: Enumerate exact supported INTERNAL names**

Read the current profile files under:

```text
GameData/KMC/IVA/Profiles/DE_IVAExtension/
```

for all interiors supported through 14.20.5. Build an exact allow-list from the actual `INTERNAL` targets. Do not infer names from cockpit display text.

- [ ] **Step 2: Write RED tests for generalized lighting coverage**

Create:

```text
Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_6.py
```

with tests that assert:

```text
- Mk1 remains included
- Mk1 Pod / Mk1 Inline / Mk1-3 / Mk1 Lander / Mk2 Lander / Cupola included
- KV1 / KV2 / KV3 / MEM / MK2 POD included
- Mk2 Cockpit / Mk2 Inline / Mk3 included
- Mission Control remains excluded
- lighting cfg contains no renderer/material/texture/mesh manipulation
```

Also assert the CFG references the new generalized KMC lighting-allow variable instead of a Mk1-only variable.

Run only this file.

Expected: **FAIL** because generalized coverage does not yet exist.

- [ ] **Step 3: Write/extend C# regression tests for the RPM variable handler**

Use the existing plugin test structure if available. Cover:

```text
supported IVA + ESS powered      => 1
supported IVA + ESS dead         => 0
unknown IVA + ESS dead           => 1
supported IVA + status missing   => 1
```

If direct plugin unit testing is not currently practical in the repository, keep the Python CFG tests and add a narrowly scoped pure helper in `KmcRpmLightingScopeVariableHandler.cs` only if that follows existing testability patterns. Do not add a new test framework.

- [ ] **Step 4: Implement the generalized RPM variable**

In `KmcRpmLightingScopeVariableHandler.cs`:

1. Preserve the existing 18.0 V threshold and status interpretation used by `KMC_ESS_POWERED`.
2. Replace or alias the Mk1-only lighting allow variable with a name scoped to supported DE IVAs, for example:

```text
KMC_DE_IVA_BACKLIGHT_ALLOW
```

3. Logic:

```text
if KMC status unavailable:
    return 1

if current INTERNAL is not in supported DE IVA allow-list:
    return 1

return actual ESS powered truth ? 1 : 0
```

4. Preserve the legacy Mk1 variable as an alias for backward compatibility if any existing CFG still references it after the change.

- [ ] **Step 5: Generalize the ModuleManager CFG**

In `KmcRpmCockpitLighting14_18_10.cfg`:

- keep ASET’s `PERSISTENT_BackLight` command untouched
- keep the effective `CUSTOM_ALCOR_BACKLIGHT_ON` path
- replace the Mk1-only KMC allow reference with the generalized supported-DE-IVA allow variable
- scope only to the supported DE IVA set
- unknown/non-target IVAs must remain untouched/fail-open
- do not add renderer/material/texture/mesh operations

- [ ] **Step 6: Run Task 2 focused tests**

Run:

```powershell
python -m unittest Tools.IvaCoverageAudit.tests.test_iva_batch_14_20_6 -v
```

Expected: PASS.

- [ ] **Step 7: Run the complete IVA/tooling suite**

Run:

```powershell
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v
```

Expected count: current 28 tests plus the new 14.20.6 tests. Record the actual count; do not hard-code a completion claim until observed.

- [ ] **Step 8: Run plugin/MissionControl C# tests**

Run the existing repository test command(s) covering plugin and Mission Control.

Expected: 0 failures.

- [ ] **Step 9: Commit Task 2**

```bash
git add KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs GameData/KMC/IVA/KmcRpmBridge.cfg GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg Tools/IvaCoverageAudit/tests/test_iva_batch_14_20_6.py
git commit -m "feat: generalize IVA lighting ESS authority"
```

Do not push.

---

### Task 3: Build and Package 14.20.6

**Files:**
- Create: `README_14.20.6.txt`
- Include full replacement copies of every changed runtime/config/test file
- Include updated project files only if source membership changed

**Interfaces:**
- Consumes: completed Task 1 + Task 2 changes
- Produces: drag/drop ZIP rooted at repository folders

- [ ] **Step 1: Write README_14.20.6.txt**

It must state:

```text
KMC 14.20.6 — Unified Lighting Electrical Authority

ADD:
- any new test file(s)

REPLACE:
- KMC.MissionControl/Engineering/GncFailureIntegrationController.cs
- KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs
- GameData/KMC/IVA/KmcRpmBridge.cfg
- GameData/KMC/IVA/KmcRpmCockpitLighting14_18_10.cfg
- README_14.20.6.txt

REMOVE:
- none, unless implementation proves otherwise

KSP Plugin DLL Required? YES

DLL procedure:
1. Build Debug.
2. Close KSP.
3. Replace installed plugin with KMC.Plugin\bin\Debug\KMC.Plugin.dll.
4. Copy updated GameData\KMC files.
5. Restart KSP.
```

Also include the complete automated and runtime test procedure.

- [ ] **Step 2: Run full automated tests before build**

Run all repository tests relevant to changed C# code and:

```powershell
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v
```

Expected: all green, 0 failures.

- [ ] **Step 3: Build Debug**

Build the solution in Debug using the repository’s existing build method.

Expected: build succeeds with 0 compile errors.

- [ ] **Step 4: Verify the DLL actually changed**

Confirm:

```text
KMC.Plugin\bin\Debug\KMC.Plugin.dll
```

has a fresh build timestamp/output from this build.

- [ ] **Step 5: Assemble the ZIP**

ZIP root must mirror repository root and contain only 14.20.6 files plus README.

Do not include:

```text
bin/
obj/
.vs/
__pycache__/
.git/
unrelated audit outputs
```

except the built DLL is not placed in the repository-root ZIP unless the established KMC packaging convention explicitly includes it. The README must still direct the user to the Debug DLL replacement.

- [ ] **Step 6: Verify packaged contents**

List the ZIP and compare against the expected changed-file manifest.

Expected: no missing files, no unrelated files.

- [ ] **Step 7: Fresh package test**

Extract the ZIP over a copy of frozen 14.20.5 baseline and rerun:

```powershell
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v
```

and the relevant C# tests/build.

Expected: all pass from packaged contents.

- [ ] **Step 8: Commit packaging/docs**

```bash
git add README_14.20.6.txt docs/superpowers/specs/2026-09-04-kmc-14-20-6-unified-lighting-electrical-authority-design.md docs/superpowers/plans/2026-09-04-kmc-14-20-6-unified-lighting-electrical-authority.md
git commit -m "docs: add 14.20.6 lighting milestone"
```

Do not push.

---

### Task 4: Runtime Acceptance — External Lighting

**Files:**
- No source edits unless a runtime failure exposes a root cause. If a failure occurs, stop and return to systematic debugging + TDD.

**Interfaces:**
- Consumes: Debug `KMC.Plugin.dll` and updated `GameData/KMC`
- Produces: runtime PASS/FAIL evidence

- [ ] **Step 1: Install correctly**

1. Close KSP.
2. Build Debug.
3. Replace installed plugin with:

```text
KMC.Plugin\bin\Debug\KMC.Plugin.dll
```

4. Copy updated `GameData\KMC`.
5. Restart KSP fresh.

- [ ] **Step 2: Test external lights commanded ON**

At nominal power, command external lights ON.

Confirm physical external lights/window emissives are ON.

- [ ] **Step 3: Test single-main-bus failures**

Fail MAIN A only, then MAIN B only, one at a time.

Acceptance is based on actual ESS state shown by the KMC schematic:

```text
ESS energized >= 18 V => lights may remain available
ESS dead / < 18 V      => lights must be physically OFF
```

- [ ] **Step 4: Test total main-bus loss**

Fail MAIN A + MAIN B until KMC schematic shows:

```text
BUS_ESS = UNPOWERED
0.0 V
```

Confirm physical external lights/window emissives go OFF automatically.

- [ ] **Step 5: Test restore with crew command retained ON**

Clear failures.

Confirm physical lights return ON automatically without re-toggling the Light command.

- [ ] **Step 6: Test retained OFF command**

Command external lights OFF.

Collapse ESS and restore it.

Confirm lights remain OFF.

- [ ] **Step 7: Test fail-open lease**

Create an active KMC Lights inhibit, then stop Mission Control or otherwise let the lease expire.

Confirm normal KSP light authority returns automatically.

---

### Task 5: Runtime Acceptance — Internal IVA Lighting

**Files:**
- No source edits unless runtime failure exposes a root cause.

**Interfaces:**
- Consumes: generalized RPM/ASET lighting gate
- Produces: representative family validation

- [ ] **Step 1: Mk1 Cockpit regression**

Internal lighting ON:

```text
nominal ESS -> lit
ESS 0 V      -> dark
ESS restored -> lit
```

Then command internal lighting OFF, repeat power loss/restore, and confirm it stays OFF.

- [ ] **Step 2: Command capsule representative**

Test one of:

```text
Mk1 Pod
Mk1-3
```

with the same ON/loss/restore and OFF/loss/restore sequence.

- [ ] **Step 3: KV-series representative**

Test KV-1 or KV-2 using the same sequence.

- [ ] **Step 4: Mk2 aircraft representative**

Test Mk2 Spaceplane Cockpit or Mk2 Inline using the same sequence.

- [ ] **Step 5: Mk3**

Test Mk3 using the same sequence.

- [ ] **Step 6: Unknown/non-target fail-open smoke test**

Enter an IVA outside the supported DE list if one is readily available.

Confirm KMC does not forcibly darken its native lighting merely because KMC cannot positively identify/support it.

---

### Task 6: Final Verification and Freeze Preparation

**Files:**
- No source changes unless verification uncovers a defect.

- [ ] **Step 1: Run full automated tests fresh**

Run all C# tests and:

```powershell
python -m unittest discover -s Tools/IvaCoverageAudit/tests -v
```

Record exact counts and results.

- [ ] **Step 2: Build Debug fresh**

Run the full Debug build again.

Record success/failure.

- [ ] **Step 3: Review git diff**

Run:

```powershell
git status --short
git diff --stat
git diff
```

Confirm only 14.20.6 intended files changed.

- [ ] **Step 4: Do not push until runtime PASS**

Required runtime evidence:

```text
External light electrical loss/restore — PASS
External retained OFF state — PASS
External fail-open lease — PASS
Mk1 internal lighting — PASS
Command capsule internal lighting — PASS
KV internal lighting — PASS
Mk2 internal lighting — PASS
Mk3 internal lighting — PASS
```

- [ ] **Step 5: Push only after user approval**

After runtime PASS, user pushes to `master`.

- [ ] **Step 6: Verify pushed HEAD before freezing**

Because GitHub public history has shown stale caching, verify with:

```powershell
git rev-parse HEAD
git show --stat --oneline --decorate HEAD
```

Require:

```text
HEAD -> master
origin/master
origin/HEAD
```

all aligned to the same SHA.

Freeze 14.20.6 only after exact HEAD and diff verification.

## KSP Plugin DLL Required?

**YES**

14.20.6 changes runtime C# logic in Mission Control and the KSP RPM variable handler.

Required runtime install:

1. Build Debug.
2. Close KSP.
3. Replace installed plugin with `KMC.Plugin\bin\Debug\KMC.Plugin.dll`.
4. Copy updated `GameData\KMC`.
5. Restart KSP.
