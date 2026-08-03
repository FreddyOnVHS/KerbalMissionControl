using System;
using System.Collections.Generic;
using System.Reflection;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Calculates the propellant physically reachable by the current
    /// propulsion-engine set.
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
            public int ReachablePartCount;
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
                SelectCurrentEngines(
                    vessel);

            result.SelectedEngineCount =
                engines.Count;

            HashSet<uint> visited =
                new HashSet<uint>();

            Queue<Part> pending =
                new Queue<Part>();

            for (int index = 0;
                 index < engines.Count;
                 index++)
            {
                Part enginePart =
                    engines[index];

                if (enginePart != null &&
                    visited.Add(
                        enginePart.flightID))
                {
                    pending.Enqueue(
                        enginePart);
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

                AddPartResources(
                    current,
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

            result.ReachablePartCount =
                visited.Count;

            return result;
        }

        private static List<Part>
            SelectCurrentEngines(
                Vessel vessel)
        {
            List<Part> ignited =
                new List<Part>();

            List<Part> stageMatched =
                new List<Part>();

            int currentStage =
                vessel.currentStage;

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

                bool hasEngine = false;
                bool hasIgnitedEngine = false;

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

                    hasEngine = true;

                    if (engine.EngineIgnited &&
                        !engine.engineShutdown)
                    {
                        hasIgnitedEngine = true;
                    }
                }

                if (!hasEngine)
                {
                    continue;
                }

                if (hasIgnitedEngine)
                {
                    ignited.Add(part);
                }

                if (part.inverseStage ==
                        currentStage ||
                    part.inverseStage ==
                        currentStage - 1)
                {
                    stageMatched.Add(part);
                }
            }

            /*
             * A fuel-starved engine normally remains ignited
             * while flamed out, so this preserves the intended
             * active engine feed after fuel depletion.
             */
            return ignited.Count > 0
                ? ignited
                : stageMatched;
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

            pending.Enqueue(linked);
        }

        private static void AddPartResources(
            Part part,
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
                else if (EqualsName(
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
