namespace KMC.Engine.Electrical
{
    public sealed class PowerSource
    {
        public PowerSource()
        {
            SourceType =
                PowerSourceType.Unknown;

            SourceName =
                string.Empty;
        }

        public uint PartId { get; set; }

        public PowerSourceType SourceType { get; set; }

        public string SourceName { get; set; }

        /*
         * Rate fields deliberately remain unknown in Build 8.0.
         * Build 8.4 will populate installed/available generation.
         */
        public bool HasKnownGenerationRate { get; set; }

        public double GenerationRateEcPerSecond { get; set; }
    }

    public sealed class PowerStorage
    {
        public PowerStorage()
        {
            ResourceName =
                "ElectricCharge";
        }

        public uint PartId { get; set; }

        public string ResourceName { get; set; }

        public double AmountEc { get; set; }

        public double CapacityEc { get; set; }
    }

    public sealed class PowerConsumer
    {
        public PowerConsumer()
        {
            ConsumerType =
                PowerConsumerType.Unknown;

            ConsumerName =
                string.Empty;
        }

        public uint PartId { get; set; }

        public PowerConsumerType ConsumerType { get; set; }

        public string ConsumerName { get; set; }

        /*
         * Consumption is intentionally not estimated in Build 8.0.
         * Build 8.5 will populate known/estimated loads.
         */
        public bool HasKnownConsumptionRate { get; set; }

        public double ConsumptionRateEcPerSecond { get; set; }
    }

    public sealed class PowerConnection
    {
        public uint FromPartId { get; set; }

        public uint ToPartId { get; set; }

        public ElectricalConnectionType ConnectionType { get; set; }
    }
}
