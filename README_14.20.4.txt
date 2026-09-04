KMC 14.20.4 — COMMAND CAPSULE IVA POWER BATCH
=============================================

BASELINE
--------
Frozen 14.20.3 HEAD: 70a252c06b0d5a485e5c506ec86468df7eb82d93

SCOPE
-----
Extends the proven Mk1-reference IVA electrical profile pattern to seven
command-capsule / lander interiors:

  - DE_mk1pod_IVA
  - DE_mk2LanderCanInternal
  - DE_KV1_ASET_IVA_Internal
  - DE_KV2_ASET_IVA_Internal
  - DE_KV3_ASET_IVA_Internal
  - DE_MEM_ASET_IVA_Internal
  - DE_MK2POD_ASET_IVA_Internal

This milestone intentionally reuses ONLY already-proven KMC powered-prop
families. It does not introduce new renderer/material/display hacks and does
not patch command controls. Buttons/switches remain native and movable.

The 14.20.2 classifier identified additional Making History / KV-specific
warning, digital, and passive instruments as reusable-family candidates.
Those candidates remain native in 14.20.4 until their upstream power-off
mechanisms are individually verified. Do not treat their presence as a reason
to guess at renderer or animation behavior.

ADD
---
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_KV1.cfg
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_KV2.cfg
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_KV3.cfg
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_MEM.cfg
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_MK2POD.cfg
Tools\IvaCoverageAudit\tests\test_iva_batch_14_20_4.py
README_14.20.4.txt

REPLACE
-------
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_Mk1Pod.cfg
GameData\KMC\IVA\Profiles\DE_IVAExtension\KmcProfile_DE_Mk2LanderCan.cfg

REMOVE
------
Nothing.

AUTOMATED TEST
--------------
From the repository root:

  python -m unittest discover -s Tools/IvaCoverageAudit/tests -v

Expected after extraction:

  Ran 23 tests
  OK

RUNTIME TEST ORDER
------------------
Test one IVA at a time:

  1. Mk1 Command Pod
  2. Mk2 Lander Can
  3. KV-1
  4. KV-2
  5. KV-3
  6. MEM
  7. MK2 POD

For each IVA:

  A. Confirm normal-power display/instrument state.
  B. Fail MAIN A; verify A-side MFD / proven A-side props go dark.
  C. Restore MAIN A; verify recovery without re-toggling controls.
  D. Fail MAIN B; verify B-side MFD / proven B-side props go dark.
  E. Restore MAIN B; verify recovery.
  F. Fail both ESS feeds; verify ESS-assigned proven props go dark.
  G. Restore ESS; verify recovery.
  H. Confirm switches/buttons remain physically operable throughout.

MFD ASSIGNMENTS
---------------
Mk1 Pod:       40x20 #0 MAIN A, #1 MAIN B
Mk2 Lander:    40x20 #0 MAIN A, #1 MAIN B
KV-1/2/3:      40x20 #0 MAIN A, #1 MAIN B, 60x30 #0 ESS
MEM:           40x20 #0 MAIN A, #1 MAIN B, #2 MAIN A, 60x30 #0 ESS
MK2 POD:       40x20 #0 MAIN A, #1 MAIN B, #2 MAIN A, 60x30 #0 ESS

KSP PLUGIN DLL REQUIRED? NO
