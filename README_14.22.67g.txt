KMC 14.22.67g — KMC-Wide Human-Readable Mission Time

Purpose
- Standardize player-facing mission time displays across KMC.
- Keep all internal timing math, telemetry packets, UT/MET storage, scheduling, and navigation calculations in seconds exactly as before.

Display policy
- >= 1 day:  1d 01h 01m 01s
- >= 1 hour: 1h 01m 01s
- >= 1 min:  1m 01s
- < 1 min:   seconds (precision preserved where already useful)
- Absolute KSP UT/MET values are still labeled UT/MET, but rendered as elapsed days/hours/minutes/seconds instead of raw second counts.

Updated player-facing areas
- MAP transfer planner: transfer waits, flight times, burn timing, fallback diagnostics, source/arrival UTs, closest-approach UT.
- MANEUVER page: node MET/UT, time-to-node, ignition MET/lead, burn duration, predicted period, queue UT/countdowns.
- GUIDANCE page: time-to-node, ignition timing, burn duration and planner countdown strings.
- ORBIT page: time-to-apsides, orbital period, ignition/burn timing.
- ASCENT page: MET footer and prediction timing.
- POWER engineering displays: endurance and trend duration formatting.

Safety
- No solver math changes.
- No node-authority changes.
- No packet protocol changes.
- No KSP.Plugin changes.
- Real-world wall-clock UTC timestamps used for logs/events remain wall-clock timestamps.
