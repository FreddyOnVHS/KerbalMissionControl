KMC 14.22.65 — KSP-AUTHORITATIVE TERMINAL PERIAPSIS CORRECTION
===============================================================
Baseline: 14.22.63a / a26a2578ba88833200e498802378dbbd18b6190b
Overlay assumes local 14.22.64b production-authority changes are already present.

WHY THIS BUILD EXISTS
---------------------
14.22.64b proved the coupled P/N/R node was transmitted exactly and produced a
real Duna encounter, but KSP's patched-conic result reached ~562.1 km center
radius while the generic KMC target was ~640 km. The uplink was correct; the
remaining discrepancy is between KMC's analytic patched-conic model and KSP's
actual patched-conic implementation.

14.22.65 does NOT change the coupled interplanetary optimizer. Instead, when a
coupled-authority CREATE packet carries a requested target periapsis radius, the
KSP plugin performs a small local terminal correction using KSP's own patched-
conic solver as the final authority.

CHANGES
-------
* ManeuverUplinkPacket optionally carries DesiredPeriapsisRadiusMeters.
  - new 10-field packet accepted
  - old 7/8/9-field packets still parse
* Coupled production node creation sends the solver's requested Pe radius.
* KSP plugin performs a bounded local minimum-norm correction in node UT and
  radial/normal/prograde components.
* Each correction step is accepted only if:
  - the target encounter remains present, and
  - absolute KSP Pe error decreases.
* If correction fails or worsens the node, the original coupled node is kept.
* KSP node-state status reports terminal-corrected verification text.
* Transfer page replaces TARGET SOI with TARGET PE / ERR for coupled nodes so
  the KSP-authoritative terminal error is directly visible.

IMPORTANT
---------
THIS BUILD CHANGES KMC.Plugin CODE.
Rebuild KMC.Plugin.dll and replace the DLL in the KSP GameData installation
before runtime testing. Mission Control and the plugin must use the same updated
KMC.shared packet contract.

TEST
----
1. Apply overlay over local 14.22.64b tree.
2. Run: .\Tools\NavigationTests\Run-Tests.ps1
   Expected NavigationTests: 36 passed, 0 failed.
3. Rebuild KMC.Plugin.dll and install it into KSP.
4. Launch KSP + KMC, solve Kerbin -> Duna.
5. Confirm COUPLED ... / PRODUCTION and MANEUVER AUTHORITY.
6. CREATE KSP NODE.
7. Expected status after plugin correction:
   NODE VERIFIED
   KSP NODE MATCHES TERMINAL-CORRECTED PLAN
   ENCOUNTER YES
   TARGET PE ~640 km  ERR near 0
8. Compare KSP map periapsis altitude. For stock Duna a ~640 km center radius is
   ~320 km altitude.

DO NOT PUSH UNTIL RUNTIME VALIDATION PASSES.
