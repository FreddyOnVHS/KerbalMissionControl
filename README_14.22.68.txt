KMC 14.22.68 - Transfer Execution Summary / Handoff

UI / mission-state build only.
No transfer solver math, authority, packet protocol, or KSP plugin behavior changed.

Adds a post-uplink operator summary:
- TRANSFER EXECUTION <target>
- node verification state/detail
- BURN IN using live KSP UT
- total maneuver DV
- encounter YES/NO
- target periapsis altitude
- KSP-assessed periapsis altitude and encounter UT
- NEXT ACTION guidance

Safety behavior:
- KMC does not assume a removed node means the burn was successfully executed.
  It displays VERIFY BURN / COAST STATUS instead.
- A past-due node is explicitly flagged for review.

Expected NavigationTests after applying over 14.22.67h:
40 passed, 0 failed
