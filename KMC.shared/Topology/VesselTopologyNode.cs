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

            StoredResourceNames =
                new List<string>();

            PartName =
                string.Empty;

            PartTitle =
                string.Empty;

            Category =
                VesselNodeCategory.Unknown;

            Roles =
                VesselNodeRole.None;
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

        public List<uint> ChildPartIds { get; private set; }

        public List<uint> SymmetryPartIds { get; private set; }

        /// <summary>
        /// Resource names stored by the part. Amounts and capacities will be
        /// added to the live-state packet in a later phase.
        /// </summary>
        public List<string> StoredResourceNames { get; private set; }

        public bool HasRole(
            VesselNodeRole role)
        {
            return
                (Roles & role) ==
                role;
        }
    }
}
