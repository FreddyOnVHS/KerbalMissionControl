KMC 14.22.64a — Production Authority Regression Test Fix

Test-only compatibility update for the 14.22.64 production node authority change.

Changes:
- Updates NavigationTests ProductionAuthorityPacket reflection invocation for the new
  BuildProductionNodePacket signature.
- Adds explicit authority-priority coverage:
    1. Coupled finite-SOI P/N/R authority
    2. Existing Lambert P/N/R authority fallback
    3. Legacy prograde-only fallback
- No production KMC source changes.
- No KSP plugin DLL changes.

Expected NavigationTests result: 35 passed, 0 failed.
