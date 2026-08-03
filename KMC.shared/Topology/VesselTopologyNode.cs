using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    /// <summary>
    /// KSP-independent snapshot of one vessel part.
    ///
    /// This class deliberately contains no KSP or Unity types so it can be
    /// serialized and transmitted to a remote Mission Control client later.
    /// </summary>
    public sealed class VesselTopologyNode
    {
        public VesselTopologyNode()
        {
            ChildPartIds =
                new List<uint>();

            SymmetryPartIds =
                new List<uint>();

            PartName =
                string.Empty;

            PartTitle =
                string.Empty;
        }

        public uint PartId { get; set; }

        public uint ParentPartId { get; set; }

        public bool HasParent { get; set; }

        public string PartName { get; set; }

        public string PartTitle { get; set; }

        public int InverseStage { get; set; }

        public VesselAttachmentType AttachmentType { get; set; }

        public double DryMassTonnes { get; set; }

        public double ResourceMassTonnes { get; set; }

        /// <summary>
        /// Position in vessel-reference coordinates. These values are
        /// diagnostic hints only; the future schematic layout engine will
        /// primarily use the topology tree.
        /// </summary>
        public double VesselX { get; set; }

        public double VesselY { get; set; }

        public double VesselZ { get; set; }

        public List<uint> ChildPartIds { get; private set; }

        public List<uint> SymmetryPartIds { get; private set; }
    }
}
