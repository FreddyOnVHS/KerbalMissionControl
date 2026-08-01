using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace KMC.Plugin
{
    internal static class CraftAnalyzer
    {
        public static CraftAnalysis Analyze(Vessel vessel)
        {
            CraftAnalysis analysis = new CraftAnalysis();

            if (vessel == null)
            {
                return analysis;
            }

            analysis.VesselName = vessel.vesselName ?? string.Empty;
            analysis.VesselId = vessel.id.ToString();
            analysis.PartCount = vessel.parts != null ? vessel.parts.Count : 0;

            Dictionary<int, StageAnalysis> stages =
                new Dictionary<int, StageAnalysis>();

            if (vessel.parts == null)
            {
                return analysis;
            }

            foreach (Part part in vessel.parts)
            {
                if (part == null)
                {
                    continue;
                }

                int stageNumber = Math.Max(0, part.inverseStage);
                StageAnalysis stage = GetOrCreateStage(stages, stageNumber);

                stage.PartCount++;

                double dryMass = GetPartDryMassTonnes(part);
                double resourceMass = GetPartResourceMassTonnes(part);

                stage.DryMassTonnes += dryMass;
                stage.ResourceMassTonnes += resourceMass;
                stage.WetMassTonnes += dryMass + resourceMass;

                ReadPartResources(part, stage);
                ReadPartModules(part, stage);
            }

            List<int> stageNumbers = new List<int>(stages.Keys);
            stageNumbers.Sort(delegate(int left, int right)
            {
                return right.CompareTo(left);
            });

            foreach (int stageNumber in stageNumbers)
            {
                StageAnalysis stage = stages[stageNumber];

                FinalizeStage(vessel, stage);

                analysis.Stages.Add(stage);
                analysis.TotalDryMassTonnes += stage.DryMassTonnes;
                analysis.TotalResourceMassTonnes += stage.ResourceMassTonnes;
                analysis.EngineCount += stage.EngineCount;

                if (stage.EngineCount > 0)
                {
                    analysis.PropulsiveStageCount++;

                    if (stage.StageNumber >
                        analysis.InitialPropulsiveStageNumber)
                    {
                        analysis.InitialPropulsiveStageNumber =
                            stage.StageNumber;

                        analysis.InitialSeaLevelThrustKilonewtons =
                            stage.SeaLevelThrustKilonewtons;

                        analysis.InitialVacuumThrustKilonewtons =
                            stage.VacuumThrustKilonewtons;
                    }
                }
            }

            analysis.TotalMassTonnes =
                analysis.TotalDryMassTonnes +
                analysis.TotalResourceMassTonnes;

            analysis.MassClass =
                ClassifyMass(
                    analysis.TotalMassTonnes);

            analysis.InitialSeaLevelThrustToWeightRatio =
                CalculateInitialThrustToWeightRatio(
                    vessel,
                    analysis.TotalMassTonnes,
                    analysis.InitialSeaLevelThrustKilonewtons);

            analysis.ThrustClass =
                ClassifyThrust(
                    analysis.InitialSeaLevelThrustToWeightRatio);

            AnalyzeStageTopology(
                vessel,
                analysis);

            analysis.SimulationModel =
                Simulation.CraftSimulationBuilder.Build(
                    vessel);

            return analysis;
        }

        public static string CreateDiagnosticReport(CraftAnalysis analysis)
        {
            if (analysis == null)
            {
                return "[KMC] Craft analysis unavailable.";
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("[KMC] Craft Analyzer Phase 3B");
            builder.Append("[KMC] Vessel: ");
            builder.AppendLine(
                string.IsNullOrEmpty(analysis.VesselName)
                    ? "---"
                    : analysis.VesselName);

            builder.AppendFormat(
                "[KMC] Parts: {0}  Engines: {1}  Propulsive stages: {2}",
                analysis.PartCount,
                analysis.EngineCount,
                analysis.PropulsiveStageCount);

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Launch mass: {0:0.000} t  Dry: {1:0.000} t  Resources: {2:0.000} t  Mass class: {3}",
                analysis.TotalMassTonnes,
                analysis.TotalDryMassTonnes,
                analysis.TotalResourceMassTonnes,
                FormatMassClass(
                    analysis.MassClass));

            builder.AppendLine();

            builder.AppendFormat(
                "[KMC] Initial propulsive stage: {0:00}  SL thrust: {1:0.0} kN  VAC thrust: {2:0.0} kN  Initial SL TWR: {3:0.00}  Thrust class: {4}",
                analysis.InitialPropulsiveStageNumber,
                analysis.InitialSeaLevelThrustKilonewtons,
                analysis.InitialVacuumThrustKilonewtons,
                analysis.InitialSeaLevelThrustToWeightRatio,
                FormatThrustClass(
                    analysis.ThrustClass));

            builder.AppendLine();

            builder.AppendLine(
                "[KMC] Stage topology:");

            foreach (StageTopologyEvent topologyEvent in
                analysis.StageTopology)
            {
                builder.AppendFormat(
                    "[KMC] Event {0:00}: pre={1:0.000} t, decouplers={2}, discarded mass={3:0.000} t, retained={4:0.000} t, engines igniting={5}, continuing={6}, discarded engines={7}, active={8}, active SL thrust={9:0.0} kN, active SL TWR={10:0.00}, reachable propellant={11:0.000} t, resource parts={12}, fuel fallbacks={13}, SL flow={14:0.0000} t/s, VAC flow={15:0.0000} t/s, burn SL={16:0.0} s, burn VAC={17:0.0} s, burnout={18:0.000} t, burnout SL TWR={19:0.00}, dV SL={20:0} m/s, dV VAC={21:0} m/s, limiting={22}, unresolved={23}",
                    topologyEvent.StageNumber,
                    topologyEvent.PreEventMassTonnes,
                    topologyEvent.DecouplerCount,
                    topologyEvent.DiscardedMassTonnes,
                    topologyEvent.RetainedMassTonnes,
                    topologyEvent.IgnitingEngineCount,
                    topologyEvent.ContinuingEngineCount,
                    topologyEvent.DiscardedEngineCount,
                    topologyEvent.ActiveEngineCount,
                    topologyEvent.ActiveSeaLevelThrustKilonewtons,
                    topologyEvent.ActiveSeaLevelThrustToWeightRatio,
                    topologyEvent.AvailablePropellantMassTonnes,
                    topologyEvent.ReachableResourcePartCount,
                    topologyEvent.FuelNetworkFallbackCount,
                    topologyEvent.SeaLevelMassFlowTonnesPerSecond,
                    topologyEvent.VacuumMassFlowTonnesPerSecond,
                    topologyEvent.EstimatedSeaLevelBurnSeconds,
                    topologyEvent.EstimatedVacuumBurnSeconds,
                    topologyEvent.EstimatedBurnoutMassTonnes,
                    topologyEvent.BurnoutSeaLevelThrustToWeightRatio,
                    topologyEvent.EstimatedSeaLevelDeltaVMetresPerSecond,
                    topologyEvent.EstimatedVacuumDeltaVMetresPerSecond,
                    string.IsNullOrEmpty(topologyEvent.LimitingPropellant)
                        ? "---"
                        : topologyEvent.LimitingPropellant,
                    topologyEvent.UnresolvedDecouplerCount);

                builder.AppendLine();
            }

            if (analysis.SimulationModel != null)
            {
                builder.Append(
                    Simulation.CraftSimulationReporter
                        .CreateReport(
                            analysis.SimulationModel));
            }

            builder.AppendLine(
                "[KMC] Assignment groups:");

            foreach (StageAnalysis stage in analysis.Stages)
            {
                builder.AppendFormat(
                    "[KMC] Stage {0:00}: parts={1}, engines={2}, wet={3:0.000} t, dry={4:0.000} t, prop={5:0.000} t, SL thrust={6:0.0} kN, VAC thrust={7:0.0} kN, SL Isp={8:0.0} s, VAC Isp={9:0.0} s, SL TWR={10:0.00}",
                    stage.StageNumber,
                    stage.PartCount,
                    stage.EngineCount,
                    stage.WetMassTonnes,
                    stage.DryMassTonnes,
                    stage.ResourceMassTonnes,
                    stage.SeaLevelThrustKilonewtons,
                    stage.VacuumThrustKilonewtons,
                    stage.SeaLevelSpecificImpulse,
                    stage.VacuumSpecificImpulse,
                    stage.SeaLevelThrustToWeightRatio);

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private static StageAnalysis GetOrCreateStage(
            IDictionary<int, StageAnalysis> stages,
            int stageNumber)
        {
            StageAnalysis stage;

            if (!stages.TryGetValue(stageNumber, out stage))
            {
                stage = new StageAnalysis
                {
                    StageNumber = stageNumber
                };

                stages.Add(stageNumber, stage);
            }

            return stage;
        }

        private static double GetPartDryMassTonnes(Part part)
        {
            if (part == null)
            {
                return 0.0;
            }

            double dryMass = part.mass;

            if (part.Modules != null)
            {
                foreach (PartModule module in part.Modules)
                {
                    IPartMassModifier modifier =
                        module as IPartMassModifier;

                    if (modifier == null)
                    {
                        continue;
                    }

                    try
                    {
                        dryMass += modifier.GetModuleMass(
                            part.mass,
                            ModifierStagingSituation.CURRENT);
                    }
                    catch
                    {
                    }
                }
            }

            return SanitizeNonNegative(dryMass);
        }

        private static double GetPartResourceMassTonnes(Part part)
        {
            if (part == null || part.Resources == null)
            {
                return 0.0;
            }

            double mass = 0.0;

            foreach (PartResource resource in part.Resources)
            {
                if (resource == null || resource.info == null)
                {
                    continue;
                }

                double resourceMass =
                    resource.amount * resource.info.density;

                if (IsFinite(resourceMass) && resourceMass > 0.0)
                {
                    mass += resourceMass;
                }
            }

            return mass;
        }

        private static void ReadPartResources(
            Part part,
            StageAnalysis stage)
        {
            if (part == null ||
                stage == null ||
                part.Resources == null)
            {
                return;
            }

            foreach (PartResource resource in part.Resources)
            {
                if (resource == null || resource.info == null)
                {
                    continue;
                }

                string name = resource.info.name ?? string.Empty;

                if (string.Equals(name, "LiquidFuel", StringComparison.Ordinal))
                {
                    stage.LiquidFuelAmount += SanitizeNonNegative(resource.amount);
                    stage.LiquidFuelCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "Oxidizer", StringComparison.Ordinal))
                {
                    stage.OxidizerAmount += SanitizeNonNegative(resource.amount);
                    stage.OxidizerCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "SolidFuel", StringComparison.Ordinal))
                {
                    stage.SolidFuelAmount += SanitizeNonNegative(resource.amount);
                    stage.SolidFuelCapacity += SanitizeNonNegative(resource.maxAmount);
                }
                else if (string.Equals(name, "MonoPropellant", StringComparison.Ordinal))
                {
                    stage.MonopropellantAmount += SanitizeNonNegative(resource.amount);
                    stage.MonopropellantCapacity += SanitizeNonNegative(resource.maxAmount);
                }
            }
        }

        private static void ReadPartModules(
            Part part,
            StageAnalysis stage)
        {
            if (part == null ||
                stage == null ||
                part.Modules == null)
            {
                return;
            }

            foreach (PartModule module in part.Modules)
            {
                if (module == null)
                {
                    continue;
                }

                ModuleEngines engine = module as ModuleEngines;

                if (engine != null)
                {
                    ReadEngine(engine, stage);
                    continue;
                }

                string moduleName = module.moduleName ?? string.Empty;

                if (moduleName.IndexOf(
                        "Decouple",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    stage.DecouplerCount++;
                }
            }
        }

        private static void ReadEngine(
            ModuleEngines engine,
            StageAnalysis stage)
        {
            stage.EngineCount++;

            double thrustLimit =
                Clamp(engine.thrustPercentage / 100.0, 0.0, 1.0);

            double vacuumThrust =
                SanitizeNonNegative(engine.maxThrust) * thrustLimit;

            double seaLevelIsp =
                EvaluateSpecificImpulse(engine, 1.0f);

            double vacuumIsp =
                EvaluateSpecificImpulse(engine, 0.0f);

            stage.SeaLevelThrustKilonewtons +=
                ConvertVacuumThrustToAmbientThrust(
                    vacuumThrust,
                    vacuumIsp,
                    seaLevelIsp);

            stage.VacuumThrustKilonewtons += vacuumThrust;

            if (seaLevelIsp > 0.0)
            {
                stage.SeaLevelSpecificImpulseTotal += seaLevelIsp;
                stage.SeaLevelSpecificImpulseSamples++;
            }

            if (vacuumIsp > 0.0)
            {
                stage.VacuumSpecificImpulseTotal += vacuumIsp;
                stage.VacuumSpecificImpulseSamples++;
            }
        }

        private static double ConvertVacuumThrustToAmbientThrust(
            double vacuumThrust,
            double vacuumIsp,
            double ambientIsp)
        {
            if (vacuumThrust <= 0.0 ||
                vacuumIsp <= 0.0 ||
                ambientIsp <= 0.0)
            {
                return 0.0;
            }

            return vacuumThrust * ambientIsp / vacuumIsp;
        }

        private static double EvaluateSpecificImpulse(
            ModuleEngines engine,
            float pressureAtmospheres)
        {
            if (engine == null || engine.atmosphereCurve == null)
            {
                return 0.0;
            }

            return SanitizeNonNegative(
                engine.atmosphereCurve.Evaluate(pressureAtmospheres));
        }

        private static void FinalizeStage(
            Vessel vessel,
            StageAnalysis stage)
        {
            if (stage.SeaLevelSpecificImpulseSamples > 0)
            {
                stage.SeaLevelSpecificImpulse =
                    stage.SeaLevelSpecificImpulseTotal /
                    stage.SeaLevelSpecificImpulseSamples;
            }

            if (stage.VacuumSpecificImpulseSamples > 0)
            {
                stage.VacuumSpecificImpulse =
                    stage.VacuumSpecificImpulseTotal /
                    stage.VacuumSpecificImpulseSamples;
            }

            double gravity = GetSurfaceGravity(vessel);

            if (stage.WetMassTonnes > 0.0 && gravity > 0.0)
            {
                stage.SeaLevelThrustToWeightRatio =
                    stage.SeaLevelThrustKilonewtons /
                    (stage.WetMassTonnes * gravity);
            }
        }

        private static double GetSurfaceGravity(Vessel vessel)
        {
            if (vessel == null || vessel.mainBody == null)
            {
                return 9.80665;
            }

            double gravity = vessel.mainBody.GeeASL * 9.80665;

            if (!IsFinite(gravity) || gravity <= 0.0)
            {
                return 9.80665;
            }

            return gravity;
        }



        private static void AnalyzeStageTopology(
            Vessel vessel,
            CraftAnalysis analysis)
        {
            if (vessel == null ||
                analysis == null ||
                vessel.parts == null)
            {
                return;
            }

            HashSet<Part> activeParts =
                new HashSet<Part>();

            foreach (Part part in vessel.parts)
            {
                if (part != null)
                {
                    activeParts.Add(part);
                }
            }

            List<int> eventStages =
                CollectEventStageNumbers(activeParts);

            HashSet<ModuleEngines> activeEngines =
                new HashSet<ModuleEngines>();

            double surfaceGravity =
                GetSurfaceGravity(vessel);

            foreach (int stageNumber in eventStages)
            {
                StageTopologyEvent topologyEvent =
                    new StageTopologyEvent
                    {
                        StageNumber = stageNumber,
                        PreEventMassTonnes =
                            GetPartsMassTonnes(activeParts)
                    };

                /*
                 * KSP activates every icon assigned to a staging event
                 * together. When a decoupler and an upper-stage engine
                 * share an inverseStage value, the engine belongs to the
                 * retained vehicle and should be evaluated after the
                 * separation topology has been applied.
                 */
                HashSet<Part> detachedParts =
                    FindDetachedPartsForStage(
                        vessel,
                        activeParts,
                        stageNumber,
                        topologyEvent);

                topologyEvent.DiscardedMassTonnes =
                    GetPartsMassTonnes(detachedParts);

                RemoveDetachedEngines(
                    activeEngines,
                    detachedParts,
                    topologyEvent);

                foreach (Part detachedPart in detachedParts)
                {
                    activeParts.Remove(detachedPart);
                }

                topologyEvent.RetainedMassTonnes =
                    GetPartsMassTonnes(activeParts);

                topologyEvent.IgnitionMassTonnes =
                    topologyEvent.RetainedMassTonnes;

                topologyEvent.ContinuingEngineCount =
                    activeEngines.Count;

                ReadIgnitingEngines(
                    activeParts,
                    activeEngines,
                    stageNumber,
                    topologyEvent);

                ReadActiveEnginePerformance(
                    activeEngines,
                    topologyEvent);

                AnalyzeBurnPerformance(
                    activeParts,
                    activeEngines,
                    surfaceGravity,
                    topologyEvent);

                if (topologyEvent.IgnitionMassTonnes > 0.0 &&
                    surfaceGravity > 0.0)
                {
                    topologyEvent
                        .IgnitionSeaLevelThrustToWeightRatio =
                        topologyEvent.SeaLevelThrustKilonewtons /
                        (topologyEvent.IgnitionMassTonnes *
                         surfaceGravity);

                    topologyEvent
                        .ActiveSeaLevelThrustToWeightRatio =
                        topologyEvent
                            .ActiveSeaLevelThrustKilonewtons /
                        (topologyEvent.IgnitionMassTonnes *
                         surfaceGravity);
                }

                analysis.StageTopology.Add(topologyEvent);
            }
        }

        private static List<int> CollectEventStageNumbers(
            IEnumerable<Part> parts)
        {
            HashSet<int> stageNumbers =
                new HashSet<int>();

            foreach (Part part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                if (!PartHasEngine(part) &&
                    !PartHasDecoupler(part))
                {
                    continue;
                }

                stageNumbers.Add(
                    Math.Max(0, part.inverseStage));
            }

            List<int> orderedStages =
                new List<int>(stageNumbers);

            orderedStages.Sort(
                delegate(int left, int right)
                {
                    return right.CompareTo(left);
                });

            return orderedStages;
        }

        private static void ReadIgnitingEngines(
            IEnumerable<Part> activeParts,
            ISet<ModuleEngines> activeEngines,
            int stageNumber,
            StageTopologyEvent topologyEvent)
        {
            foreach (Part part in activeParts)
            {
                if (part == null ||
                    part.inverseStage != stageNumber ||
                    part.Modules == null)
                {
                    continue;
                }

                foreach (PartModule module in part.Modules)
                {
                    ModuleEngines engine =
                        module as ModuleEngines;

                    if (engine == null ||
                        activeEngines.Contains(engine))
                    {
                        continue;
                    }

                    activeEngines.Add(engine);
                    topologyEvent.IgnitingEngineCount++;

                    EnginePerformance performance =
                        ReadEnginePerformance(engine);

                    topologyEvent
                        .SeaLevelThrustKilonewtons +=
                        performance
                            .SeaLevelThrustKilonewtons;

                    topologyEvent
                        .VacuumThrustKilonewtons +=
                        performance
                            .VacuumThrustKilonewtons;
                }
            }
        }

        private static void RemoveDetachedEngines(
            ISet<ModuleEngines> activeEngines,
            ISet<Part> detachedParts,
            StageTopologyEvent topologyEvent)
        {
            if (activeEngines == null ||
                detachedParts == null ||
                detachedParts.Count == 0)
            {
                return;
            }

            List<ModuleEngines> enginesToRemove =
                new List<ModuleEngines>();

            foreach (ModuleEngines engine in activeEngines)
            {
                if (engine == null ||
                    engine.part == null ||
                    detachedParts.Contains(engine.part))
                {
                    enginesToRemove.Add(engine);
                }
            }

            foreach (ModuleEngines engine in enginesToRemove)
            {
                activeEngines.Remove(engine);
                topologyEvent.DiscardedEngineCount++;
            }
        }

        private static void ReadActiveEnginePerformance(
            IEnumerable<ModuleEngines> activeEngines,
            StageTopologyEvent topologyEvent)
        {
            foreach (ModuleEngines engine in activeEngines)
            {
                if (engine == null)
                {
                    continue;
                }

                EnginePerformance performance =
                    ReadEnginePerformance(engine);

                topologyEvent.ActiveEngineCount++;

                topologyEvent
                    .ActiveSeaLevelThrustKilonewtons +=
                    performance
                        .SeaLevelThrustKilonewtons;

                topologyEvent
                    .ActiveVacuumThrustKilonewtons +=
                    performance
                        .VacuumThrustKilonewtons;
            }
        }

        private static EnginePerformance ReadEnginePerformance(
            ModuleEngines engine)
        {
            EnginePerformance performance =
                new EnginePerformance();

            if (engine == null)
            {
                return performance;
            }

            double thrustLimit =
                Clamp(
                    engine.thrustPercentage / 100.0,
                    0.0,
                    1.0);

            double vacuumThrust =
                SanitizeNonNegative(engine.maxThrust) *
                thrustLimit;

            double seaLevelIsp =
                EvaluateSpecificImpulse(engine, 1.0f);

            double vacuumIsp =
                EvaluateSpecificImpulse(engine, 0.0f);

            performance.VacuumThrustKilonewtons =
                vacuumThrust;

            performance.SeaLevelThrustKilonewtons =
                ConvertVacuumThrustToAmbientThrust(
                    vacuumThrust,
                    vacuumIsp,
                    seaLevelIsp);

            performance.SeaLevelSpecificImpulse =
                seaLevelIsp;

            performance.VacuumSpecificImpulse =
                vacuumIsp;

            return performance;
        }


        private static void AnalyzeBurnPerformance(
            IEnumerable<Part> activeParts,
            IEnumerable<ModuleEngines> activeEngines,
            double surfaceGravity,
            StageTopologyEvent topologyEvent)
        {
            if (topologyEvent == null ||
                topologyEvent.ActiveEngineCount <= 0 ||
                topologyEvent.IgnitionMassTonnes <= 0.0)
            {
                return;
            }

            ReachableResourceInventory reachableInventory =
                CollectReachableResourceMass(
                    activeParts,
                    activeEngines);

            IDictionary<string, double> availableMassByResource =
                reachableInventory.MassByResource;

            topologyEvent.ReachableResourcePartCount =
                reachableInventory.ResourcePartCount;

            topologyEvent.FuelNetworkFallbackCount =
                reachableInventory.FallbackCount;

            Dictionary<string, double> seaLevelFlowByResource =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);

            Dictionary<string, double> vacuumFlowByResource =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);

            double seaLevelMassFlow = 0.0;
            double vacuumMassFlow = 0.0;

            foreach (ModuleEngines engine in activeEngines)
            {
                if (engine == null)
                {
                    continue;
                }

                EnginePerformance performance =
                    ReadEnginePerformance(engine);

                double engineSeaLevelFlow =
                    CalculateMassFlowTonnesPerSecond(
                        performance.SeaLevelThrustKilonewtons,
                        performance.SeaLevelSpecificImpulse);

                double engineVacuumFlow =
                    CalculateMassFlowTonnesPerSecond(
                        performance.VacuumThrustKilonewtons,
                        performance.VacuumSpecificImpulse);

                seaLevelMassFlow += engineSeaLevelFlow;
                vacuumMassFlow += engineVacuumFlow;

                AddEnginePropellantFlows(
                    engine,
                    engineSeaLevelFlow,
                    seaLevelFlowByResource);

                AddEnginePropellantFlows(
                    engine,
                    engineVacuumFlow,
                    vacuumFlowByResource);
            }

            topologyEvent.SeaLevelMassFlowTonnesPerSecond =
                seaLevelMassFlow;

            topologyEvent.VacuumMassFlowTonnesPerSecond =
                vacuumMassFlow;

            topologyEvent.EffectiveSeaLevelSpecificImpulse =
                CalculateEffectiveSpecificImpulse(
                    topologyEvent.ActiveSeaLevelThrustKilonewtons,
                    seaLevelMassFlow);

            topologyEvent.EffectiveVacuumSpecificImpulse =
                CalculateEffectiveSpecificImpulse(
                    topologyEvent.ActiveVacuumThrustKilonewtons,
                    vacuumMassFlow);

            BurnLimit seaLevelLimit =
                CalculateBurnLimit(
                    availableMassByResource,
                    seaLevelFlowByResource);

            BurnLimit vacuumLimit =
                CalculateBurnLimit(
                    availableMassByResource,
                    vacuumFlowByResource);

            topologyEvent.EstimatedSeaLevelBurnSeconds =
                seaLevelLimit.DurationSeconds;

            topologyEvent.EstimatedVacuumBurnSeconds =
                vacuumLimit.DurationSeconds;

            topologyEvent.LimitingPropellant =
                !string.IsNullOrEmpty(
                    seaLevelLimit.ResourceName)
                    ? seaLevelLimit.ResourceName
                    : vacuumLimit.ResourceName;

            double usablePropellantMass =
                CalculateConsumedMass(
                    seaLevelFlowByResource,
                    seaLevelLimit.DurationSeconds);

            if (usablePropellantMass <= 0.0)
            {
                usablePropellantMass =
                    CalculateConsumedMass(
                        vacuumFlowByResource,
                        vacuumLimit.DurationSeconds);
            }

            usablePropellantMass =
                Math.Min(
                    usablePropellantMass,
                    topologyEvent.IgnitionMassTonnes);

            topologyEvent.AvailablePropellantMassTonnes =
                usablePropellantMass;

            topologyEvent.EstimatedBurnoutMassTonnes =
                Math.Max(
                    0.001,
                    topologyEvent.IgnitionMassTonnes -
                    usablePropellantMass);

            if (surfaceGravity > 0.0)
            {
                topologyEvent
                    .BurnoutSeaLevelThrustToWeightRatio =
                    topologyEvent
                        .ActiveSeaLevelThrustKilonewtons /
                    (topologyEvent
                         .EstimatedBurnoutMassTonnes *
                     surfaceGravity);
            }

            topologyEvent
                .EstimatedSeaLevelDeltaVMetresPerSecond =
                CalculateDeltaV(
                    topologyEvent
                        .EffectiveSeaLevelSpecificImpulse,
                    topologyEvent.IgnitionMassTonnes,
                    topologyEvent.EstimatedBurnoutMassTonnes);

            topologyEvent
                .EstimatedVacuumDeltaVMetresPerSecond =
                CalculateDeltaV(
                    topologyEvent
                        .EffectiveVacuumSpecificImpulse,
                    topologyEvent.IgnitionMassTonnes,
                    topologyEvent.EstimatedBurnoutMassTonnes);
        }


        private static ReachableResourceInventory
            CollectReachableResourceMass(
                IEnumerable<Part> activeParts,
                IEnumerable<ModuleEngines> activeEngines)
        {
            ReachableResourceInventory inventory =
                new ReachableResourceInventory();

            HashSet<Part> activePartSet =
                activeParts != null
                    ? new HashSet<Part>(activeParts)
                    : new HashSet<Part>();

            HashSet<PartResource> uniqueResources =
                new HashSet<PartResource>();

            foreach (ModuleEngines engine in activeEngines)
            {
                if (engine == null ||
                    engine.part == null ||
                    engine.propellants == null)
                {
                    continue;
                }

                foreach (Propellant propellant in
                    engine.propellants)
                {
                    if (propellant == null ||
                        string.IsNullOrEmpty(
                            propellant.name))
                    {
                        continue;
                    }

                    List<PartResource> connectedResources =
                        new List<PartResource>();

                    bool resolved =
                        TryGetConnectedResources(
                            engine.part,
                            propellant,
                            connectedResources);

                    if (!resolved)
                    {
                        inventory.FallbackCount++;

                        AddFallbackResources(
                            activePartSet,
                            propellant.name,
                            connectedResources);
                    }

                    foreach (PartResource resource in
                        connectedResources)
                    {
                        if (resource == null ||
                            resource.info == null ||
                            resource.part == null ||
                            !activePartSet.Contains(
                                resource.part))
                        {
                            continue;
                        }

                        uniqueResources.Add(
                            resource);
                    }
                }
            }

            HashSet<Part> resourceParts =
                new HashSet<Part>();

            foreach (PartResource resource in
                uniqueResources)
            {
                string resourceName =
                    resource.info.name ??
                    string.Empty;

                double resourceMass =
                    SanitizeNonNegative(
                        resource.amount *
                        resource.info.density);

                AddValue(
                    inventory.MassByResource,
                    resourceName,
                    resourceMass);

                if (resource.part != null)
                {
                    resourceParts.Add(
                        resource.part);
                }
            }

            inventory.ResourcePartCount =
                resourceParts.Count;

            return inventory;
        }

        private static bool TryGetConnectedResources(
            Part enginePart,
            Propellant propellant,
            IList<PartResource> output)
        {
            if (enginePart == null ||
                propellant == null ||
                output == null)
            {
                return false;
            }

            PartResourceDefinition definition =
                PartResourceLibrary.Instance
                    .GetDefinition(
                        propellant.name);

            if (definition == null)
            {
                return false;
            }

            MethodInfo[] methods =
                enginePart.GetType().GetMethods(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            foreach (MethodInfo method in methods)
            {
                if (!string.Equals(
                        method.Name,
                        "GetConnectedResources",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ParameterInfo[] parameters =
                    method.GetParameters();

                if (parameters.Length != 3)
                {
                    continue;
                }

                object resourceIdentifier =
                    ConvertResourceIdentifier(
                        definition,
                        parameters[0].ParameterType);

                object flowMode =
                    ConvertFlowMode(
                        definition,
                        propellant,
                        parameters[1].ParameterType);

                object resourceCollection =
                    CreateResourceCollectionArgument(
                        parameters[2].ParameterType);

                if (resourceIdentifier == null ||
                    flowMode == null ||
                    resourceCollection == null)
                {
                    continue;
                }

                try
                {
                    method.Invoke(
                        enginePart,
                        new[]
                        {
                            resourceIdentifier,
                            flowMode,
                            resourceCollection
                        });

                    IEnumerable<PartResource> resources =
                        resourceCollection as
                            IEnumerable<PartResource>;

                    if (resources == null)
                    {
                        continue;
                    }

                    foreach (PartResource resource in
                        resources)
                    {
                        if (resource != null)
                        {
                            output.Add(
                                resource);
                        }
                    }

                    return true;
                }
                catch
                {
                }
            }

            return false;
        }

        private static object ConvertResourceIdentifier(
            PartResourceDefinition definition,
            Type targetType)
        {
            if (definition == null ||
                targetType == null)
            {
                return null;
            }

            try
            {
                if (targetType == typeof(int))
                {
                    return definition.id;
                }

                if (targetType == typeof(string))
                {
                    return definition.name;
                }

                if (targetType.IsInstanceOfType(
                        definition))
                {
                    return definition;
                }
            }
            catch
            {
            }

            return null;
        }

        private static object ConvertFlowMode(
            PartResourceDefinition definition,
            Propellant propellant,
            Type targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            object flowMode =
                ReadMemberValue(
                    propellant,
                    "GetFlowMode");

            if (flowMode == null)
            {
                flowMode =
                    ReadMemberValue(
                        propellant,
                        "flowMode");
            }

            if (flowMode == null)
            {
                flowMode =
                    ReadMemberValue(
                        definition,
                        "resourceFlowMode");
            }

            if (flowMode == null)
            {
                return null;
            }

            if (targetType.IsInstanceOfType(
                    flowMode))
            {
                return flowMode;
            }

            try
            {
                if (targetType.IsEnum)
                {
                    return Enum.ToObject(
                        targetType,
                        Convert.ToInt32(
                            flowMode));
                }
            }
            catch
            {
            }

            return null;
        }

        private static object CreateResourceCollectionArgument(
            Type targetType)
        {
            if (targetType == null)
            {
                return null;
            }

            if (targetType.IsAssignableFrom(
                    typeof(List<PartResource>)))
            {
                return new List<PartResource>();
            }

            try
            {
                return Activator.CreateInstance(
                    targetType);
            }
            catch
            {
                return null;
            }
        }

        private static object ReadMemberValue(
            object instance,
            string memberName)
        {
            if (instance == null ||
                string.IsNullOrEmpty(
                    memberName))
            {
                return null;
            }

            Type type =
                instance.GetType();

            try
            {
                MethodInfo method =
                    type.GetMethod(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);

                if (method != null)
                {
                    return method.Invoke(
                        instance,
                        null);
                }

                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    return field.GetValue(
                        instance);
                }

                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null &&
                    property.CanRead)
                {
                    return property.GetValue(
                        instance,
                        null);
                }
            }
            catch
            {
            }

            return null;
        }

        private static void AddFallbackResources(
            IEnumerable<Part> activeParts,
            string resourceName,
            ICollection<PartResource> output)
        {
            if (activeParts == null ||
                string.IsNullOrEmpty(
                    resourceName) ||
                output == null)
            {
                return;
            }

            foreach (Part part in activeParts)
            {
                if (part == null ||
                    part.Resources == null)
                {
                    continue;
                }

                foreach (PartResource resource in
                    part.Resources)
                {
                    if (resource == null ||
                        resource.info == null ||
                        !string.Equals(
                            resource.info.name,
                            resourceName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    output.Add(
                        resource);
                }
            }
        }

        private static Dictionary<string, double>
            CollectResourceMassByName(
                IEnumerable<Part> parts)
        {
            Dictionary<string, double> values =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);

            if (parts == null)
            {
                return values;
            }

            foreach (Part part in parts)
            {
                if (part == null ||
                    part.Resources == null)
                {
                    continue;
                }

                foreach (PartResource resource in
                    part.Resources)
                {
                    if (resource == null ||
                        resource.info == null)
                    {
                        continue;
                    }

                    string name =
                        resource.info.name ??
                        string.Empty;

                    double mass =
                        SanitizeNonNegative(
                            resource.amount *
                            resource.info.density);

                    AddValue(
                        values,
                        name,
                        mass);
                }
            }

            return values;
        }

        private static void AddEnginePropellantFlows(
            ModuleEngines engine,
            double totalMassFlow,
            IDictionary<string, double> flowByResource)
        {
            if (engine == null ||
                engine.propellants == null ||
                totalMassFlow <= 0.0)
            {
                return;
            }

            double weightedDensityTotal = 0.0;

            foreach (Propellant propellant in
                engine.propellants)
            {
                if (propellant == null)
                {
                    continue;
                }

                PartResourceDefinition definition =
                    PartResourceLibrary.Instance
                        .GetDefinition(
                            propellant.name);

                if (definition == null)
                {
                    continue;
                }

                weightedDensityTotal +=
                    SanitizeNonNegative(
                        propellant.ratio) *
                    SanitizeNonNegative(
                        definition.density);
            }

            if (weightedDensityTotal <= 0.0)
            {
                return;
            }

            foreach (Propellant propellant in
                engine.propellants)
            {
                if (propellant == null)
                {
                    continue;
                }

                PartResourceDefinition definition =
                    PartResourceLibrary.Instance
                        .GetDefinition(
                            propellant.name);

                if (definition == null)
                {
                    continue;
                }

                double weightedDensity =
                    SanitizeNonNegative(
                        propellant.ratio) *
                    SanitizeNonNegative(
                        definition.density);

                double resourceMassFlow =
                    totalMassFlow *
                    weightedDensity /
                    weightedDensityTotal;

                AddValue(
                    flowByResource,
                    propellant.name,
                    resourceMassFlow);
            }
        }

        private static BurnLimit CalculateBurnLimit(
            IDictionary<string, double> availableMassByResource,
            IDictionary<string, double> flowByResource)
        {
            BurnLimit result =
                new BurnLimit();

            if (flowByResource == null ||
                flowByResource.Count == 0)
            {
                return result;
            }

            result.DurationSeconds =
                double.MaxValue;

            foreach (KeyValuePair<string, double> pair in
                flowByResource)
            {
                if (pair.Value <= 0.0)
                {
                    continue;
                }

                double availableMass = 0.0;

                availableMassByResource.TryGetValue(
                    pair.Key,
                    out availableMass);

                double duration =
                    availableMass /
                    pair.Value;

                if (duration <
                    result.DurationSeconds)
                {
                    result.DurationSeconds =
                        Math.Max(
                            0.0,
                            duration);

                    result.ResourceName =
                        pair.Key;
                }
            }

            if (result.DurationSeconds ==
                double.MaxValue)
            {
                result.DurationSeconds = 0.0;
            }

            return result;
        }

        private static double CalculateConsumedMass(
            IDictionary<string, double> flowByResource,
            double durationSeconds)
        {
            if (flowByResource == null ||
                durationSeconds <= 0.0)
            {
                return 0.0;
            }

            double consumed = 0.0;

            foreach (double flow in
                flowByResource.Values)
            {
                if (flow > 0.0)
                {
                    consumed +=
                        flow *
                        durationSeconds;
                }
            }

            return consumed;
        }

        private static double
            CalculateMassFlowTonnesPerSecond(
                double thrustKilonewtons,
                double specificImpulseSeconds)
        {
            if (thrustKilonewtons <= 0.0 ||
                specificImpulseSeconds <= 0.0)
            {
                return 0.0;
            }

            /*
             * One kilonewton acting on one tonne has the same
             * numerical acceleration as one newton acting on one
             * kilogram, so kN / (Isp * g0) yields tonnes per second.
             */
            return
                thrustKilonewtons /
                (specificImpulseSeconds *
                 9.80665);
        }

        private static double
            CalculateEffectiveSpecificImpulse(
                double thrustKilonewtons,
                double massFlowTonnesPerSecond)
        {
            if (thrustKilonewtons <= 0.0 ||
                massFlowTonnesPerSecond <= 0.0)
            {
                return 0.0;
            }

            return
                thrustKilonewtons /
                (massFlowTonnesPerSecond *
                 9.80665);
        }

        private static double CalculateDeltaV(
            double specificImpulseSeconds,
            double ignitionMassTonnes,
            double burnoutMassTonnes)
        {
            if (specificImpulseSeconds <= 0.0 ||
                ignitionMassTonnes <= 0.0 ||
                burnoutMassTonnes <= 0.0 ||
                burnoutMassTonnes >= ignitionMassTonnes)
            {
                return 0.0;
            }

            return
                specificImpulseSeconds *
                9.80665 *
                Math.Log(
                    ignitionMassTonnes /
                    burnoutMassTonnes);
        }

        private static void AddValue(
            IDictionary<string, double> values,
            string key,
            double amount)
        {
            if (values == null ||
                string.IsNullOrEmpty(key) ||
                amount <= 0.0)
            {
                return;
            }

            double current = 0.0;

            values.TryGetValue(
                key,
                out current);

            values[key] =
                current +
                amount;
        }

        private static HashSet<Part>
            FindDetachedPartsForStage(
                Vessel vessel,
                HashSet<Part> activeParts,
                int stageNumber,
                StageTopologyEvent topologyEvent)
        {
            HashSet<Part> detachedParts =
                new HashSet<Part>();

            List<SeparationEdge> removedEdges =
                new List<SeparationEdge>();

            foreach (Part part in activeParts)
            {
                if (part == null ||
                    part.inverseStage != stageNumber ||
                    part.Modules == null)
                {
                    continue;
                }

                foreach (PartModule module in part.Modules)
                {
                    if (!IsDecouplerModule(module))
                    {
                        continue;
                    }

                    topologyEvent.DecouplerCount++;

                    SeparationEdge edge =
                        ResolveSeparationEdge(
                            part,
                            module,
                            activeParts);

                    if (edge == null)
                    {
                        topologyEvent
                            .UnresolvedDecouplerCount++;

                        continue;
                    }

                    removedEdges.Add(edge);
                }
            }

            if (removedEdges.Count == 0)
            {
                return detachedParts;
            }

            HashSet<Part> retainedParts =
                FindRootConnectedComponent(
                    vessel,
                    activeParts,
                    removedEdges);

            foreach (Part part in activeParts)
            {
                if (!retainedParts.Contains(part))
                {
                    detachedParts.Add(part);
                }
            }

            return detachedParts;
        }

        private static HashSet<Part>
            FindRootConnectedComponent(
                Vessel vessel,
                HashSet<Part> activeParts,
                IList<SeparationEdge> removedEdges)
        {
            HashSet<Part> connected =
                new HashSet<Part>();

            Part root =
                vessel != null
                    ? vessel.rootPart
                    : null;

            if (root == null ||
                !activeParts.Contains(root))
            {
                foreach (Part part in activeParts)
                {
                    root = part;
                    break;
                }
            }

            if (root == null)
            {
                return connected;
            }

            Queue<Part> pending =
                new Queue<Part>();

            connected.Add(root);
            pending.Enqueue(root);

            while (pending.Count > 0)
            {
                Part current =
                    pending.Dequeue();

                TryVisitConnectedPart(
                    current,
                    current.parent,
                    activeParts,
                    removedEdges,
                    connected,
                    pending);

                if (current.children == null)
                {
                    continue;
                }

                foreach (Part child in current.children)
                {
                    TryVisitConnectedPart(
                        current,
                        child,
                        activeParts,
                        removedEdges,
                        connected,
                        pending);
                }
            }

            return connected;
        }

        private static void TryVisitConnectedPart(
            Part from,
            Part candidate,
            HashSet<Part> activeParts,
            IList<SeparationEdge> removedEdges,
            HashSet<Part> connected,
            Queue<Part> pending)
        {
            if (candidate == null ||
                !activeParts.Contains(candidate) ||
                connected.Contains(candidate) ||
                IsRemovedEdge(from, candidate, removedEdges))
            {
                return;
            }

            connected.Add(candidate);
            pending.Enqueue(candidate);
        }

        private static bool IsRemovedEdge(
            Part first,
            Part second,
            IList<SeparationEdge> removedEdges)
        {
            foreach (SeparationEdge edge in removedEdges)
            {
                bool forward =
                    ReferenceEquals(edge.First, first) &&
                    ReferenceEquals(edge.Second, second);

                bool reverse =
                    ReferenceEquals(edge.First, second) &&
                    ReferenceEquals(edge.Second, first);

                if (forward || reverse)
                {
                    return true;
                }
            }

            return false;
        }

        private static SeparationEdge ResolveSeparationEdge(
            Part decouplerPart,
            PartModule module,
            HashSet<Part> activeParts)
        {
            string explosiveNodeId =
                ReadStringMember(
                    module,
                    "explosiveNodeID");

            if (!string.IsNullOrEmpty(explosiveNodeId))
            {
                try
                {
                    AttachNode node =
                        decouplerPart.FindAttachNode(
                            explosiveNodeId);

                    if (node != null &&
                        node.attachedPart != null &&
                        activeParts.Contains(node.attachedPart))
                    {
                        return new SeparationEdge(
                            decouplerPart,
                            node.attachedPart);
                    }
                }
                catch
                {
                }
            }

            if (decouplerPart.parent != null &&
                activeParts.Contains(decouplerPart.parent))
            {
                return new SeparationEdge(
                    decouplerPart,
                    decouplerPart.parent);
            }

            if (decouplerPart.children != null)
            {
                foreach (Part child in decouplerPart.children)
                {
                    if (child != null &&
                        activeParts.Contains(child))
                    {
                        return new SeparationEdge(
                            decouplerPart,
                            child);
                    }
                }
            }

            return null;
        }

        private static string ReadStringMember(
            object instance,
            string memberName)
        {
            if (instance == null ||
                string.IsNullOrEmpty(memberName))
            {
                return string.Empty;
            }

            Type type = instance.GetType();

            try
            {
                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    return field.GetValue(instance) as string ??
                        string.Empty;
                }

                PropertyInfo property =
                    type.GetProperty(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (property != null &&
                    property.CanRead)
                {
                    return property.GetValue(
                        instance,
                        null) as string ??
                        string.Empty;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static bool PartHasEngine(Part part)
        {
            if (part == null ||
                part.Modules == null)
            {
                return false;
            }

            foreach (PartModule module in part.Modules)
            {
                if (module is ModuleEngines)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PartHasDecoupler(Part part)
        {
            if (part == null ||
                part.Modules == null)
            {
                return false;
            }

            foreach (PartModule module in part.Modules)
            {
                if (IsDecouplerModule(module))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDecouplerModule(
            PartModule module)
        {
            if (module == null)
            {
                return false;
            }

            string moduleName =
                module.moduleName ??
                module.GetType().Name ??
                string.Empty;

            return
                moduleName.IndexOf(
                    "Decouple",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                moduleName.IndexOf(
                    "Separator",
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double GetPartsMassTonnes(
            IEnumerable<Part> parts)
        {
            double mass = 0.0;

            if (parts == null)
            {
                return mass;
            }

            foreach (Part part in parts)
            {
                if (part == null)
                {
                    continue;
                }

                mass += GetPartDryMassTonnes(part);
                mass += GetPartResourceMassTonnes(part);
            }

            return mass;
        }

        private static LaunchMassClass ClassifyMass(
            double launchMassTonnes)
        {
            if (launchMassTonnes < 25.0)
            {
                return LaunchMassClass.Light;
            }

            if (launchMassTonnes < 100.0)
            {
                return LaunchMassClass.Medium;
            }

            if (launchMassTonnes < 400.0)
            {
                return LaunchMassClass.Heavy;
            }

            return LaunchMassClass.SuperHeavy;
        }

        private static ThrustClass ClassifyThrust(
            double initialSeaLevelTwr)
        {
            if (initialSeaLevelTwr < 1.25)
            {
                return ThrustClass.Low;
            }

            if (initialSeaLevelTwr <= 1.75)
            {
                return ThrustClass.Standard;
            }

            return ThrustClass.High;
        }

        private static double CalculateInitialThrustToWeightRatio(
            Vessel vessel,
            double launchMassTonnes,
            double seaLevelThrustKilonewtons)
        {
            if (launchMassTonnes <= 0.0 ||
                seaLevelThrustKilonewtons <= 0.0)
            {
                return 0.0;
            }

            double gravity =
                GetSurfaceGravity(
                    vessel);

            if (gravity <= 0.0)
            {
                return 0.0;
            }

            return
                seaLevelThrustKilonewtons /
                (launchMassTonnes *
                 gravity);
        }

        private static string FormatMassClass(
            LaunchMassClass value)
        {
            switch (value)
            {
                case LaunchMassClass.Light:
                    return "LIGHT";

                case LaunchMassClass.Medium:
                    return "MEDIUM";

                case LaunchMassClass.Heavy:
                    return "HEAVY";

                case LaunchMassClass.SuperHeavy:
                    return "SUPER HEAVY";

                default:
                    return "UNKNOWN";
            }
        }

        private static string FormatThrustClass(
            ThrustClass value)
        {
            switch (value)
            {
                case ThrustClass.Low:
                    return "LOW";

                case ThrustClass.Standard:
                    return "STANDARD";

                case ThrustClass.High:
                    return "HIGH";

                default:
                    return "UNKNOWN";
            }
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static double SanitizeNonNegative(double value)
        {
            if (!IsFinite(value) || value < 0.0)
            {
                return 0.0;
            }

            return value;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }


    internal enum LaunchMassClass
    {
        Light,
        Medium,
        Heavy,
        SuperHeavy
    }

    internal enum ThrustClass
    {
        Low,
        Standard,
        High
    }

    internal sealed class CraftAnalysis
    {
        public CraftAnalysis()
        {
            Stages =
                new List<StageAnalysis>();

            StageTopology =
                new List<StageTopologyEvent>();
        }

        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public int PartCount { get; set; }
        public int EngineCount { get; set; }
        public int PropulsiveStageCount { get; set; }
        public int InitialPropulsiveStageNumber { get; set; } = -1;
        public double TotalMassTonnes { get; set; }
        public double TotalDryMassTonnes { get; set; }
        public double TotalResourceMassTonnes { get; set; }
        public double InitialSeaLevelThrustKilonewtons { get; set; }
        public double InitialVacuumThrustKilonewtons { get; set; }
        public double InitialSeaLevelThrustToWeightRatio { get; set; }
        public LaunchMassClass MassClass { get; set; }
        public ThrustClass ThrustClass { get; set; }

        public IList<StageAnalysis> Stages
        {
            get;
            private set;
        }

        public IList<StageTopologyEvent> StageTopology
        {
            get;
            private set;
        }

        public Simulation.CraftSimulationModel
            SimulationModel
        {
            get;
            set;
        }
    }


    internal sealed class StageTopologyEvent
    {
        public int StageNumber { get; set; }
        public int IgnitingEngineCount { get; set; }
        public int ContinuingEngineCount { get; set; }
        public int DiscardedEngineCount { get; set; }
        public int ActiveEngineCount { get; set; }
        public int DecouplerCount { get; set; }
        public int UnresolvedDecouplerCount { get; set; }
        public double PreEventMassTonnes { get; set; }
        public double IgnitionMassTonnes { get; set; }
        public double RetainedMassTonnes { get; set; }
        public double DiscardedMassTonnes { get; set; }
        public double SeaLevelThrustKilonewtons { get; set; }
        public double VacuumThrustKilonewtons { get; set; }
        public double ActiveSeaLevelThrustKilonewtons { get; set; }
        public double ActiveVacuumThrustKilonewtons { get; set; }
        public double EffectiveSeaLevelSpecificImpulse { get; set; }
        public double EffectiveVacuumSpecificImpulse { get; set; }
        public double SeaLevelMassFlowTonnesPerSecond { get; set; }
        public double VacuumMassFlowTonnesPerSecond { get; set; }
        public double AvailablePropellantMassTonnes { get; set; }
        public int ReachableResourcePartCount { get; set; }
        public int FuelNetworkFallbackCount { get; set; }
        public double EstimatedSeaLevelBurnSeconds { get; set; }
        public double EstimatedVacuumBurnSeconds { get; set; }
        public double EstimatedBurnoutMassTonnes { get; set; }
        public double BurnoutSeaLevelThrustToWeightRatio { get; set; }
        public double EstimatedSeaLevelDeltaVMetresPerSecond { get; set; }
        public double EstimatedVacuumDeltaVMetresPerSecond { get; set; }
        public string LimitingPropellant { get; set; }

        public double
            IgnitionSeaLevelThrustToWeightRatio
        {
            get;
            set;
        }

        public double
            ActiveSeaLevelThrustToWeightRatio
        {
            get;
            set;
        }
    }



    internal sealed class ReachableResourceInventory
    {
        public ReachableResourceInventory()
        {
            MassByResource =
                new Dictionary<string, double>(
                    StringComparer.Ordinal);
        }

        public IDictionary<string, double>
            MassByResource
        {
            get;
            private set;
        }

        public int ResourcePartCount { get; set; }

        public int FallbackCount { get; set; }
    }

    internal sealed class BurnLimit
    {
        public string ResourceName { get; set; }

        public double DurationSeconds { get; set; }
    }

    internal sealed class EnginePerformance
    {
        public double SeaLevelThrustKilonewtons { get; set; }
        public double VacuumThrustKilonewtons { get; set; }
        public double SeaLevelSpecificImpulse { get; set; }
        public double VacuumSpecificImpulse { get; set; }
    }

    internal sealed class SeparationEdge
    {
        public SeparationEdge(
            Part first,
            Part second)
        {
            First = first;
            Second = second;
        }

        public Part First { get; private set; }
        public Part Second { get; private set; }
    }

    internal sealed class StageAnalysis
    {
        public int StageNumber { get; set; }
        public int PartCount { get; set; }
        public int EngineCount { get; set; }
        public int DecouplerCount { get; set; }
        public double WetMassTonnes { get; set; }
        public double DryMassTonnes { get; set; }
        public double ResourceMassTonnes { get; set; }
        public double LiquidFuelAmount { get; set; }
        public double LiquidFuelCapacity { get; set; }
        public double OxidizerAmount { get; set; }
        public double OxidizerCapacity { get; set; }
        public double SolidFuelAmount { get; set; }
        public double SolidFuelCapacity { get; set; }
        public double MonopropellantAmount { get; set; }
        public double MonopropellantCapacity { get; set; }
        public double SeaLevelThrustKilonewtons { get; set; }
        public double VacuumThrustKilonewtons { get; set; }
        public double SeaLevelSpecificImpulse { get; set; }
        public double VacuumSpecificImpulse { get; set; }
        public double SeaLevelThrustToWeightRatio { get; set; }

        internal double SeaLevelSpecificImpulseTotal { get; set; }
        internal int SeaLevelSpecificImpulseSamples { get; set; }
        internal double VacuumSpecificImpulseTotal { get; set; }
        internal int VacuumSpecificImpulseSamples { get; set; }
    }
}
