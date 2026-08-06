using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    public sealed class VesselTopologyNode
    {
        public VesselTopologyNode()
        {
            ChildPartIds = new List<uint>();
            StackChildPartIds = new List<uint>();
            SurfaceChildPartIds = new List<uint>();
            SymmetryPartIds = new List<uint>();
            StoredResourceNames = new List<string>();
            Resources = new List<VesselResourceState>();
            PropellantRequirements = new List<VesselPropellantRequirement>();
            Modules = new List<VesselModuleDescriptor>();

            PartName = string.Empty;
            PartTitle = string.Empty;
            Category = VesselNodeCategory.Unknown;
            Roles = VesselNodeRole.None;
            ActivationStage = -1;
            SeparationStage = -1;
            AllowsCrossFeed = true;
        }

        public uint PartId { get; set; }
        public uint ParentPartId { get; set; }
        public bool HasParent { get; set; }
        public string PartName { get; set; }
        public string PartTitle { get; set; }
        public int InverseStage { get; set; }
        public VesselAttachmentType AttachmentType { get; set; }
        public VesselNodeCategory Category { get; set; }
        public VesselNodeRole Roles { get; set; }
        public double DryMassTonnes { get; set; }
        public double ResourceMassTonnes { get; set; }
        public double VesselX { get; set; }
        public double VesselY { get; set; }
        public double VesselZ { get; set; }

        public int StructuralDepth { get; set; }
        public uint SymmetryGroupId { get; set; }
        public uint BranchRootPartId { get; set; }
        public int ActivationStage { get; set; }
        public int SeparationStage { get; set; }
        public bool IsSeparationBoundary { get; set; }
        public bool WillSeparateOnNextStage { get; set; }
        public bool AllowsCrossFeed { get; set; }

        public bool SurvivesNextStage
        {
            get { return !WillSeparateOnNextStage; }
        }

        public List<uint> ChildPartIds { get; private set; }
        public List<uint> StackChildPartIds { get; private set; }
        public List<uint> SurfaceChildPartIds { get; private set; }
        public List<uint> SymmetryPartIds { get; private set; }
        public List<string> StoredResourceNames { get; private set; }
        public List<VesselResourceState> Resources { get; private set; }
        public List<VesselPropellantRequirement> PropellantRequirements { get; private set; }
        public List<VesselModuleDescriptor> Modules { get; private set; }

        public bool HasRole(
            VesselNodeRole role)
        {
            return (Roles & role) == role;
        }
    }
}
