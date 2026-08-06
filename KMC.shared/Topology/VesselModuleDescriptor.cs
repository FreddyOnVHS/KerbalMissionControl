using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    public sealed class VesselModuleDescriptor
    {
        public VesselModuleDescriptor()
        {
            ModuleName = string.Empty;
            ModuleTypeName = string.Empty;
            DisplayName = string.Empty;
            StatusText = string.Empty;
            InputResources = new List<VesselModuleResource>();
            OutputResources = new List<VesselModuleResource>();
        }

        public string ModuleName { get; set; }

        public string ModuleTypeName { get; set; }

        public string DisplayName { get; set; }

        public bool IsEnabled { get; set; }

        public bool HasActiveState { get; set; }

        public bool IsActive { get; set; }

        public string StatusText { get; set; }

        public List<VesselModuleResource> InputResources { get; private set; }

        public List<VesselModuleResource> OutputResources { get; private set; }
    }
}
