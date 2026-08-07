using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    public sealed class ElectricalNetwork
    {
        public const string VesselElectricChargeBusId =
            "VESSEL_EC";

        public ElectricalNetwork()
        {
            VesselName =
                string.Empty;

            Nodes =
                new List<PowerNode>();

            Connections =
                new List<PowerConnection>();

            BusMemberships =
                new List<ElectricalBusMembership>();

            StructuralConnections =
                new List<StructuralConnection>();

            Storage =
                new ElectricalStorageModel();

            Diagnostics =
                new List<string>();
        }

        public string VesselName { get; set; }

        public long TopologyRevision { get; set; }

        public int CurrentStage { get; set; }

        public List<PowerNode> Nodes { get; private set; }

        public List<PowerConnection> Connections { get; private set; }

        public List<ElectricalBusMembership> BusMemberships { get; private set; }

        public List<StructuralConnection> StructuralConnections { get; private set; }

        public ElectricalStorageModel Storage { get; internal set; }

        public List<string> Diagnostics { get; private set; }

        public int StructuralPartCount { get; internal set; }

        public int SourceNodeCount { get; internal set; }

        public int StorageNodeCount { get; internal set; }

        public int ConsumerNodeCount { get; internal set; }

        public int ExplicitConsumerNodeCount { get; internal set; }

        public int PotentialConsumerNodeCount { get; internal set; }

        public double StoredElectricCharge { get; internal set; }

        public double ElectricChargeCapacity { get; internal set; }

        public bool HasAnyElectricalSystem
        {
            get
            {
                return
                    Nodes.Count > 0;
            }
        }
    }
}
