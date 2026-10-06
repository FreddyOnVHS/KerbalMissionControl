KMC 14.22.41 UI Refinement v3 — Font-Safe Reserved Layout
================================================================

This supersedes the earlier 14.22.41 UI overlap patches.

The file is rebuilt from the original 14.22.41 MapPage, not from the first
compact-layout patch.

WHAT WAS WRONG
--------------
The first overlap fix used 18-20 px vertical increments even though the
rendered KMC SmallFont is taller at the user's current display scale. That
caused line-on-line collisions.

FIX
---
Every TRANSFER row now uses actual rendered font height:

  transferFontHeight = SmallFont.GetHeight(Graphics)
  transferLineHeight = max(24, fontHeight + 5)

The upper diagnostic area also gets a hard bottom boundary:

  transferContentBottom = plannerPanel.Bottom - 235

Optional diagnostics are only drawn when enough room remains above that
boundary.

The node-status / KSP assessment block remains independently bottom anchored,
and a separator line is drawn above it.

IMPORTANT
---------
The legacy parking-ejection candidate is ALWAYS calculated even when its
verbose text is omitted for space. CREATE KSP NODE behavior is unchanged.

NO NAVIGATION MATH CHANGES
--------------------------
- Hohmann unchanged
- Lambert unchanged
- parking-aware search unchanged
- finite-SOI matching unchanged
- CREATE LAMBERT TEST NODE unchanged
- Duna encounter behavior unchanged
- plugin/shared protocol unchanged

LIVE CHECK
----------
Compile and reopen Duna TRANSFER.

Expected:
- no line overlaps anywhere
- clear readable finite-SOI diagnostics
- large blank safety gap before NODE STATUS if needed
- bottom KSP assessment remains readable
- buttons remain usable

After this visual check, continue with Eve.
