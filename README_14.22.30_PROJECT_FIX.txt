KMC 14.22.30 — Project File Compile Fix
=========================================

Cause:
KMC.MissionControl uses a legacy .csproj with explicit <Compile Include=...>
entries. TransferPlannerManeuverUplink.cs existed on disk but was not listed
in the project file, causing CS0103 in MapPage.cs.

Fix:
Adds:
  <Compile Include="Transport\TransferPlannerManeuverUplink.cs" />

No runtime behavior changed.
No KSP plugin rebuild is required.
