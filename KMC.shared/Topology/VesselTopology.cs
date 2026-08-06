using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    public sealed class VesselTopology
    {
        public VesselTopology()
        {
            VesselId = string.Empty;
            VesselName = string.Empty;
            Nodes = new List<VesselTopologyNode>();
        }

        public int TransportVersion { get; set; }
        public string VesselId { get; set; }
        public string VesselName { get; set; }
        public uint RootPartId { get; set; }
        public bool HasRootPart { get; set; }
        public int PartCount { get; set; }
        public int MaximumInverseStage { get; set; }
        public int CurrentStage { get; set; }

        public int NextStage
        {
            get
            {
                return CurrentStage > 0
                    ? CurrentStage - 1
                    : -1;
            }
        }

        public int StructuralBranchCount { get; set; }
        public int SymmetryGroupCount { get; set; }
        public int SeparationBoundaryCount { get; set; }
        public long Revision { get; set; }
        public List<VesselTopologyNode> Nodes { get; private set; }
    }
}
