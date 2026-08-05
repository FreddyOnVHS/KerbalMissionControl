using System;

namespace KMC.MissionControl.Telemetry
{
    public sealed class SolidFuelTelemetrySnapshot
    {
        public DateTime TimestampUtc { get; set; }

        public double TotalAmount { get; set; }

        public double TotalCapacity { get; set; }

        public double ActiveAmount { get; set; }

        public double ActiveCapacity { get; set; }

        public int BoosterCount { get; set; }

        public int BurningBoosterCount { get; set; }
    }
}
