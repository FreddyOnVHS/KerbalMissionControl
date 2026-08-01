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
            stageNumbers.Sort(delegate (int left, int right)
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

            return analysis;
        }

        public static string CreateDiagnosticReport(CraftAnalysis analysis)
        {
            if (analysis == null)
            {
                return "[KMC] Craft analysis unavailable.";
            }

            StringBuilder builder = new StringBuilder();

            builder.AppendLine("[KMC] Craft Analyzer Phase 1");
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
                    "[KMC] Event {0:00}: ignition engines={1}, ignition mass={2:0.000} t, SL thrust={3:0.0} kN, ignition SL TWR={4:0.00}, decouplers={5}, discarded={6:0.000} t, retained={7:0.000} t, unresolved={8}",
                    topologyEvent.StageNumber,
                    topologyEvent.IgnitingEngineCount,
                    topologyEvent.IgnitionMassTonnes,
                    topologyEvent.SeaLevelThrustKilonewtons,
                    topologyEvent.IgnitionSeaLevelThrustToWeightRatio,
                    topologyEvent.DecouplerCount,
                    topologyEvent.DiscardedMassTonnes,
                    topologyEvent.RetainedMassTonnes,
                    topologyEvent.UnresolvedDecouplerCount);

                builder.AppendLine();
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

            double surfaceGravity =
                GetSurfaceGravity(vessel);

            foreach (int stageNumber in eventStages)
            {
                StageTopologyEvent topologyEvent =
                    new StageTopologyEvent
                    {
                        StageNumber = stageNumber,
                        IgnitionMassTonnes =
                            GetPartsMassTonnes(activeParts)
                    };

                ReadIgnitingEngines(
                    activeParts,
                    stageNumber,
                    topologyEvent);

                if (topologyEvent.IgnitionMassTonnes > 0.0 &&
                    surfaceGravity > 0.0)
                {
                    topologyEvent
                        .IgnitionSeaLevelThrustToWeightRatio =
                        topologyEvent.SeaLevelThrustKilonewtons /
                        (topologyEvent.IgnitionMassTonnes *
                         surfaceGravity);
                }

                HashSet<Part> detachedParts =
                    FindDetachedPartsForStage(
                        vessel,
                        activeParts,
                        stageNumber,
                        topologyEvent);

                topologyEvent.DiscardedMassTonnes =
                    GetPartsMassTonnes(detachedParts);

                foreach (Part detachedPart in detachedParts)
                {
                    activeParts.Remove(detachedPart);
                }

                topologyEvent.RetainedMassTonnes =
                    GetPartsMassTonnes(activeParts);

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
                delegate (int left, int right)
                {
                    return right.CompareTo(left);
                });

            return orderedStages;
        }

        private static void ReadIgnitingEngines(
            IEnumerable<Part> activeParts,
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

                    if (engine == null)
                    {
                        continue;
                    }

                    topologyEvent.IgnitingEngineCount++;

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

                    topologyEvent
                        .SeaLevelThrustKilonewtons +=
                        ConvertVacuumThrustToAmbientThrust(
                            vacuumThrust,
                            vacuumIsp,
                            seaLevelIsp);

                    topologyEvent
                        .VacuumThrustKilonewtons +=
                        vacuumThrust;
                }
            }
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
    }


    internal sealed class StageTopologyEvent
    {
        public int StageNumber { get; set; }
        public int IgnitingEngineCount { get; set; }
        public int DecouplerCount { get; set; }
        public int UnresolvedDecouplerCount { get; set; }
        public double IgnitionMassTonnes { get; set; }
        public double RetainedMassTonnes { get; set; }
        public double DiscardedMassTonnes { get; set; }
        public double SeaLevelThrustKilonewtons { get; set; }
        public double VacuumThrustKilonewtons { get; set; }

        public double
            IgnitionSeaLevelThrustToWeightRatio
        {
            get;
            set;
        }
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