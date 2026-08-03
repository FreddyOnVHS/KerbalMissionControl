using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    /// <summary>
    /// KSP-independent snapshot of one vessel part.
    /// </summary>
    public sealed class VesselTopologyNode
    {
        public VesselTopologyNode()
        {
            ChildPartIds =
                new List<uint>();

            StackChildPartIds =
                new List<uint>();

            SurfaceChildPartIds =
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

            ActivationStage =
                -1;

            SeparationStage =
                -1;
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

        /// <summary>
        /// Distance from the vessel root in parent-child links.
        /// </summary>
        public int StructuralDepth { get; set; }

        /// <summary>
        /// Stable representative ID for this part's symmetry family.
        /// Zero means no symmetry family was found.
        /// </summary>
        public uint SymmetryGroupId { get; set; }

        /// <summary>
        /// First node in this structural branch below the vessel root.
        /// Root itself uses its own PartId.
        /// </summary>
        public uint BranchRootPartId { get; set; }

        /// <summary>
        /// Stage that activates this part. -1 means no staged activation.
        /// </summary>
        public int ActivationStage { get; set; }

        /// <summary>
        /// Stage whose decoupler boundary discards this node. -1 means the
        /// node is on the retained/root side of every known boundary.
        /// </summary>
        public int SeparationStage { get; set; }

        public bool IsSeparationBoundary { get; set; }

        public bool WillSeparateOnNextStage { get; set; }

        public bool SurvivesNextStage
        {
            get { return !WillSeparateOnNextStage; }
        }

        public List<uint> ChildPartIds { get; private set; }

        public List<uint> StackChildPartIds { get; private set; }

        public List<uint> SurfaceChildPartIds { get; private set; }

        public List<uint> SymmetryPartIds { get; private set; }

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
