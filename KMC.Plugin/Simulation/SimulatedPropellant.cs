namespace KMC.Plugin.Simulation
{
    internal sealed class SimulatedPropellant
    {
        public int ResourceId { get; set; }

        public string Name { get; set; }

        public double Ratio { get; set; }

        public double DensityTonnesPerUnit { get; set; }

        public ResourceFlowCategory FlowCategory
        {
            get;
            set;
        }

        public string RawFlowMode { get; set; }
    }
}
