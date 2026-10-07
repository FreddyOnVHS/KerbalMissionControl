KMC 14.22.67b - Transfer Planner UI Cleanup

Base: 14.22.67a local overlay on frozen 14.22.66 / 0512205b0a152469072b9fbcac1ef7ba0dfd017a

Scope: UI/layout only. No solver math, authority, packet, or plugin changes.

Changes:
- Collapses target periapsis altitude controls onto one compact row.
- Shortens PE step labels to -100 / -10 / +10 / +100 with km shown in the PE readout.
- Hides Lambert/fallback diagnostic blocks during a healthy coupled-production solution.
- Re-labels Lambert diagnostics as FALLBACK when they are shown.
- Replaces the 7-line coupled production diagnostic block with a 4-line operator summary:
  * production/authority state
  * burn UT + P/N/R + total DV
  * target/predicted PE + error
  * source/split/target continuity summary
- Retains detailed optimizer diagnostics automatically when the coupled result is only a candidate.
- Reserves slightly more vertical space for the bottom NODE STATUS block.

No KMC.Plugin or KMC.shared source changes. No DLL replacement required in KSP.
