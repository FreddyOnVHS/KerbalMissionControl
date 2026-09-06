KMC 14.21.6 CORRECTIVE — IVA Lighting Honors BRK_LIGHTING_ESS
================================================================

PURPOSE
-------
Fix the runtime gap discovered while validating 14.21.7:
BRK_LIGHTING_ESS correctly removes exterior-light authority, but supported
DE_IVA/ASET cockpit backlighting still follows broad ESS power truth only.

This corrective keeps the proven IVA prop path and adds the already-existing
KMC LIGHTS authority lease as an additional deny condition.

DESIGN
------
Supported IVA backlight ALLOW is now:

    ESS powered
    AND
    no active KMC SystemAuthorityKind.Lights inhibit lease

Fail-open behavior is preserved:
- unsupported/unknown IVA -> native ASET behavior
- missing/stale KMC status -> ALLOW (existing behavior)
- missing/stale system-authority receiver/lease -> no breaker deny

The crew's PERSISTENT_BackLight command is NOT changed.
No Unity renderer/material/light hacks are added.
No PartModule is newly disabled by this corrective.

ADD
---
Tools/ElectricalExpansion/apply_14_21_6_iva_lighting_authority_fix.py
Tools/ElectricalExpansion/tests/test_14_21_6_iva_lighting_authority_fix.py

REPLACE / MODIFY
----------------
KMC.Plugin/KmcSystemAuthorityReceiver.cs
  - add a read-only live authority-lease query
  - track the active receiver instance for that query
  - stale/missing evidence returns not inhibited (fail-open)

KMC.Plugin/KmcRpmLightingScopeVariableHandler.cs
  - retain the existing ESS powered gate
  - additionally deny IVA backlight while SystemAuthorityKind.Lights is inhibited

REMOVE
------
Nothing.

APPLY
-----
From the KMC repository root:

python Tools/ElectricalExpansion/apply_14_21_6_iva_lighting_authority_fix.py

FOCUSED TEST
------------
python -m pytest -q Tools/ElectricalExpansion/tests/test_14_21_6_iva_lighting_authority_fix.py

REGRESSION TEST
---------------
python -m pytest -q Tools/ElectricalExpansion/tests

BUILD / RUNTIME
---------------
1. Build KMC.Plugin in Visual Studio (Release, using your normal KSP references).
2. Confirm 0 build errors.
3. Deploy the rebuilt KMC.Plugin.dll using your normal test workflow.
4. Runtime checks:
   a. ESS alive + BRK_LIGHTING_ESS closed + crew internal lights ON -> IVA lights ON.
   b. Trip BRK_LIGHTING_ESS from F10 -> exterior lights OFF AND IVA lights OFF.
   c. Restore breaker -> crew's previous internal-light selection returns.
   d. MAIN A + MAIN B loss that kills ESS -> IVA lights OFF as before.
   e. Other ESS breaker trips do not kill IVA lighting.
   f. Verify exterior lighting remains correct.

DO NOT PUSH/FREEZE
------------------
Do not push or freeze until focused tests, full ElectricalExpansion regressions,
Visual Studio build, and KSP runtime tests all pass.

KSP Plugin DLL Required? YES
