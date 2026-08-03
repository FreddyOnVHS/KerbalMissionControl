namespace KMC.Shared.Topology
{
    /// <summary>
    /// KSP-independent amount/capacity snapshot for one resource on one part.
    /// </summary>
    public sealed class VesselResourceState
    {
        public VesselResourceState()
        {
            Name = string.Empty;
        }

        public int ResourceId { get; set; }

        public string Name { get; set; }

        public double Amount { get; set; }

        public double Capacity { get; set; }

        public double DensityTonnesPerUnit { get; set; }

        public bool FlowEnabled { get; set; }

        public double FillFraction
        {
            get
            {
                if (Capacity <= 0.0)
                {
                    return 0.0;
                }

                double value = Amount / Capacity;

                if (value < 0.0)
                {
                    return 0.0;
                }

                return value > 1.0
                    ? 1.0
                    : value;
            }
        }
    }
}
