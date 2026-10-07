KMC 14.22.67f — Human-Readable Mission Time

UI-only quality-of-life overlay on top of 14.22.67e.

Changes:
- Long transfer/countdown durations now display days, hours, and minutes.
- Example: 1472h 08m -> 61d 08h 08m.
- Durations under one day remain compact.
- Durations under one hour include seconds (for useful short countdown precision).
- No solver, maneuver authority, packet, or plugin changes.

Apply over 14.22.67e and run Tools/NavigationTests/Run-Tests.ps1.
