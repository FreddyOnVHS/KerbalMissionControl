using System;
using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Capabilities
{
    public static class VesselCapabilityBuilder
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

            result.VesselName = topology.VesselName;
            result.TopologyRevision = topology.Revision;
            result.CurrentStage = topology.CurrentStage;

            HashSet<string> unknown =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < topology.Nodes.Count; i++)
            {
                VesselTopologyNode node = topology.Nodes[i];

                if (node == null)
                {
                    continue;
                }

                PartCapabilitySnapshot part =
                    PartCapabilityClassifier.Classify(node);

                result.Parts.Add(part);

                for (int r = 0; r < part.Resources.Count; r++)
                {
                    ResourceDescriptor resource =
                        part.Resources[r];

                    if (!resource.IsKnown &&
                        !string.IsNullOrEmpty(
                            resource.InternalName))
                    {
                        unknown.Add(
                            resource.InternalName);
                    }
                }
            }

            foreach (string resource in unknown)
            {
                result.UnknownResources.Add(resource);
            }

            result.Diagnostics.Add(
                "Phase 1 uses existing roles, resources, and propellant requirements.");

            result.Diagnostics.Add(
                "Raw module names and converter rates require the next packet phase.");

            return result;
        }
    }
}
