namespace KMC.Engine.Models
{
    public sealed class PowerModel
    {
        public bool IsAvailable { get; internal set; }
        public double StoredEnergy { get; internal set; }
        public double StorageCapacity { get; internal set; }
        public double GenerationRate { get; internal set; }
        public double ConsumptionRate { get; internal set; }
    }
}
