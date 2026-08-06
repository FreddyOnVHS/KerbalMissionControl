using System.Collections.Generic;

namespace KMC.MissionControl.Debugging.Electrical
{
    public sealed class ElectricalTopologyModel
    {
        public ElectricalTopologyModel()
        {
            VesselName =
                string.Empty;

            Sections =
                new List<
                    ElectricalSectionModel>();

            Parts =
                new List<
                    ElectricalPartModel>();

            Diagnostics =
                new List<string>();
        }

        public string VesselName { get; set; }

        public long TopologyRevision { get; set; }

        public int CurrentStage { get; set; }

        public List<
            ElectricalSectionModel> Sections
        {
            get;
            private set;
        }

        public List<
            ElectricalPartModel> Parts
        {
            get;
            private set;
        }

        public List<string> Diagnostics
        {
            get;
            private set;
        }
    }

    public sealed class ElectricalSectionModel
    {
        public ElectricalSectionModel()
        {
            Key =
                string.Empty;

            Name =
                string.Empty;

            SourcePartIds =
                new List<uint>();
        }

        public string Key { get; set; }

        public string Name { get; set; }

        public int DisplayOrder { get; set; }

        public int SeparationStage { get; set; }

        public int ActivationStage { get; set; }

        public bool IsCommandSection { get; set; }

        public bool IsRadialSection { get; set; }

        public uint BranchRootPartId { get; set; }

        public uint SymmetryGroupId { get; set; }

        public double AverageY { get; set; }

        public double MinimumY { get; set; }

        public double MaximumY { get; set; }

        public int PartCount { get; set; }

        public int ElectricalPartCount { get; set; }

        public int CommandPartCount { get; set; }

        public int BatteryPartCount { get; set; }

        public int SolarPartCount { get; set; }

        public int GeneratorPartCount { get; set; }

        public int FuelCellPartCount { get; set; }

        public int DockingPortCount { get; set; }

        public double ElectricChargeAmount { get; set; }

        public double ElectricChargeCapacity { get; set; }

        public List<uint> SourcePartIds
        {
            get;
            private set;
        }

        public double ElectricChargePercent
        {
            get
            {
                if (ElectricChargeCapacity <=
                    0.0001)
                {
                    return 0.0;
                }

                return
                    ElectricChargeAmount /
                    ElectricChargeCapacity *
                    100.0;
            }
        }

        public bool HasElectricalHardware
        {
            get
            {
                return
                    ElectricalPartCount > 0 ||
                    ElectricChargeCapacity > 0.0001;
            }
        }
    }

    public sealed class ElectricalPartModel
    {
        public ElectricalPartModel()
        {
            SectionKey =
                string.Empty;

            Title =
                string.Empty;

            Roles =
                string.Empty;

            ElectricalRole =
                string.Empty;
        }

        public uint PartId { get; set; }

        public uint ParentPartId { get; set; }

        public bool HasParent { get; set; }

        public string SectionKey { get; set; }

        public string Title { get; set; }

        public string Roles { get; set; }

        public string ElectricalRole { get; set; }

        public int ActivationStage { get; set; }

        public int SeparationStage { get; set; }

        public int StructuralDepth { get; set; }

        public uint BranchRootPartId { get; set; }

        public uint SymmetryGroupId { get; set; }

        public double VesselX { get; set; }

        public double VesselY { get; set; }

        public double VesselZ { get; set; }

        public double ElectricChargeAmount { get; set; }

        public double ElectricChargeCapacity { get; set; }

        public bool IsElectricalPart { get; set; }

        public bool IsCommand { get; set; }

        public bool IsBattery { get; set; }

        public bool IsSolar { get; set; }

        public bool IsGenerator { get; set; }

        public bool IsFuelCell { get; set; }

        public bool IsDockingPort { get; set; }
    }
}
