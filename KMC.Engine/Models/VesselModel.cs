using System;
using KMC.Shared.Topology;

namespace KMC.Engine.Models
{
    public sealed class VesselModel
    {
        public VesselModel(VesselTopology topology)
        {
            Topology = topology ?? throw new ArgumentNullException(nameof(topology));
            VesselId = topology.VesselId ?? string.Empty;
            VesselName = topology.VesselName ?? string.Empty;
            TopologyRevision = topology.Revision;
            PartCount = topology.PartCount;
            CurrentStage = topology.CurrentStage;
        }

        public string VesselId { get; }
        public string VesselName { get; }
        public long TopologyRevision { get; }
        public int PartCount { get; }
        public int CurrentStage { get; }
        public VesselTopology Topology { get; }
    }
}
