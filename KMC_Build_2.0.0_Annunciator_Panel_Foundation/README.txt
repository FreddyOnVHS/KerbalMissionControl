KMC Build 2.0.0 - Annunciator Panel Foundation

Replace:
KMC.MissionControl/Controls/MissionSummary.cs

No .csproj changes are required.

KSP plugin:
Do NOT rebuild or replace KMC.Plugin.dll.
Only rebuild KMC.MissionControl.

Purpose:
This first milestone replaces the persistent lower Mission Summary display
with an Apollo-style event/caution indicator panel foundation.

Included:
- Existing MissionSummary class name and UpdateTelemetry API are preserved.
- No MainForm changes are required.
- 24 fixed annunciators arranged in two rows of twelve.
- Recessed hardware-style lamp housings.
- Dark inactive lenses.
- Blue, green, amber, and red illuminated-lamp rendering.
- Subtle glow and mounting fasteners.
- Clickable ACK control.
- Clickable LAMP TEST control.
- Lamp test remains active for three seconds.
- Keyboard shortcut: T starts a lamp test.

Important:
No live event logic is connected in this build. All lamps remain dark during
normal operation. Use LAMP TEST to assess layout, colors, readability, spacing,
and brightness before Build 2.0.1 connects live indicators.

Recommended test:
1. Build and run KMC.
2. Confirm the lower panel fits at your normal resolution.
3. Click LAMP TEST.
4. Check every label for clipping or overlap.
5. Decide whether lamp size, labels, order, or brightness should change.
