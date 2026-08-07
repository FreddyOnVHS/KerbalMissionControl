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

            context.AddDiagnostic(
                "Electrical domain model built. " +
                "Nodes=" +
                network.Nodes.Count +
                ", Sources=" +
                network.SourceNodeCount +
                ", Storage=" +
                network.StorageNodeCount +
                ", Consumers=" +
                network.ConsumerNodeCount +
                ", Connections=" +
                network.Connections.Count +
                ".");
        }
    }
}
