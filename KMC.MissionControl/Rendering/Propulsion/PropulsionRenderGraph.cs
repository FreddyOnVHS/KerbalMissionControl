using System.Collections.Generic;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Complete logical graph consumed by the upcoming automatic layout
    /// engine. It contains no screen coordinates and no drawing code.
    /// </summary>
    public sealed class PropulsionRenderGraph
    {
        public PropulsionRenderGraph()
        {
            VesselName = string.Empty;
            Nodes = new List<PropulsionGraphNode>();
            Edges = new List<PropulsionGraphEdge>();
        }

        public string VesselName { get; set; }

        public long TopologyRevision { get; set; }

        public uint RootPartId { get; set; }

        public bool HasRootPart { get; set; }

        public int CurrentStage { get; set; }

        public int NextStage { get; set; }

        public int CollapsedPartCount { get; set; }

        public List<PropulsionGraphNode> Nodes { get; private set; }

        public List<PropulsionGraphEdge> Edges { get; private set; }
    }
}
