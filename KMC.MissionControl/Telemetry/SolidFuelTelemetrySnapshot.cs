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

        public double LeftAmount { get; set; }

        public double LeftCapacity { get; set; }

        public bool LeftBurning { get; set; }

        public double RightAmount { get; set; }

        public double RightCapacity { get; set; }

        public bool RightBurning { get; set; }
    }
}
