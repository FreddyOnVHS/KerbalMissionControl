KMC 14.22.31 — KSP-Authoritative Transfer Assessment
======================================================

BASELINE
--------
Frozen master verified before build:
29e256cb9e7e9a9aba0976986af7ed86fa2f71a0

PURPOSE
-------
Establish the first trustworthy closed-loop refinement signal after the
14.22.30 transfer planner creates a real stock KSP maneuver node.

14.22.31 does NOT automatically modify the maneuver. Instead, KSP evaluates
the actual post-node patched-conic chain against the selected destination and
reports the result back to Mission Control.

WHY THIS BUILD EXISTS
---------------------
The normal LOCAL Orbit Map packet intentionally samples the current reference
body and its immediate local hierarchy. It is not a reliable source for a
heliocentric Kerbin->Duna miss-distance assessment.

Rather than reconstructing KSP's future solar trajectory in Mission Control,
14.22.31 asks the KSP plugin to evaluate the stock ManeuverNode.nextPatch chain
directly.

PROTOCOL
--------
ManeuverUplinkPacket remains KMC-MNV1 and is backward compatible:
- old 7-field maneuver commands still parse
- new 8-field commands append TargetBodyName

ManeuverNodeStatePacket also remains backward compatible:
- old 10-field state packets still parse
- new 16-field packets append:
  * TargetBodyName
  * TransferAssessmentAvailable
  * TargetEncounter
  * ClosestApproachMeters
  * ClosestApproachUniversalTimeSeconds
  * TargetSoiRadiusMeters

KSP ASSESSMENT
--------------
For the selected destination, the plugin walks the stock node.nextPatch chain.

Encounter authority:
- A target encounter is TRUE only when KSP creates a patch whose
  referenceBody is the selected target body.

Closest approach:
- On a target-body patch, distance is the vessel's KSP relative-position norm.
- On a patch sharing the target's parent (for example a Sun-referenced vessel
  patch versus Duna), KSP propagates both the vessel patch and the target body's
  stock orbit at the same UT.
- The plugin coarse-samples each comparable patch, then refines the best time
  window numerically.
- The result is recomputed if the player changes node UT or DV.

TRANSFER PAGE
-------------
After NODE VERIFIED the lower-right panel can now show:

  KSP TRANSFER ASSESSMENT  Duna
  ENCOUNTER       NO
  CLOSEST APPROACH ...
  TARGET SOI       ...
  OUTSIDE SOI      ...

If KSP actually produces a Duna SOI patch:

  ENCOUNTER       YES

No correction button is added yet. 14.22.31 establishes the error signal that
14.22.32 can use for controlled node refinement.

FILES
-----
KMC.shared/ManeuverUplinkPacket.cs
KMC.Plugin/ManeuverUplinkReceiver.cs
KMC.MissionControl/Engineering/ManeuverUplinkStatusStore.cs
KMC.MissionControl/Pages/MapPage.cs
Tools/OrbitMap/tests/test_14_22_31_ksp_authoritative_transfer_assessment.py
README_14.22.31.txt

BUILD / DEPLOY
--------------
This build changes the shared maneuver protocol source and the KSP plugin.

Rebuild:
1. KMC.shared
2. KMC.Plugin
3. KMC.MissionControl

Replace the KSP plugin DLL:
  GameData/KMC/Plugins/KMC.Plugin.dll

Ensure Mission Control is using the newly rebuilt KMC.shared.dll.

RUNTIME ACCEPTANCE
------------------
1. Use a near-circular equatorial Kerbin parking orbit.
2. Select Duna in TRANSFER.
3. Create one KSP node.
4. Confirm NODE VERIFIED and NODE CREATED still work.
5. Confirm KSP TRANSFER ASSESSMENT appears.
6. For the current approximate node, ENCOUNTER is expected to be NO unless KSP
   actually generates a Duna patch.
7. Confirm CLOSEST APPROACH and TARGET SOI are finite.
8. Compare the result qualitatively with KSP Map View.
9. Manually edit the KSP node slightly and verify the assessment changes after
   KSP reports CREW MODIFIED.
10. LOCAL MAP behavior remains unchanged.

KSP Plugin DLL Required? YES
Mission Control rebuild required? YES
KMC.shared.dll change required? YES


14.22.31 UI REFINEMENT
----------------------
The KSP transfer-assessment block now uses 24 px line spacing and starts
slightly higher in the node-status area. This prevents CLOSEST APPROACH,
TARGET SOI, and OUTSIDE SOI from overlapping at the current display font size.

No transfer calculations, KSP assessment logic, protocol fields, or maneuver
authority behavior changed in this refinement.


14.22.31 UI REFINEMENT v2
-------------------------
The node-status / KSP-assessment block is now vertically anchored from the top
edge of the NODE UPLINKED / NODE CREATED button area. The code computes the
number of rendered rows and positions the entire block so every line remains
above the button, including the longer CLOSEST APPROACH and OUTSIDE SOI rows.

No transfer calculations, KSP assessment logic, protocol fields, or maneuver
authority behavior changed in this refinement.


14.22.31 UI REFINEMENT v3
-------------------------
The node-status / transfer-assessment block now measures the actual SmallFont
render height and includes that height in the reserved block above the
NODE UPLINKED / NODE CREATED button. A fixed 10 px safety gap is preserved
between the last rendered assessment line and the button.

This replaces the earlier baseline-only positioning that could still allow the
font glyphs to extend into the button rectangle. No transfer calculations,
protocol behavior, KSP assessment logic, or maneuver authority changed.
