using System;
using System.Collections.Generic;
using System.Reflection;

namespace KMC.Plugin.Simulation
{
    internal static class CraftSimulationBuilder
    {
        public static CraftSimulationModel Build(
            Vessel vessel)
        {
            CraftSimulationModel model =
                new CraftSimulationModel();

            if (vessel == null ||
                vessel.parts == null)
            {
                return model;
            }

            model.VesselName =
                vessel.vesselName ??
                string.Empty;

            Dictionary<Part, SimulatedPart> partMap =
                new Dictionary<Part, SimulatedPart>();

            foreach (Part part in vessel.parts)
            {
                if (part == null)
                {
                    continue;
                }

                SimulatedPart simulatedPart =
                    CreatePart(
                        vessel,
                        part);

                model.Parts.Add(
                    simulatedPart);

                partMap.Add(
                    part,
                    simulatedPart);

                if (simulatedPart.IsRoot)
                {
                    model.RootPartCount++;
                }

                foreach (SimulatedEngine engine in
                    simulatedPart.Engines)
                {
                    model.Engines.Add(
                        engine);
                }
            }

            BuildPhysicalLinks(
                partMap);

            BuildCrossFeedSets(
                partMap,
                model);

            return model;
        }

        private static SimulatedPart CreatePart(
            Vessel vessel,
            Part part)
        {
            SimulatedPart result =
                new SimulatedPart
                {
                    PersistentId =
                        part.persistentId,

                    Name =
                        part.partInfo != null
                            ? part.partInfo.name
                            : part.name,

                    InverseStage =
                        Math.Max(
                            0,
                            part.inverseStage),

                    DecoupledInStage =
                        FindDecouplingStage(
                            part),

                    IsRoot =
                        ReferenceEquals(
                            vessel.rootPart,
                            part),

                    AllowsCrossFeed =
                        ReadFuelCrossFeed(
                            part),

                    DryMassTonnes =
                        ReadDryMassTonnes(
                            part)
                };

            ReadResources(
                part,
                result);

            ReadEngines(
                part,
                result);

            return result;
        }

        private static void BuildPhysicalLinks(
            IDictionary<Part, SimulatedPart> partMap)
        {
            foreach (KeyValuePair<Part, SimulatedPart> pair in
                partMap)
            {
                Part part =
                    pair.Key;

                SimulatedPart simulatedPart =
                    pair.Value;

                AddLinkedPart(
                    part.parent,
                    partMap,
                    simulatedPart.LinkedPartIds);

                if (part.children == null)
                {
                    continue;
                }

                foreach (Part child in part.children)
                {
                    AddLinkedPart(
                        child,
                        partMap,
                        simulatedPart.LinkedPartIds);
                }
            }
        }

        private static void BuildCrossFeedSets(
            IDictionary<Part, SimulatedPart> partMap,
            CraftSimulationModel model)
        {
            foreach (KeyValuePair<Part, SimulatedPart> pair in
                partMap)
            {
                Part source =
                    pair.Key;

                SimulatedPart simulatedSource =
                    pair.Value;

                HashSet<Part> visited =
                    new HashSet<Part>();

                Queue<Part> pending =
                    new Queue<Part>();

                visited.Add(
                    source);

                pending.Enqueue(
                    source);

                while (pending.Count > 0)
                {
                    Part current =
                        pending.Dequeue();

                    SimulatedPart simulatedCurrent;

                    if (!partMap.TryGetValue(
                            current,
                            out simulatedCurrent))
                    {
                        continue;
                    }

                    simulatedSource.CrossFeedPartIds.Add(
                        simulatedCurrent.PersistentId);

                    foreach (uint linkedId in
                        simulatedCurrent.LinkedPartIds)
                    {
                        Part linkedPart =
                            FindPartByPersistentId(
                                partMap,
                                linkedId);

                        if (linkedPart == null ||
                            visited.Contains(
                                linkedPart))
                        {
                            continue;
                        }

                        SimulatedPart simulatedLinked =
                            partMap[linkedPart];

                        if (!CanCrossFeedBetween(
                                simulatedCurrent,
                                simulatedLinked))
                        {
                            continue;
                        }

                        visited.Add(
                            linkedPart);

                        pending.Enqueue(
                            linkedPart);

                        model.CrossFeedLinkCount++;
                    }
                }
            }
        }

        private static bool CanCrossFeedBetween(
            SimulatedPart first,
            SimulatedPart second)
        {
            return
                first != null &&
                second != null &&
                first.AllowsCrossFeed &&
                second.AllowsCrossFeed;
        }

        private static void AddLinkedPart(
            Part linkedPart,
            IDictionary<Part, SimulatedPart> partMap,
            ICollection<uint> linkedIds)
        {
            if (linkedPart == null ||
                linkedIds == null)
            {
                return;
            }

            SimulatedPart simulatedLinked;

            if (!partMap.TryGetValue(
                    linkedPart,
                    out simulatedLinked))
            {
                return;
            }

            if (!linkedIds.Contains(
                    simulatedLinked.PersistentId))
            {
                linkedIds.Add(
                    simulatedLinked.PersistentId);
            }
        }

        private static Part FindPartByPersistentId(
            IDictionary<Part, SimulatedPart> partMap,
            uint persistentId)
        {
            foreach (KeyValuePair<Part, SimulatedPart> pair in
                partMap)
            {
                if (pair.Value.PersistentId ==
                    persistentId)
                {
                    return pair.Key;
                }
            }

            return null;
        }

        private static void ReadResources(
            Part part,
            SimulatedPart simulatedPart)
        {
            if (part.Resources == null)
            {
                return;
            }

            foreach (PartResource resource in
                part.Resources)
            {
                if (resource == null ||
                    resource.info == null)
                {
                    continue;
                }

                simulatedPart.Resources.Add(
                    new SimulatedResource
                    {
                        ResourceId =
                            resource.info.id,

                        Name =
                            resource.info.name ??
                            string.Empty,

                        Amount =
                            Math.Max(
                                0.0,
                                resource.amount),

                        Capacity =
                            Math.Max(
                                0.0,
                                resource.maxAmount),

                        DensityTonnesPerUnit =
                            Math.Max(
                                0.0,
                                resource.info.density),

                        FlowEnabled =
                            resource.flowState
                    });
            }
        }

        private static void ReadEngines(
            Part part,
            SimulatedPart simulatedPart)
        {
            if (part.Modules == null)
            {
                return;
            }

            foreach (PartModule module in
                part.Modules)
            {
                ModuleEngines engine =
                    module as ModuleEngines;

                if (engine == null)
                {
                    continue;
                }

                SimulatedEngine simulatedEngine =
                    new SimulatedEngine
                    {
                        PartPersistentId =
                            part.persistentId,

                        PartName =
                            simulatedPart.Name,

                        ActivationStage =
                            simulatedPart.InverseStage,

                        SeaLevelSpecificImpulse =
                            EvaluateIsp(
                                engine,
                                1.0f),

                        VacuumSpecificImpulse =
                            EvaluateIsp(
                                engine,
                                0.0f),

                        VacuumThrustKilonewtons =
                            Math.Max(
                                0.0,
                                engine.maxThrust) *
                            Clamp01(
                                engine.thrustPercentage /
                                100.0),

                        ThrottleLocked =
                            ReadBooleanMember(
                                engine,
                                "throttleLocked")
                    };

                if (simulatedEngine
                        .VacuumSpecificImpulse >
                    0.0)
                {
                    simulatedEngine
                        .SeaLevelThrustKilonewtons =
                        simulatedEngine
                            .VacuumThrustKilonewtons *
                        simulatedEngine
                            .SeaLevelSpecificImpulse /
                        simulatedEngine
                            .VacuumSpecificImpulse;
                }

                ReadPropellants(
                    engine,
                    simulatedEngine);

                simulatedPart.Engines.Add(
                    simulatedEngine);
            }
        }

        private static void ReadPropellants(
            ModuleEngines engine,
            SimulatedEngine simulatedEngine)
        {
            if (engine.propellants == null)
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

                object rawFlowMode =
                    ReadMemberValue(
                        propellant,
                        "GetFlowMode");

                if (rawFlowMode == null)
                {
                    rawFlowMode =
                        ReadMemberValue(
                            propellant,
                            "flowMode");
                }

                if (rawFlowMode == null &&
                    definition != null)
                {
                    rawFlowMode =
                        ReadMemberValue(
                            definition,
                            "resourceFlowMode");
                }

                simulatedEngine.Propellants.Add(
                    new SimulatedPropellant
                    {
                        ResourceId =
                            definition != null
                                ? definition.id
                                : -1,

                        Name =
                            propellant.name ??
                            string.Empty,

                        Ratio =
                            Math.Max(
                                0.0,
                                propellant.ratio),

                        DensityTonnesPerUnit =
                            definition != null
                                ? Math.Max(
                                    0.0,
                                    definition.density)
                                : 0.0,

                        FlowCategory =
                            ResourceFlowCategoryParser
                                .Parse(
                                    rawFlowMode),

                        RawFlowMode =
                            rawFlowMode != null
                                ? rawFlowMode.ToString()
                                : "UNKNOWN"
                    });
            }
        }

        private static int FindDecouplingStage(
            Part part)
        {
            if (part == null ||
                part.Modules == null)
            {
                return -1;
            }

            foreach (PartModule module in
                part.Modules)
            {
                if (module == null)
                {
                    continue;
                }

                string name =
                    module.moduleName ??
                    module.GetType().Name ??
                    string.Empty;

                if (name.IndexOf(
                        "Decouple",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf(
                        "Separator",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Math.Max(
                        0,
                        part.inverseStage);
                }
            }

            return -1;
        }

        private static bool ReadFuelCrossFeed(
            Part part)
        {
            object value =
                ReadMemberValue(
                    part,
                    "fuelCrossFeed");

            if (value is bool)
            {
                return (bool)value;
            }

            return true;
        }

        private static double ReadDryMassTonnes(
            Part part)
        {
            if (part == null)
            {
                return 0.0;
            }

            double mass =
                Math.Max(
                    0.0,
                    part.mass);

            if (part.Modules == null)
            {
                return mass;
            }

            foreach (PartModule module in
                part.Modules)
            {
                IPartMassModifier modifier =
                    module as IPartMassModifier;

                if (modifier == null)
                {
                    continue;
                }

                try
                {
                    mass +=
                        modifier.GetModuleMass(
                            part.mass,
                            ModifierStagingSituation
                                .CURRENT);
                }
                catch
                {
                }
            }

            return Math.Max(
                0.0,
                mass);
        }

        private static double EvaluateIsp(
            ModuleEngines engine,
            float pressure)
        {
            if (engine == null ||
                engine.atmosphereCurve == null)
            {
                return 0.0;
            }

            return Math.Max(
                0.0,
                engine.atmosphereCurve
                    .Evaluate(
                        pressure));
        }

        private static bool ReadBooleanMember(
            object instance,
            string memberName)
        {
            object value =
                ReadMemberValue(
                    instance,
                    memberName);

            return
                value is bool &&
                (bool)value;
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

        private static double Clamp01(
            double value)
        {
            if (value < 0.0)
            {
                return 0.0;
            }

            if (value > 1.0)
            {
                return 1.0;
            }

            return value;
        }
    }
}
