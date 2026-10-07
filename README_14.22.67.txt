KMC 14.22.67 — User-Selectable Target Periapsis

Baseline:
  14.22.66 / 0512205b0a152469072b9fbcac1ef7ba0dfd017a

Purpose:
  Allow the transfer operator to choose target periapsis altitude above the
  live destination surface while preserving the proven coupled finite-SOI
  optimizer and KSP-authoritative terminal correction.

Changes:
  - Adds TARGET PE ALT control row to MAP / TRANSFER.
  - AUTO retains the proven generic target policy.
  - Manual controls adjust altitude by -100 km, -10 km, +10 km, +100 km.
  - Manual altitude is converted to center radius using live body radius.
  - CoupledFiniteSoiOptimizer accepts an optional desired periapsis radius.
  - The selected target is used by terminal B-plane/periapsis constraints and
    final assessment.
  - The existing maneuver packet carries the same requested Pe into KSP's
    authoritative terminal correction.
  - Manual Pe never silently falls back to a legacy/default-target solution;
    CREATE KSP NODE requires a valid coupled solution for the requested Pe.
  - AUTO remains backward compatible with the 14.22.66 behavior.

No KMC.Plugin or KMC.shared source changes are required in this overlay.
The plugin DLL from 14.22.65a remains valid.

Test expectation:
  NavigationTests should report 38 passed, 0 failed.
