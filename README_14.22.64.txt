KMC 14.22.64 — Coupled Finite-SOI Production Node Authority

Baseline: 14.22.63a / a26a2578ba88833200e498802378dbbd18b6190b

Purpose
- Promote the validated coupled finite-SOI interplanetary solution from shadow diagnostics to guarded CREATE KSP NODE authority.
- Do not change optimizer mathematics.
- Preserve existing Lambert/legacy production paths as fallbacks.

Production gate
The coupled solution is authoritative only when:
- feasibility pass succeeded;
- source exit is outbound;
- target entry is inbound;
- terminal stage reports B-PLANE / PE SOLVED;
- target encounter is predicted and is not a collision;
- source/split/target interface diagnostics are finite and within generic tolerances;
- requested periapsis and true B-plane magnitude errors are within the same body-scaled 0.2% / 1 km floor used by the optimizer;
- final burn UT and P/N/R delta-v are finite.

Authority order
1. Coupled finite-SOI final ejection.
2. Existing production Lambert/target-shooting ejection.
3. Legacy prograde-only fallback candidate.

UI
- A production-ready coupled solution displays COUPLED FINITE-SOI OPTIMIZER / PRODUCTION and MANEUVER AUTHORITY.
- Successful uplink reports COUPLED AUTHORITY UPLINK SENT.
- Coupled-created nodes are not eligible for the old node-refinement path.

KSP plugin DLL
- No plugin code changes in this build.
