using System;
using System.Collections.Generic;
using System.Reflection;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Calculates propellant physically reachable by consumers that are
    /// currently active.
    ///
    /// Main-engine resources and RCS resources are analyzed independently:
    ///
    /// - LiquidFuel and Oxidizer originate from ignited ModuleEngines parts.
    /// - MonoPropellant originates from enabled ModuleRCS parts only while the
    ///   vessel RCS action group is active.
    ///
    /// Stored resources remain available through the vessel-wide TOTAL fields;
    /// they are not reported as ACTIVE unless an active consumer can reach them.
    /// </summary>
    internal static class ActivePropellantAnalyzer
    {
        internal sealed class Result
        {
            public double LiquidFuelAmount;
            public double LiquidFuelCapacity;

            public double OxidizerAmount;
            public double OxidizerCapacity;

            public double MonopropellantAmount;
            public double MonopropellantCapacity;

            public int SelectedEngineCount;
            public int SelectedRcsPartCount;
            public int ReachablePartCount;
        }

        private enum ResourceSelection
        {
            MainEnginePropellants,
            Monopropellant
        }

        public static Result Analyze(
            Vessel vessel)
        {
            Result result =
                new Result();

            if (vessel == null ||
                vessel.parts == null)
            {
                return result;
            }

            List<Part> engines =
                SelectIgnitedEngineParts(
                    vessel);

            result.SelectedEngineCount =
                engines.Count;

            HashSet<uint> allReachable =
                new HashSet<uint>();

            AnalyzeConsumerNetwork(
                engines,
                ResourceSelection.MainEnginePropellants,
                result,
                allReachable);

            List<Part> rcsParts =
                SelectActiveRcsParts(
                    vessel);

            result.SelectedRcsPartCount =
                rcsParts.Count;

            AnalyzeConsumerNetwork(
                rcsParts,
                ResourceSelection.Monopropellant,
                result,
                allReachable);

            result.ReachablePartCount =
                allReachable.Count;

            return result;
        }

        private static void AnalyzeConsumerNetwork(
            IList<Part> consumers,
            ResourceSelection resourceSelection,
            Result result,
            ISet<uint> allReachable)
        {
            if (consumers == null ||
                consumers.Count == 0)
            {
                return;
            }

            HashSet<uint> visited =
                new HashSet<uint>();

            Queue<Part> pending =
                new Queue<Part>();

            for (int index = 0;
                 index < consumers.Count;
                 index++)
            {
                Part consumer =
                    consumers[index];

                if (consumer != null &&
                    visited.Add(
                        consumer.flightID))
                {
                    pending.Enqueue(
                        consumer);
                }
            }

            while (pending.Count > 0)
            {
                Part current =
                    pending.Dequeue();

                if (current == null)
                {
                    continue;
                }

                allReachable.Add(
                    current.flightID);

                AddPartResources(
                    current,
                    resourceSelection,
                    result);

                if (!AllowsCrossFeed(current))
                {
                    continue;
                }

                EnqueueLinkedPart(
                    current.parent,
                    current,
                    visited,
                    pending);

                if (current.children == null)
                {
                    continue;
                }

                for (int index = 0;
                     index < current.children.Count;
                     index++)
                {
                    EnqueueLinkedPart(
                        current.children[index],
                        current,
                        visited,
                        pending);
                }
            }
        }

        private static List<Part>
            SelectIgnitedEngineParts(
                Vessel vessel)
        {
            List<Part> result =
                new List<Part>();

            for (int partIndex = 0;
                 partIndex < vessel.parts.Count;
                 partIndex++)
            {
                Part part =
                    vessel.parts[partIndex];

                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                bool hasActiveEngine =
                    false;

                for (int moduleIndex = 0;
                     moduleIndex < part.Modules.Count;
                     moduleIndex++)
                {
                    ModuleEngines engine =
                        part.Modules[moduleIndex]
                        as ModuleEngines;

                    if (engine == null)
                    {
                        continue;
                    }

                    /*
                     * A fuel-starved engine normally remains ignited while
                     * flamed out. Retaining it here preserves the intended
                     * active feed network after depletion.
                     */
                    if (engine.EngineIgnited &&
                        !engine.engineShutdown)
                    {
                        hasActiveEngine =
                            true;

                        break;
                    }
                }

                if (hasActiveEngine)
                {
                    result.Add(
                        part);
                }
            }

            return result;
        }

        private static List<Part>
            SelectActiveRcsParts(
                Vessel vessel)
        {
            List<Part> result =
                new List<Part>();

            if (!IsRcsActionGroupEnabled(
                    vessel))
            {
                return result;
            }

            for (int partIndex = 0;
                 partIndex < vessel.parts.Count;
                 partIndex++)
            {
                Part part =
                    vessel.parts[partIndex];

                if (part == null ||
                    part.Modules == null)
                {
                    continue;
                }

                bool hasEnabledRcs =
                    false;

                for (int moduleIndex = 0;
                     moduleIndex < part.Modules.Count;
                     moduleIndex++)
                {
                    ModuleRCS rcs =
                        part.Modules[moduleIndex]
                        as ModuleRCS;

                    if (rcs != null &&
                        rcs.isEnabled)
                    {
                        hasEnabledRcs =
                            true;

                        break;
                    }
                }

                if (hasEnabledRcs)
                {
                    result.Add(
                        part);
                }
            }

            return result;
        }

        private static bool IsRcsActionGroupEnabled(
            Vessel vessel)
        {
            if (vessel == null ||
                vessel.ActionGroups == null)
            {
                return false;
            }

            try
            {
                return vessel.ActionGroups[
                    KSPActionGroup.RCS];
            }
            catch
            {
                return false;
            }
        }

        private static void EnqueueLinkedPart(
            Part linked,
            Part current,
            ISet<uint> visited,
            Queue<Part> pending)
        {
            if (linked == null ||
                current == null ||
                visited.Contains(
                    linked.flightID))
            {
                return;
            }

            if (!AllowsCrossFeed(current) ||
                !AllowsCrossFeed(linked))
            {
                return;
            }

            visited.Add(
                linked.flightID);

            pending.Enqueue(
                linked);
        }

        private static void AddPartResources(
            Part part,
            ResourceSelection resourceSelection,
            Result result)
        {
            if (part.Resources == null)
            {
                return;
            }

            for (int index = 0;
                 index < part.Resources.Count;
                 index++)
            {
                PartResource resource =
                    part.Resources[index];

                if (resource == null ||
                    resource.info == null ||
                    !resource.flowState)
                {
                    continue;
                }

                string name =
                    resource.info.name;

                if (resourceSelection ==
                        ResourceSelection.MainEnginePropellants)
                {
                    if (EqualsName(
                            name,
                            "LiquidFuel"))
                    {
                        result.LiquidFuelAmount +=
                            Math.Max(
                                0.0,
                                resource.amount);

                        result.LiquidFuelCapacity +=
                            Math.Max(
                                0.0,
                                resource.maxAmount);
                    }
                    else if (EqualsName(
                                 name,
                                 "Oxidizer"))
                    {
                        result.OxidizerAmount +=
                            Math.Max(
                                0.0,
                                resource.amount);

                        result.OxidizerCapacity +=
                            Math.Max(
                                0.0,
                                resource.maxAmount);
                    }
                }
                else if (resourceSelection ==
                             ResourceSelection.Monopropellant &&
                         EqualsName(
                             name,
                             "MonoPropellant"))
                {
                    result.MonopropellantAmount +=
                        Math.Max(
                            0.0,
                            resource.amount);

                    result.MonopropellantCapacity +=
                        Math.Max(
                            0.0,
                            resource.maxAmount);
                }
            }
        }

        private static bool AllowsCrossFeed(
            Part part)
        {
            if (part == null)
            {
                return false;
            }

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

        private static object ReadMemberValue(
            object instance,
            string name)
        {
            if (instance == null ||
                string.IsNullOrEmpty(name))
            {
                return null;
            }

            Type type =
                instance.GetType();

            FieldInfo field =
                type.GetField(
                    name,
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
                    name,
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

            return null;
        }

        private static bool EqualsName(
            string left,
            string right)
        {
            return string.Equals(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
