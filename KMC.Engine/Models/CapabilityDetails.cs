using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.Engine.Capabilities
{
    public sealed class ResourceDescriptor
    {
        public ResourceDescriptor()
        {
            InternalName = string.Empty;
            DisplayName = string.Empty;
            Category = ResourceCategory.Unknown;
        }

        public string InternalName { get; set; }
        public string DisplayName { get; set; }
        public ResourceCategory Category { get; set; }
        public bool IsKnown { get; set; }
        public bool IsStored { get; set; }
        public bool IsConsumed { get; set; }
        public double Amount { get; set; }
        public double Capacity { get; set; }
        public double RequiredRatio { get; set; }
    }

    public sealed class PartCapability
    {
        public PartCapability()
        {
            Type = PartCapabilityType.Unknown;
            Subtype = string.Empty;
            Description = string.Empty;
            Source = CapabilitySource.Unknown;
            Confidence = ClassificationConfidence.Low;
        }

        public PartCapabilityType Type { get; set; }
        public string Subtype { get; set; }
        public string Description { get; set; }
        public CapabilitySource Source { get; set; }
        public ClassificationConfidence Confidence { get; set; }
    }

    public sealed class PartCapabilitySnapshot
    {
        public PartCapabilitySnapshot()
        {
            PartName = string.Empty;
            PartTitle = string.Empty;
            Capabilities = new List<PartCapability>();
            Resources = new List<ResourceDescriptor>();
            Modules = new List<VesselModuleDescriptor>();
            Diagnostics = new List<string>();
        }

        public uint PartId { get; set; }
        public uint ParentPartId { get; set; }
        public bool HasParent { get; set; }
        public string PartName { get; set; }
        public string PartTitle { get; set; }
        public int ActivationStage { get; set; }
        public int SeparationStage { get; set; }

        public List<PartCapability> Capabilities { get; private set; }
        public List<ResourceDescriptor> Resources { get; private set; }
        public List<VesselModuleDescriptor> Modules { get; private set; }
        public List<string> Diagnostics { get; private set; }

        public bool HasCapability(
            PartCapabilityType type)
        {
            for (int index = 0;
                 index < Capabilities.Count;
                 index++)
            {
                if (Capabilities[index].Type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class VesselCapabilitySnapshot
    {
        public VesselCapabilitySnapshot()
        {
            VesselName = string.Empty;
            Parts = new List<PartCapabilitySnapshot>();
            UnknownResources = new List<string>();
            Diagnostics = new List<string>();
        }

        public int TransportVersion { get; set; }
        public string VesselName { get; set; }
        public long TopologyRevision { get; set; }
        public int CurrentStage { get; set; }

        public List<PartCapabilitySnapshot> Parts { get; private set; }
        public List<string> UnknownResources { get; private set; }
        public List<string> Diagnostics { get; private set; }
    }
}
