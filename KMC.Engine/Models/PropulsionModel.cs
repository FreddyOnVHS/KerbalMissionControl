namespace KMC.Engine.Models
{
    public sealed class PropulsionModel
    {
        public bool IsAvailable { get; internal set; }
        public int EngineCount { get; internal set; }
        public int OperableEngineCount { get; internal set; }
        public double AvailableThrust { get; internal set; }
    }
}
