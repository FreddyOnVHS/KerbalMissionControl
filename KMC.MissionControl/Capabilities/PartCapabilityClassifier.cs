using System;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Capabilities
{
    public static class PartCapabilityClassifier
    {
        public static PartCapabilitySnapshot Classify(
            VesselTopologyNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            PartCapabilitySnapshot result =
                new PartCapabilitySnapshot
                {
                    PartId = node.PartId,
                    ParentPartId = node.ParentPartId,
                    HasParent = node.HasParent,
                    PartName = node.PartName,
                    PartTitle = node.PartTitle,
                    ActivationStage = node.ActivationStage,
                    SeparationStage = node.SeparationStage
                };

            AddRoles(node, result);
            AddStoredResources(node, result);
            AddRequirements(node, result);

            if (result.Capabilities.Count == 0)
            {
                result.Capabilities.Add(
                    NewCapability(
                        PartCapabilityType.Unknown,
                        "Unclassified Part",
                        "No current topology rule matched.",
                        CapabilitySource.Unknown,
                        ClassificationConfidence.Low));

                result.Diagnostics.Add(
                    "No role, stored resource, or propellant rule matched.");
            }

            return result;
        }

        private static void AddRoles(
            VesselTopologyNode node,
            PartCapabilitySnapshot result)
        {
            AddRole(node, result, VesselNodeRole.Command,
                PartCapabilityType.Command, "Command");
            AddRole(node, result, VesselNodeRole.Crew,
                PartCapabilityType.CrewSupport, "Crew");
            AddRole(node, result, VesselNodeRole.StoresElectricCharge,
                PartCapabilityType.ElectricalStorage, "Battery / EC Storage");
            AddRole(node, result, VesselNodeRole.SolarGeneration,
                PartCapabilityType.ElectricalProducer, "Solar Generator");
            AddRole(node, result, VesselNodeRole.ElectricalGeneration,
                PartCapabilityType.ElectricalProducer, "Electrical Generator");
            AddRole(node, result, VesselNodeRole.FuelCell,
                PartCapabilityType.ElectricalProducer, "Fuel Cell");
            AddRole(node, result, VesselNodeRole.Engine,
                PartCapabilityType.Propulsion, "Engine");
            AddRole(node, result, VesselNodeRole.RcsThruster,
                PartCapabilityType.ReactionControl, "RCS Thruster");
            AddRole(node, result, VesselNodeRole.ReactionWheel,
                PartCapabilityType.AttitudeControl, "Reaction Wheel");
            AddRole(node, result, VesselNodeRole.Antenna,
                PartCapabilityType.Communication, "Antenna");
            AddRole(node, result, VesselNodeRole.Science,
                PartCapabilityType.Science, "Science");
            AddRole(node, result, VesselNodeRole.DockingPort,
                PartCapabilityType.Docking, "Docking Port");

            if (node.HasRole(VesselNodeRole.Decoupler) ||
                node.HasRole(VesselNodeRole.Separator))
            {
                result.Capabilities.Add(
                    NewCapability(
                        PartCapabilityType.Separation,
                        "Separation Device",
                        "Derived from decoupler/separator role.",
                        CapabilitySource.ExistingRole,
                        ClassificationConfidence.Explicit));
            }

            if (node.HasRole(VesselNodeRole.Structural) ||
                node.HasRole(VesselNodeRole.Fairing))
            {
                result.Capabilities.Add(
                    NewCapability(
                        PartCapabilityType.Structural,
                        "Structural",
                        "Derived from structural/fairing role.",
                        CapabilitySource.ExistingRole,
                        ClassificationConfidence.Explicit));
            }
        }

        private static void AddRole(
            VesselTopologyNode node,
            PartCapabilitySnapshot result,
            VesselNodeRole role,
            PartCapabilityType type,
            string subtype)
        {
            if (!node.HasRole(role))
            {
                return;
            }

            result.Capabilities.Add(
                NewCapability(
                    type,
                    subtype,
                    "Derived from VesselNodeRole." + role,
                    CapabilitySource.ExistingRole,
                    ClassificationConfidence.Explicit));
        }

        private static void AddStoredResources(
            VesselTopologyNode node,
            PartCapabilitySnapshot result)
        {
            for (int i = 0; i < node.Resources.Count; i++)
            {
                VesselResourceState state = node.Resources[i];

                if (state == null)
                {
                    continue;
                }

                ResourceDescriptor resource =
                    ResourceClassifier.Classify(state.Name);

                resource.IsStored = true;
                resource.Amount = state.Amount;
                resource.Capacity = state.Capacity;
                result.Resources.Add(resource);

                if (resource.Category == ResourceCategory.Electrical)
                {
                    Ensure(
                        result,
                        PartCapabilityType.ElectricalStorage,
                        "Battery / EC Storage",
                        CapabilitySource.StoredResource,
                        ClassificationConfidence.High);
                }
                else
                {
                    Ensure(
                        result,
                        PartCapabilityType.ResourceStorage,
                        resource.IsKnown
                            ? resource.DisplayName + " Storage"
                            : "Unknown Resource Storage",
                        CapabilitySource.StoredResource,
                        resource.IsKnown
                            ? ClassificationConfidence.High
                            : ClassificationConfidence.Medium);
                }

                if (!resource.IsKnown)
                {
                    result.Diagnostics.Add(
                        "Unknown stored resource: " +
                        resource.InternalName);
                }
            }
        }

        private static void AddRequirements(
            VesselTopologyNode node,
            PartCapabilitySnapshot result)
        {
            for (int i = 0;
                 i < node.PropellantRequirements.Count;
                 i++)
            {
                VesselPropellantRequirement requirement =
                    node.PropellantRequirements[i];

                if (requirement == null)
                {
                    continue;
                }

                ResourceDescriptor resource =
                    FindOrCreate(
                        result,
                        requirement.ResourceName);

                resource.IsConsumed = true;
                resource.RequiredRatio = requirement.Ratio;

                Ensure(
                    result,
                    PartCapabilityType.ResourceConsumer,
                    resource.IsKnown
                        ? resource.DisplayName + " Consumer"
                        : "Unknown Resource Consumer",
                    CapabilitySource.PropellantRequirement,
                    resource.IsKnown
                        ? ClassificationConfidence.High
                        : ClassificationConfidence.Medium);

                if (!resource.IsKnown)
                {
                    result.Diagnostics.Add(
                        "Unknown consumed resource: " +
                        resource.InternalName);
                }
            }
        }

        private static ResourceDescriptor FindOrCreate(
            PartCapabilitySnapshot result,
            string name)
        {
            for (int i = 0; i < result.Resources.Count; i++)
            {
                if (string.Equals(
                        result.Resources[i].InternalName,
                        name,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return result.Resources[i];
                }
            }

            ResourceDescriptor resource =
                ResourceClassifier.Classify(name);

            result.Resources.Add(resource);
            return resource;
        }

        private static void Ensure(
            PartCapabilitySnapshot result,
            PartCapabilityType type,
            string subtype,
            CapabilitySource source,
            ClassificationConfidence confidence)
        {
            if (result.HasCapability(type))
            {
                return;
            }

            result.Capabilities.Add(
                NewCapability(
                    type,
                    subtype,
                    "Derived from resource behavior.",
                    source,
                    confidence));
        }

        private static PartCapability NewCapability(
            PartCapabilityType type,
            string subtype,
            string description,
            CapabilitySource source,
            ClassificationConfidence confidence)
        {
            return new PartCapability
            {
                Type = type,
                Subtype = subtype,
                Description = description,
                Source = source,
                Confidence = confidence
            };
        }
    }
}
