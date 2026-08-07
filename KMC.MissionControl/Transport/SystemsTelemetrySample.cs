using System;
using System.Collections.Generic;

namespace KMC.MissionControl.Transport
{
    public sealed class SystemsTelemetrySample
    {
        public SystemsTelemetrySample()
        {
            AttributionEntries =
                new List<SystemsAttributionEntry>();
        }

        public DateTime ReceivedUtc { get; set; }
        public double ElectricChargeAmount { get; set; }
        public double ElectricChargeCapacity { get; set; }
        public double MaximumThermalRatio { get; set; }
        public bool IsDocked { get; set; }

        public List<SystemsAttributionEntry> AttributionEntries
        {
            get;
            private set;
        }
    }

    public sealed class SystemsAttributionEntry
    {
        public bool IsProducer { get; set; }
        public uint PartId { get; set; }
        public string Category { get; set; }
        public string Evidence { get; set; }
        public bool CurrentKnown { get; set; }
        public double CurrentRateEcPerSecond { get; set; }
        public bool MaximumKnown { get; set; }
        public double MaximumRateEcPerSecond { get; set; }
        public bool Enabled { get; set; }
        public bool ActiveKnown { get; set; }
        public bool Active { get; set; }
        public string PartTitle { get; set; }
    }
}
