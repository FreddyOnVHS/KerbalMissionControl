KMC 14.22.67c — Transfer Planner Operator View

Base: 14.22.67b local overlay on frozen 14.22.66 baseline.

Scope: UI readability only. No solver math, node authority, packet, or plugin changes.

Changes:
- Replaces Hohmann/phase jargon in the normal view with a simple TRANSFER WINDOW summary.
- Replaces the production coupled diagnostic block with an operator-focused TRANSFER SOLUTION READY block.
- Shows burn countdown, total DV, explicit prograde/normal/radial components, target periapsis altitude, predicted altitude, and error.
- Shows STATUS READY TO CREATE NODE when coupled production authority is healthy.
- Hides finite-SOI and TARGET-SOI SHOOTING debug blocks while the coupled production solution is healthy.
- Retains detailed fallback/debug diagnostics automatically when coupled production authority is unavailable.
- No KMC.Plugin or KMC.shared changes; no DLL replacement required.
