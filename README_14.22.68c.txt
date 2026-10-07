KMC 14.22.68c - Transfer Execution Wording Cleanup

UI-only wording fix on top of 14.22.68b.

Changes:
- Removes duplicated "NODE" prefix from the execution summary.
- Example:
    NODE VERIFIED / KSP NODE MATCHES UPLINKED PLAN
  instead of:
    NODE NODE VERIFIED / KSP NODE MATCHES UPLINKED PLAN

No solver math, maneuver authority, packet protocol, plugin behavior, or test expectations changed.
Expected NavigationTests: 40 passed, 0 failed.
