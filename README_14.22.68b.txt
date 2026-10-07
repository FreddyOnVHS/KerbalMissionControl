KMC 14.22.68b - Transfer UI State Cleanup

UI-only cleanup on top of 14.22.68a.

Changes:
- Clarifies Hohmann timing as a REFERENCE WINDOW / HOHMANN GUIDE once the
  coupled production solution is ready.
- Changes DEPART IN to REFERENCE DEPART IN for that guide value.
- Upper production status now changes to:
    STATUS NODE CREATED / VERIFYING
  or
    STATUS NODE CREATED / READY FOR BURN
  when the current KSP node matches the submitted candidate.
- Keeps AUTH COUPLED visible.
- Expands the existing 14.22.68 regression; test count remains unchanged.

No solver math, maneuver authority, packet protocol, or KSP plugin behavior changed.
Expected NavigationTests: 40 passed, 0 failed.
