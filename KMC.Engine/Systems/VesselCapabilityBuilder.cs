using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Capabilities
{
    internal static class VesselCapabilityBuilder
    {
        public static VesselCapabilitySnapshot Build(
            VesselTopology topology)
        {
            VesselCapabilitySnapshot result =
                new VesselCapabilitySnapshot();

            if (topology == null)
            {
                result.Diagnostics.Add(
                    "No vessel topology has been received.");

                return result;
            }

            result.TransportVersion = topology.TransportVersion;
            result.VesselName = topology.VesselName;
            result.TopologyRevision = topology.Revision;
            result.CurrentStage = topology.CurrentStage;

            HashSet<string> unknown =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int index = 0;
                 index < topology.Nodes.Count;
                 index++)
            {
                VesselTopologyNode node =
                    topology.Nodes[index];

                if (node == null)
                {
                    continue;
                }

                PartCapabilitySnapshot part =
                    PartCapabilityClassifier.Classify(node);

                result.Parts.Add(part);

                for (int resourceIndex = 0;
                     resourceIndex < part.Resources.Count;
                     resourceIndex++)
                {
                    ResourceDescriptor resource =
                        part.Resources[resourceIndex];

                    if (!resource.IsKnown &&
                        !string.IsNullOrEmpty(resource.InternalName))
                    {
                        unknown.Add(resource.InternalName);
                    }
                }
            }

            foreach (string resource in unknown)
            {
                result.UnknownResources.Add(resource);
            }

            result.Diagnostics.Add(
                "Capability classification is owned by KMC.Engine.");

            result.Diagnostics.Add(
                topology.TransportVersion >= 2
                    ? "Raw module discovery is available from topology packet Version 2."
                    : "Raw module discovery is unavailable because topology packet Version 1 was received.");

            return result;
        }
    }
}
