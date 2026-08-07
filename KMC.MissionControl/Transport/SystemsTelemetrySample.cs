using System;

namespace KMC.MissionControl.Transport
{
    public sealed class SystemsTelemetrySample
    {
        public DateTime ReceivedUtc { get; set; }
        public double ElectricChargeAmount { get; set; }
        public double ElectricChargeCapacity { get; set; }
        public double MaximumThermalRatio { get; set; }
        public bool IsDocked { get; set; }
    }
}
