using System.Collections.Generic;
using KMC.Engine.Analysis;
using KMC.Engine.Models;
using KMC.Shared.Topology;

namespace KMC.Engine.Systems
{
    /// <summary>
    /// Builds vessel-level engineering capabilities from the topology supplied
    /// by the KSP plugin. This milestone intentionally uses only data already
    /// present in Shared topology packets: roles, stored resources, and
    /// propellant requirements.
    ///
    /// Existing Mission Control capability classification remains in place for
    /// parallel verification and is not consumed by this system.
    /// </summary>
    public sealed class CapabilitySystem :
        IEngineeringSystem
    {
        public string Name
        {
            get { return "Capabilities"; }
        }

        public int Order
        {
            get { return 100; }
        }

        public void Analyze(
            AnalysisContext context)
        {
            VesselTopology topology =
                context.Vessel.Topology;

            if (topology == null ||
                topology.Nodes == null)
            {
                context.AddDiagnostic(
                    "Capability analysis skipped: vessel topology is unavailable.");

                return;
            }

            int classifiedParts =
                0;

            int unclassifiedParts =
                0;

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

                HashSet<VesselCapabilityType> partCapabilities =
                    ClassifyPart(
                        node);

                if (partCapabilities.Count ==
                    0)
                {
                    unclassifiedParts++;

                    continue;
                }

                classifiedParts++;

                foreach (VesselCapabilityType capability
                    in partCapabilities)
                {
                    context.Capabilities.Add(
                        capability);
                }
            }

            context.Capabilities.ClassifiedPartCount =
                classifiedParts;

            context.Capabilities.UnclassifiedPartCount =
                unclassifiedParts;

            /*
             * Retain the original foundation marker for compatibility with
             * any early diagnostics that may still query it by name.
             */
            if (context.Vessel.PartCount >
                0)
            {
                context.Capabilities.Add(
                    "VesselTopology");
            }

            context.AddDiagnostic(
                "Capability analysis completed from topology roles/resources. " +
                "ClassifiedParts=" +
                classifiedParts +
                ", UnclassifiedParts=" +
                unclassifiedParts +
                ".");
        }

        private static HashSet<VesselCapabilityType>
            ClassifyPart(
                VesselTopologyNode node)
        {
            HashSet<VesselCapabilityType> result =
                new HashSet<VesselCapabilityType>();

            AddRole(
                node,
                VesselNodeRole.Command,
                VesselCapabilityType.Command,
                result);

            AddRole(
                node,
                VesselNodeRole.Crew,
                VesselCapabilityType.CrewSupport,
                result);

            AddRole(
                node,
                VesselNodeRole.StoresElectricCharge,
                VesselCapabilityType.ElectricalStorage,
                result);

            if (node.HasRole(
                    VesselNodeRole.SolarGeneration) ||
                node.HasRole(
                    VesselNodeRole.ElectricalGeneration) ||
                node.HasRole(
                    VesselNodeRole.FuelCell))
            {
                result.Add(
                    VesselCapabilityType.ElectricalProducer);
            }

            if (node.HasRole(
                    VesselNodeRole.Engine) ||
                node.HasRole(
                    VesselNodeRole.SolidPropulsion) ||
                node.HasRole(
                    VesselNodeRole.LiquidPropulsion))
            {
                result.Add(
                    VesselCapabilityType.Propulsion);
            }

            AddRole(
                node,
                VesselNodeRole.RcsThruster,
                VesselCapabilityType.ReactionControl,
                result);

            AddRole(
                node,
                VesselNodeRole.ReactionWheel,
                VesselCapabilityType.AttitudeControl,
                result);

            AddRole(
                node,
                VesselNodeRole.Antenna,
                VesselCapabilityType.Communication,
                result);

            AddRole(
                node,
                VesselNodeRole.Science,
                VesselCapabilityType.Science,
                result);

            AddRole(
                node,
                VesselNodeRole.DockingPort,
                VesselCapabilityType.Docking,
                result);

            if (node.HasRole(
                    VesselNodeRole.Decoupler) ||
                node.HasRole(
                    VesselNodeRole.Separator))
            {
                result.Add(
                    VesselCapabilityType.Separation);
            }

            if (node.HasRole(
                    VesselNodeRole.Structural) ||
                node.HasRole(
                    VesselNodeRole.Fairing))
            {
                result.Add(
                    VesselCapabilityType.Structural);
            }

            AddResourceCapabilities(
                node,
                result);

            AddConsumptionCapabilities(
                node,
                result);

            return result;
        }

        private static void AddRole(
            VesselTopologyNode node,
            VesselNodeRole role,
            VesselCapabilityType capability,
            HashSet<VesselCapabilityType> result)
        {
            if (node.HasRole(
                    role))
            {
                result.Add(
                    capability);
            }
        }

        private static void AddResourceCapabilities(
            VesselTopologyNode node,
            HashSet<VesselCapabilityType> result)
        {
            if (node.Resources ==
                null)
            {
                return;
            }

            for (int index = 0;
                 index < node.Resources.Count;
                 index++)
            {
                VesselResourceState resource =
                    node.Resources[index];

                if (resource == null ||
                    string.IsNullOrEmpty(
                        resource.Name))
                {
                    continue;
                }

                if (string.Equals(
                        resource.Name,
                        "ElectricCharge",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(
                        VesselCapabilityType.ElectricalStorage);
                }
                else
                {
                    result.Add(
                        VesselCapabilityType.ResourceStorage);
                }
            }
        }

        private static void AddConsumptionCapabilities(
            VesselTopologyNode node,
            HashSet<VesselCapabilityType> result)
        {
            if (node.PropellantRequirements ==
                null ||
                node.PropellantRequirements.Count ==
                0)
            {
                return;
            }

            for (int index = 0;
                 index < node.PropellantRequirements.Count;
                 index++)
            {
                VesselPropellantRequirement requirement =
                    node.PropellantRequirements[index];

                if (requirement == null ||
                    string.IsNullOrEmpty(
                        requirement.Name))
                {
                    continue;
                }

                result.Add(
                    VesselCapabilityType.ResourceConsumer);

                return;
            }
        }
    }
}
