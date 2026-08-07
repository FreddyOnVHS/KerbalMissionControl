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

            ModuleName =
                string.Empty;

            Evidence =
                ElectricalEvidenceType.Unknown;
        }

        public uint PartId { get; set; }

        public PowerSourceType SourceType { get; set; }

        public string SourceName { get; set; }

        public string ModuleName { get; set; }

        public ElectricalEvidenceType Evidence { get; set; }

        public bool HasKnownGenerationRate { get; set; }

        public double GenerationRateEcPerSecond { get; set; }
    }

    public sealed class PowerStorage
    {
        public PowerStorage()
        {
            ResourceName =
                "ElectricCharge";

            Evidence =
                ElectricalEvidenceType.StoredResource;
        }

        public uint PartId { get; set; }

        public string ResourceName { get; set; }

        public ElectricalEvidenceType Evidence { get; set; }

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

            ModuleName =
                string.Empty;

            Evidence =
                ElectricalEvidenceType.Unknown;
        }

        public uint PartId { get; set; }

        public PowerConsumerType ConsumerType { get; set; }

        public string ConsumerName { get; set; }

        public string ModuleName { get; set; }

        public ElectricalEvidenceType Evidence { get; set; }

        /// <summary>
        /// True when the part role makes electrical use plausible, but the
        /// current topology packet does not expose an EC input that proves a
        /// present electrical load.
        /// </summary>
        public bool IsPotentialOnly { get; set; }

        public bool HasKnownConsumptionRate { get; set; }

        public double ConsumptionRateEcPerSecond { get; set; }
    }

    /// <summary>
    /// Membership in the currently connected vessel-wide ElectricCharge
    /// resource system. This is logical KSP resource membership, not a
    /// physical wire/bus diagram.
    /// </summary>
    public sealed class ElectricalBusMembership
    {
        public ElectricalBusMembership()
        {
            BusId =
                string.Empty;
        }

        public string BusId { get; set; }

        public uint PartId { get; set; }
    }

    /// <summary>
    /// Physical parent/child vessel topology retained independently from
    /// electrical bus membership for later staging/section analysis.
    /// </summary>
    public sealed class StructuralConnection
    {
        public uint ParentPartId { get; set; }

        public uint ChildPartId { get; set; }

        public int ChildActivationStage { get; set; }

        public int ChildSeparationStage { get; set; }

        public bool ParentIsElectricalNode { get; set; }

        public bool ChildIsElectricalNode { get; set; }
    }

    /// <summary>
    /// Legacy compatibility descriptor retained for early consumers. Build
    /// 8.1 no longer uses pairwise PowerConnection objects to represent stock
    /// KSP ElectricCharge connectivity.
    /// </summary>
    public sealed class PowerConnection
    {
        public uint FromPartId { get; set; }

        public uint ToPartId { get; set; }

        public ElectricalConnectionType ConnectionType { get; set; }
    }
}
