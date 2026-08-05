namespace KMC.MissionControl.Telemetry
{
    public sealed class EngineStateTelemetry
    {
        public uint PartId { get; set; }
        public EngineOperatingState OperatingState { get; set; }
        public bool IsSolidBooster { get; set; }
        public double CurrentThrust { get; set; }
        public double MaximumThrust { get; set; }
    }
}
