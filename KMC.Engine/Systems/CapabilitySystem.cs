using System.Collections.Generic;
using KMC.Engine.Analysis;
using KMC.Engine.Capabilities;
using KMC.Engine.Models;
using KMC.Shared.Topology;

namespace KMC.Engine.Systems
{
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

            VesselCapabilitySnapshot details =
                VesselCapabilityBuilder.Build(topology);

            context.Capabilities.Details =
                details;

            int classifiedParts = 0;
            int unclassifiedParts = 0;

            for (int partIndex = 0;
                 partIndex < details.Parts.Count;
                 partIndex++)
            {
                PartCapabilitySnapshot part =
                    details.Parts[partIndex];

                if (part == null)
                {
                    continue;
                }

                HashSet<VesselCapabilityType> aggregate =
                    new HashSet<VesselCapabilityType>();

                for (int capabilityIndex = 0;
                     capabilityIndex < part.Capabilities.Count;
                     capabilityIndex++)
                {
                    PartCapability capability =
                        part.Capabilities[capabilityIndex];

                    if (capability == null ||
                        capability.Type == PartCapabilityType.Unknown)
                    {
                        continue;
                    }

                    VesselCapabilityType aggregateType;

                    if (TryMap(capability.Type, out aggregateType))
                    {
                        aggregate.Add(aggregateType);
                    }
                }

                if (aggregate.Count == 0)
                {
                    unclassifiedParts++;
                }
                else
                {
                    classifiedParts++;
                }

                foreach (VesselCapabilityType capability in aggregate)
                {
                    context.Capabilities.AddPartCapability(capability);
                }
            }

            context.Capabilities.ClassifiedPartCount = classifiedParts;
            context.Capabilities.UnclassifiedPartCount = unclassifiedParts;

            if (context.Vessel.PartCount > 0)
            {
                context.Capabilities.Add("VesselTopology");
            }

            context.AddDiagnostic(
                "Capability analysis completed by KMC.Engine detailed classifier. " +
                "ClassifiedParts=" +
                classifiedParts +
                ", UnclassifiedParts=" +
                unclassifiedParts +
                ".");
        }

        private static bool TryMap(
            PartCapabilityType source,
            out VesselCapabilityType target)
        {
            switch (source)
            {
                case PartCapabilityType.Command:
                    target = VesselCapabilityType.Command;
                    return true;
                case PartCapabilityType.CrewSupport:
                    target = VesselCapabilityType.CrewSupport;
                    return true;
                case PartCapabilityType.ElectricalStorage:
                    target = VesselCapabilityType.ElectricalStorage;
                    return true;
                case PartCapabilityType.ElectricalProducer:
                    target = VesselCapabilityType.ElectricalProducer;
                    return true;
                case PartCapabilityType.ResourceStorage:
                    target = VesselCapabilityType.ResourceStorage;
                    return true;
                case PartCapabilityType.ResourceConsumer:
                    target = VesselCapabilityType.ResourceConsumer;
                    return true;
                case PartCapabilityType.Propulsion:
                    target = VesselCapabilityType.Propulsion;
                    return true;
                case PartCapabilityType.ReactionControl:
                    target = VesselCapabilityType.ReactionControl;
                    return true;
                case PartCapabilityType.AttitudeControl:
                    target = VesselCapabilityType.AttitudeControl;
                    return true;
                case PartCapabilityType.Communication:
                    target = VesselCapabilityType.Communication;
                    return true;
                case PartCapabilityType.Science:
                    target = VesselCapabilityType.Science;
                    return true;
                case PartCapabilityType.Docking:
                    target = VesselCapabilityType.Docking;
                    return true;
                case PartCapabilityType.Separation:
                    target = VesselCapabilityType.Separation;
                    return true;
                case PartCapabilityType.Structural:
                    target = VesselCapabilityType.Structural;
                    return true;
                default:
                    target = VesselCapabilityType.Unknown;
                    return false;
            }
        }
    }
}
