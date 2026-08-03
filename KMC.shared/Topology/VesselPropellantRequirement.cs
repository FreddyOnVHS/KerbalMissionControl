using System.Collections.Generic;

namespace KMC.Shared.Topology
{
    /// <summary>
    /// One propellant consumed by an engine and the vessel parts that can
    /// currently supply it through the simplified Phase 2C crossfeed graph.
    /// </summary>
    public sealed class VesselPropellantRequirement
    {
        public VesselPropellantRequirement()
        {
            Name = string.Empty;
            RawFlowMode = string.Empty;
            ReachableSourcePartIds = new List<uint>();
        }

        public int ResourceId { get; set; }

        public string Name { get; set; }

        public double Ratio { get; set; }

        public double DensityTonnesPerUnit { get; set; }

        public string RawFlowMode { get; set; }

        public List<uint> ReachableSourcePartIds { get; private set; }
    }
}
