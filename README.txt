KMC PROPULSION PHASE 1 — PROJECT FIX

WHY THE BUILD FAILED

KMC.Plugin deliberately compiles shared contracts as linked source files so
KSP does not need a separate KMC.shared.dll in GameData.

TelemetryPacket.cs was linked, but the three topology model files were not.
The plugin therefore compiled VesselTopologyBuilder.cs without having:

KMC.Shared.Topology.VesselAttachmentType
KMC.Shared.Topology.VesselTopologyNode
KMC.Shared.Topology.VesselTopology

The KSP references were also hard-coded to a single D: drive path.

REPLACE

KMC.Plugin\KMC.Plugin.csproj
KMC.MissionControl\KMC.MissionControl.csproj

WHAT THE FIX DOES

1. Links all three shared topology model files into KMC.Plugin.
2. Links the same models into KMC.MissionControl for future rendering.
3. Keeps KMC.Plugin independent from KMC.shared.dll at KSP runtime.
4. Detects common C: and D: Steam KSP locations.
5. Supports a KSP_DIR environment variable for any custom installation.
6. Produces a clear build error when KSP assemblies cannot be found.

IF KSP IS IN A CUSTOM LOCATION

Open PowerShell in this extracted folder and run:

.\Set-KspDirectory.ps1 "E:\Games\Kerbal Space Program"

Use your real KSP installation directory.

Then close and reopen Visual Studio.

BUILD CLEANUP

1. Replace both project files.
2. Close Visual Studio.
3. Delete the solution .vs folder.
4. Delete bin and obj folders under all three projects.
5. Reopen the solution.
6. Select Release | Any CPU.
7. Rebuild Solution.

IF SYSTEM / SYSTEM.CORE STILL SHOW AS MISSING

Install or repair:

Visual Studio Installer
→ Modify
→ Individual components
→ .NET Framework 4.8 SDK
→ .NET Framework 4.8 targeting pack

Those framework references are independent of KSP.

GITHUB NOTE

The repository was readable, but the GitHub integration rejected the direct
write with HTTP 403, so these corrected files are supplied as a replacement
package.
