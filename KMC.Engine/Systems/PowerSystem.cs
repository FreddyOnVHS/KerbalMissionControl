using KMC.Engine.Analysis;
using KMC.Engine.Electrical;

namespace KMC.Engine.Systems
{
    public sealed class PowerSystem :
        IEngineeringSystem
    {
        public string Name
        {
            get { return "Power"; }
        }

        public int Order
        {
            get { return 200; }
        }

        public void Analyze(
            AnalysisContext context)
        {
            ElectricalNetwork network =
                ElectricalNetworkBuilder.Build(
                    context.Vessel.Topology);

            network.Storage =
                ElectricalStorageAnalyzer.Analyze(
                    context.Vessel.Topology,
                    network);

            context.Power.ElectricalNetwork =
                network;

            context.Power.Diagnostics.Clear();

            for (int index = 0;
                 index < network.Diagnostics.Count;
                 index++)
            {
                context.Power.Diagnostics.Add(
                    network.Diagnostics[index]);
            }

            for (int index = 0;
                 index < network.Storage.Diagnostics.Count;
                 index++)
            {
                context.Power.Diagnostics.Add(
                    network.Storage.Diagnostics[index]);
            }

            context.AddDiagnostic(
                "Electrical engineering model built. " +
                "Nodes=" +
                network.Nodes.Count +
                ", BusMembers=" +
                network.BusMemberships.Count +
                ", StructuralLinks=" +
                network.StructuralConnections.Count +
                ", StorageParts=" +
                network.Storage.Parts.Count +
                ", EC=" +
                network.Storage.StoredEc.ToString("0.###") +
                "/" +
                network.Storage.CapacityEc.ToString("0.###") +
                ".");
        }
    }
}
