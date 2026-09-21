KMC 14.22.26 — Encounter Exit / Transition Polish
==================================================

BASELINE
--------
GitHub master verified before build:
b0f2ffe3c9591782c59066ff04cc23015f6f567a

Parent of baseline:
f10a3c280429a99ac60f62ae643f3309b915bd3b

PURPOSE
-------
Finish the MAP encounter-panel timing presentation by showing the end of the
child-body encounter patch without changing the proven patched-conic geometry.

CHANGES
-------
KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
- Uses the encounter patch EndUniversalTimeSeconds relative to current packet UT.
- If KSP reports the patch end transition as ESCAPE, the panel shows:
    EXIT T-HH:MM:SS
- If KSP reports another transition type, the panel uses the neutral form:
    END <TRANSITION> T-HH:MM:SS
  rather than incorrectly calling every patch end an SOI exit.
- If KSP supplies NextBodyName, the destination is appended:
    EXIT T-HH:MM:SS -> Kerbin
- Invalid/non-finite end UT remains omitted.
- Existing ENTRY, PE, and sampled CLOSEST presentation is preserved.
- Existing T-/T+ and multi-day formatting is reused.

IMPORTANT BEHAVIOR
------------------
The display remains KSP-authoritative. KMC does not calculate a new SOI exit,
transition, or patched-conic boundary. It presents the end UT and transition
already carried by the KSP-generated encounter patch.

UNCHANGED
---------
- Encounter geometry and moving child-body translation
- Closest-approach body placement
- Canonical coordinate transform
- Patch coloring (orange / purple / green)
- Telemetry rate and packet protocol
- Scene geometry caching / rebuild behavior
- KMC.Plugin
- KMC.shared

FILES IN THIS BUILD
-------------------
KMC.MissionControl/Rendering/OrbitMap/OrbitMapRenderer.cs
Tools/OrbitMap/tests/test_14_22_26_encounter_exit_transition.py
README_14.22.26.txt

BUILD / DEPLOY
--------------
1. Unzip this package.
2. Drag/drop the contents over the KerbalMissionControl repository root.
3. Overwrite the matching file when prompted.
4. Rebuild KMC.MissionControl in Visual Studio.
5. Launch the rebuilt Mission Control application.

No KSP-side DLL replacement is required for this build.

RUNTIME ACCEPTANCE TEST
-----------------------
Create/load a maneuver that produces a child-body encounter (for example Mun).
On the MAP page verify:

1. Existing encounter geometry still matches KSP.
2. Existing encounter PE altitude still agrees with KSP.
3. ENTRY and CLOSEST timing still behave as in 14.22.25.
4. For a normal flyby whose child patch ends with ESCAPE, an EXIT countdown is shown.
5. EXIT decreases as UT advances and changes to T+ after the event passes.
6. If a next reference body is supplied, the expected destination is displayed.
7. Patch colors remain orange first primary / purple encounter / green later primary.
8. Camera rotate/zoom does not cause orbit geometry rebuilds.
9. No regression is visible in Mun ENC placement or SOI visualization.

Do not report runtime PASS until these checks are performed in KSP/KMC.

KSP Plugin DLL Required? NO
Mission Control rebuild required? YES
KMC.shared.dll change required? NO
