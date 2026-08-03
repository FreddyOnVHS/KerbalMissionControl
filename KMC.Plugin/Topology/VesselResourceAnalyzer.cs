using System;
using System.Collections.Generic;
using System.Reflection;
using KMC.Shared.Topology;

namespace KMC.Plugin.Topology
{
    /// <summary>
    /// Captures per-part resource state and resolves simplified physical
    /// crossfeed reachability for each engine propellant.
    ///
    /// This phase deliberately mirrors the existing CraftSimulationBuilder
    /// physical-link model. Exact KSP priority and advanced flow modes remain
    /// the responsibility of the later propulsion simulation phase.
    /// </summary>
    internal static class VesselResourceAnalyzer
    {
        public static void Analyze(
            Vessel vessel,
            VesselTopology topology)
        {
            if (vessel == null ||
                vessel.parts == null ||
                topology == null)
            {
                return;
            }

            Dictionary<uint, VesselTopologyNode> nodes =
                BuildNodeMap(topology);

            Dictionary<uint, Part> parts =
                BuildPartMap(vessel);

            foreach (KeyValuePair<uint, VesselTopologyNode> pair in nodes)
            {
                Part part;

                if (!parts.TryGetValue(pair.Key, out part))
                {
                    continue;
                }

                VesselTopologyNode node = pair.Value;

                node.AllowsCrossFeed =
                    ReadFuelCrossFeed(part);

                ReadResources(part, node);
                ReadEngineRequirements(part, node);
            }

            ResolveReachableSources(nodes);
        }

        private static Dictionary<uint, VesselTopologyNode> BuildNodeMap(
            VesselTopology topology)
        {
            Dictionary<uint, VesselTopologyNode> result =
                new Dictionary<uint, VesselTopologyNode>();

            for (int index = 0; index < topology.Nodes.Count; index++)
            {
                VesselTopologyNode node = topology.Nodes[index];

                if (node != null)
                {
                    result[node.PartId] = node;
                }
            }

            return result;
        }

        private static Dictionary<uint, Part> BuildPartMap(
            Vessel vessel)
        {
            Dictionary<uint, Part> result =
                new Dictionary<uint, Part>();

            for (int index = 0; index < vessel.parts.Count; index++)
            {
                Part part = vessel.parts[index];

                if (part != null)
                {
                    result[part.flightID] = part;
                }
            }

            return result;
        }

        private static void ReadResources(
            Part part,
            VesselTopologyNode node)
        {
            node.Resources.Clear();

            if (part.Resources == null)
            {
                return;
            }

            for (int index = 0; index < part.Resources.Count; index++)
            {
                PartResource resource = part.Resources[index];

                if (resource == null ||
                    resource.info == null)
                {
                    continue;
                }

                node.Resources.Add(
                    new VesselResourceState
                    {
                        ResourceId = resource.info.id,
                        Name = resource.info.name ?? string.Empty,
                        Amount = Math.Max(0.0, resource.amount),
                        Capacity = Math.Max(0.0, resource.maxAmount),
                        DensityTonnesPerUnit =
                            Math.Max(0.0, resource.info.density),
                        FlowEnabled = resource.flowState
                    });
            }
        }

        private static void ReadEngineRequirements(
            Part part,
            VesselTopologyNode node)
        {
            node.PropellantRequirements.Clear();

            if (part.Modules == null)
            {
                return;
            }

            for (int moduleIndex = 0;
                 moduleIndex < part.Modules.Count;
                 moduleIndex++)
            {
                ModuleEngines engine =
                    part.Modules[moduleIndex] as ModuleEngines;

                if (engine == null ||
                    engine.propellants == null)
                {
                    continue;
                }

                for (int propellantIndex = 0;
                     propellantIndex < engine.propellants.Count;
                     propellantIndex++)
                {
                    Propellant propellant =
                        engine.propellants[propellantIndex];

                    if (propellant == null)
                    {
                        continue;
                    }

                    PartResourceDefinition definition =
                        PartResourceLibrary.Instance.GetDefinition(
                            propellant.name);

                    object flowMode =
                        ReadMemberValue(propellant, "GetFlowMode") ??
                        ReadMemberValue(propellant, "flowMode");

                    if (flowMode == null &&
                        definition != null)
                    {
                        flowMode =
                            ReadMemberValue(
                                definition,
                                "resourceFlowMode");
                    }

                    node.PropellantRequirements.Add(
                        new VesselPropellantRequirement
                        {
                            ResourceId =
                                definition != null
                                    ? definition.id
                                    : -1,
                            Name =
                                propellant.name ?? string.Empty,
                            Ratio =
                                Math.Max(0.0, propellant.ratio),
                            DensityTonnesPerUnit =
                                definition != null
                                    ? Math.Max(0.0, definition.density)
                                    : 0.0,
                            RawFlowMode =
                                flowMode != null
                                    ? flowMode.ToString()
                                    : "UNKNOWN"
                        });
                }
            }
        }

        private static void ResolveReachableSources(
            IDictionary<uint, VesselTopologyNode> nodes)
        {
            foreach (KeyValuePair<uint, VesselTopologyNode> pair in nodes)
            {
                VesselTopologyNode engineNode = pair.Value;

                for (int requirementIndex = 0;
                     requirementIndex <
                        engineNode.PropellantRequirements.Count;
                     requirementIndex++)
                {
                    VesselPropellantRequirement requirement =
                        engineNode.PropellantRequirements[requirementIndex];

                    requirement.ReachableSourcePartIds.Clear();

                    HashSet<uint> visited =
                        new HashSet<uint>();

                    Queue<uint> pending =
                        new Queue<uint>();

                    visited.Add(engineNode.PartId);
                    pending.Enqueue(engineNode.PartId);

                    while (pending.Count > 0)
                    {
                        uint currentId = pending.Dequeue();
                        VesselTopologyNode current;

                        if (!nodes.TryGetValue(currentId, out current))
                        {
                            continue;
                        }

                        if (StoresUsableResource(
                                current,
                                requirement.Name))
                        {
                            requirement.ReachableSourcePartIds.Add(
                                current.PartId);
                        }

                        if (!current.AllowsCrossFeed)
                        {
                            continue;
                        }

                        EnqueueLinked(
                            current.ParentPartId,
                            current.HasParent,
                            current,
                            nodes,
                            visited,
                            pending);

                        for (int childIndex = 0;
                             childIndex < current.ChildPartIds.Count;
                             childIndex++)
                        {
                            EnqueueLinked(
                                current.ChildPartIds[childIndex],
                                true,
                                current,
                                nodes,
                                visited,
                                pending);
                        }
                    }
                }
            }
        }

        private static void EnqueueLinked(
            uint linkedId,
            bool hasLink,
            VesselTopologyNode current,
            IDictionary<uint, VesselTopologyNode> nodes,
            ISet<uint> visited,
            Queue<uint> pending)
        {
            if (!hasLink ||
                visited.Contains(linkedId))
            {
                return;
            }

            VesselTopologyNode linked;

            if (!nodes.TryGetValue(linkedId, out linked) ||
                !linked.AllowsCrossFeed ||
                !current.AllowsCrossFeed)
            {
                return;
            }

            visited.Add(linkedId);
            pending.Enqueue(linkedId);
        }

        private static bool StoresUsableResource(
            VesselTopologyNode node,
            string resourceName)
        {
            for (int index = 0;
                 index < node.Resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    node.Resources[index];

                if (resource.FlowEnabled &&
                    resource.Amount > 0.0 &&
                    string.Equals(
                        resource.Name,
                        resourceName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReadFuelCrossFeed(
            Part part)
        {
            object value =
                ReadMemberValue(part, "fuelCrossFeed");

            return value is bool
                ? (bool)value
                : true;
        }

        private static object ReadMemberValue(
            object instance,
            string memberName)
        {
            if (instance == null ||
                string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = instance.GetType();

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
                    return method.Invoke(instance, null);
                }

                FieldInfo field =
                    type.GetField(
                        memberName,
                        BindingFlags.Instance |
                        BindingFlags.Public |
                        BindingFlags.NonPublic);

                if (field != null)
                {
                    return field.GetValue(instance);
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
                    return property.GetValue(instance, null);
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
