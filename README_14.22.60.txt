KMC 14.22.60 - Coupled B-Plane / Periapsis Terminal Solve

SHADOW MODE ONLY. NO KSP NODE AUTHORITY.

Builds directly on the proven 14.22.59 free split heliocentric state solver.
Stage 1 is preserved as the finite-SOI feasibility solve.
Stage 2 starts only from a genuinely feasible inbound target encounter and:
- keeps source interface continuity active
- keeps split midpoint velocity continuity active
- keeps target interface continuity active
- adds signed requested periapsis-radius error as the terminal constraint
- keeps departure delta-v as the tie-break objective
- uses the same 8 coupled variables (burn UT, P/N/R, midpoint XYZ, arrival UT)
- introduces no destination-specific branches and no independent P/N/R search

Success diagnostic target:
FEAS PASS / B-PLANE / PE SOLVED
SOURCE IF ~0 / small velocity error
SPLIT VERR small
TARGET IF ~0 / IN
PE ERR <= body-scaled terminal tolerance

Do not push until the multi-body validation matrix is green.
