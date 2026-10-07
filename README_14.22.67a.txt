KMC 14.22.67a - Coupled Production API Regression Fix

Test-only correction for the 14.22.67 NavigationTests reflection regression.
14.22.67 added a second production TrySolve overload for operator-selected
periapsis, making GetMethod("TrySolve") ambiguous. The test now requests the
exact original production signature explicitly.

No KMC production source, solver math, UI behavior, shared protocol, or plugin
code changes in this overlay.
