namespace KMC.Shared.Topology
{
    public sealed class VesselModuleResource
    {
        public VesselModuleResource()
        {
            Name = string.Empty;
        }

        public string Name { get; set; }

        public double Ratio { get; set; }
    }
}
