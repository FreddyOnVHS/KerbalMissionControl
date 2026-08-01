KMC Phase 3B Installation

1. Replace KMC.Plugin/CraftAnalyzer.cs with CraftAnalyzer.cs.
2. Create KMC.Plugin/Simulation.
3. Copy every .cs file from this package's Simulation folder into it.
4. Replace KMC.Plugin/KMC.Plugin.csproj with KMC.Plugin.csproj.
5. Rebuild in Release.
6. Copy KMC.Plugin.dll to GameData/KMC/Plugins.
7. Load a rocket on the pad and search KSP.log for:
   [KMC] Simulation model:
   [KMC] Sim engine
   Fuel graph:
