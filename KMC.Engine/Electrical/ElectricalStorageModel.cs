using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    /// <summary>
    /// One ElectricCharge storage-bearing part as represented by the most
    /// recent vessel topology snapshot.
    /// </summary>
    public sealed class ElectricalStoragePart
    {
        public ElectricalStoragePart()
        {
            PartName =
                string.Empty;

            PartTitle =
                string.Empty;
        }

        public uint PartId { get; set; }

        public uint BranchRootPartId { get; set; }

        public string PartName { get; set; }

        public string PartTitle { get; set; }

        public int StructuralDepth { get; set; }

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public bool WillSeparateOnNextStage { get; set; }

        public double StoredEc { get; set; }

        public double CapacityEc { get; set; }

        public double ChargeFraction
        {
            get
            {
                if (CapacityEc <= 0.0)
                {
                    return
                        0.0;
                }

                return
                    Clamp01(
                        StoredEc /
                        CapacityEc);
            }
        }

        public double ChargePercent
        {
            get
            {
                return
                    ChargeFraction *
                    100.0;
            }
        }

        private static double Clamp01(
            double value)
        {
            if (value < 0.0)
            {
                return
                    0.0;
            }

            if (value > 1.0)
            {
                return
                    1.0;
            }

            return
                value;
        }
    }

    /// <summary>
    /// ElectricCharge storage grouped by the part's topology separation stage.
    /// SeparationStage -1 means no known staged separation.
    /// </summary>
    public sealed class ElectricalStorageStageSection
    {
        public ElectricalStorageStageSection()
        {
            Parts =
                new List<ElectricalStoragePart>();
        }

        public int SeparationStage { get; set; }

        public bool IsRetainedSection
        {
            get
            {
                return
                    SeparationStage < 0;
            }
        }

        public bool WillSeparateOnNextStage { get; internal set; }

        public int StoragePartCount
        {
            get
            {
                return
                    Parts.Count;
            }
        }

        public double StoredEc { get; internal set; }

        public double CapacityEc { get; internal set; }

        public List<ElectricalStoragePart> Parts { get; private set; }
    }

    /// <summary>
    /// ElectricCharge storage grouped by structural branch root.
    /// This is useful for later spacecraft-section and staging analysis.
    /// </summary>
    public sealed class ElectricalStorageBranchSection
    {
        public ElectricalStorageBranchSection()
        {
            Parts =
                new List<ElectricalStoragePart>();
        }

        public uint BranchRootPartId { get; set; }

        public int StoragePartCount
        {
            get
            {
                return
                    Parts.Count;
            }
        }

        public double StoredEc { get; internal set; }

        public double CapacityEc { get; internal set; }

        public bool ContainsNextStageLoss { get; internal set; }

        public List<ElectricalStoragePart> Parts { get; private set; }
    }

    /// <summary>
    /// Snapshot-state electrical storage analysis.
    ///
    /// Amount/capacity values come from the most recent VesselTopology
    /// resource snapshot. This model does not claim high-frequency EC flow.
    /// </summary>
    public sealed class ElectricalStorageModel
    {
        public ElectricalStorageModel()
        {
            Parts =
                new List<ElectricalStoragePart>();

            StageSections =
                new List<ElectricalStorageStageSection>();

            BranchSections =
                new List<ElectricalStorageBranchSection>();

            Diagnostics =
                new List<string>();
        }

        public long TopologyRevision { get; set; }

        public int CurrentStage { get; set; }

        public int NextStage { get; set; }

        public double StoredEc { get; internal set; }

        public double CapacityEc { get; internal set; }

        public double ChargeFraction
        {
            get
            {
                if (CapacityEc <= 0.0)
                {
                    return
                        0.0;
                }

                return
                    Clamp01(
                        StoredEc /
                        CapacityEc);
            }
        }

        public double ChargePercent
        {
            get
            {
                return
                    ChargeFraction *
                    100.0;
            }
        }

        public double NextStageLostStoredEc { get; internal set; }

        public double NextStageLostCapacityEc { get; internal set; }

        public double NextStageRemainingStoredEc
        {
            get
            {
                return
                    MaximumZero(
                        StoredEc -
                        NextStageLostStoredEc);
            }
        }

        public double NextStageRemainingCapacityEc
        {
            get
            {
                return
                    MaximumZero(
                        CapacityEc -
                        NextStageLostCapacityEc);
            }
        }

        public double NextStageRemainingChargePercent
        {
            get
            {
                double capacity =
                    NextStageRemainingCapacityEc;

                if (capacity <= 0.0)
                {
                    return
                        0.0;
                }

                return
                    Clamp01(
                        NextStageRemainingStoredEc /
                        capacity) *
                    100.0;
            }
        }

        public bool HasStorageLossOnNextStage
        {
            get
            {
                return
                    NextStageLostCapacityEc >
                    0.000001;
            }
        }

        public bool LosesAllStorageOnNextStage
        {
            get
            {
                return
                    CapacityEc >
                        0.000001 &&
                    NextStageRemainingCapacityEc <=
                        0.000001;
            }
        }

        public List<ElectricalStoragePart> Parts { get; private set; }

        public List<ElectricalStorageStageSection> StageSections { get; private set; }

        public List<ElectricalStorageBranchSection> BranchSections { get; private set; }

        public List<string> Diagnostics { get; private set; }

        private static double Clamp01(
            double value)
        {
            if (value < 0.0)
            {
                return
                    0.0;
            }

            if (value > 1.0)
            {
                return
                    1.0;
            }

            return
                value;
        }

        private static double MaximumZero(
            double value)
        {
            return
                value < 0.0
                    ? 0.0
                    : value;
        }
    }
}
