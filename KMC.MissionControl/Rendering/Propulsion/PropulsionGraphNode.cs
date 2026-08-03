using System.Collections.Generic;
using KMC.Shared.Topology;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Renderer-facing vessel node. This is deliberately independent of GDI+
    /// coordinates so layout and drawing remain separate phases.
    /// </summary>
    public sealed class PropulsionGraphNode
    {
        public PropulsionGraphNode()
        {
            Title = string.Empty;
            PartName = string.Empty;
            ResourceNames = new List<string>();
            PropellantNames = new List<string>();
            SourcePartIds = new List<uint>();
        }

        public uint PartId { get; set; }

        public string Title { get; set; }

        public string PartName { get; set; }

        public VesselNodeCategory Category { get; set; }

        public VesselNodeRole Roles { get; set; }

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public int StructuralDepth { get; set; }

        public uint BranchRootPartId { get; set; }

        public uint SymmetryGroupId { get; set; }

        public bool IsRoot { get; set; }

        public bool IsSeparationBoundary { get; set; }

        public bool SurvivesNextStage { get; set; }

        public double DryMassTonnes { get; set; }

        public double ResourceMassTonnes { get; set; }

        public double VesselX { get; set; }

        public double VesselY { get; set; }

        public double VesselZ { get; set; }

        public List<string> ResourceNames { get; private set; }

        public List<string> PropellantNames { get; private set; }

        /// <summary>
        /// Unique resource-source parts reachable by engines represented by
        /// this node.
        /// </summary>
        public List<uint> SourcePartIds { get; private set; }
    }
}
