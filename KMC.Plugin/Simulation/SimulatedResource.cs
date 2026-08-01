namespace KMC.Plugin.Simulation
{
    internal sealed class SimulatedResource
    {
        public int ResourceId { get; set; }

        public string Name { get; set; }

        public double Amount { get; set; }

        public double Capacity { get; set; }

        public double DensityTonnesPerUnit { get; set; }

        public double MassTonnes
        {
            get
            {
                return
                    Amount *
                    DensityTonnesPerUnit;
            }
        }

        public bool FlowEnabled { get; set; }
    }
}
