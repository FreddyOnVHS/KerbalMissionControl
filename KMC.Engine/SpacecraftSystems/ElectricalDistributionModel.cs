using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    public enum SyntheticElectricalSourceKind
    {
        Generator = 0,
        Battery = 1,
        BusFeed = 2
    }

    public enum SyntheticElectricalSourceState
    {
        Offline = 0,
        Online = 1,
        Degraded = 2
    }

    public enum SyntheticElectricalBusState
    {
        Unpowered = 0,
        Nominal = 1,
        HighLoad = 2,
        Overloaded = 3,
        Undervoltage = 4
    }

    public enum SyntheticElectricalSwitchKind
    {
        SourceContactor = 0,
        SourceTransfer = 1,
        BusFeedContactor = 2,
        LoadBreaker = 3
    }

    public sealed class SyntheticElectricalSwitch
    {
        public SyntheticElectricalSwitch()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            UpstreamId = string.Empty;
            DownstreamId = string.Empty;
            Kind = SyntheticElectricalSwitchKind.SourceContactor;
            CommandedClosed = true;
            ActualClosed = true;
            IndicatedClosed = true;
            Conducting = false;
            Automatic = false;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string UpstreamId { get; set; }
        public string DownstreamId { get; set; }
        public SyntheticElectricalSwitchKind Kind { get; set; }
        public bool CommandedClosed { get; set; }
        public bool ActualClosed { get; set; }
        public bool IndicatedClosed { get; set; }
        public bool Conducting { get; set; }
        public bool Automatic { get; set; }

        internal SyntheticElectricalSwitch Clone()
        {
            return new SyntheticElectricalSwitch
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                UpstreamId = UpstreamId ?? string.Empty,
                DownstreamId = DownstreamId ?? string.Empty,
                Kind = Kind,
                CommandedClosed = CommandedClosed,
                ActualClosed = ActualClosed,
                IndicatedClosed = IndicatedClosed,
                Conducting = Conducting,
                Automatic = Automatic
            };
        }
    }

    public sealed class SyntheticElectricalSource
    {
        public SyntheticElectricalSource()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            BusId = string.Empty;
            ParentBusId = string.Empty;
            ContactorId = string.Empty;
            Kind = SyntheticElectricalSourceKind.Generator;
            CommandedAvailable = true;
            State = SyntheticElectricalSourceState.Online;
            NominalVoltage = 28.0;
            SelectedForBus = false;
            Conducting = false;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string BusId { get; set; }
        public string ParentBusId { get; set; }
        public string ContactorId { get; set; }
        public SyntheticElectricalSourceKind Kind { get; set; }
        public bool CommandedAvailable { get; set; }
        public SyntheticElectricalSourceState State { get; set; }
        public double NominalVoltage { get; set; }
        public double CapacityAmps { get; set; }
        public bool SelectedForBus { get; internal set; }
        public bool Conducting { get; internal set; }

        public double RatedAvailableCurrentAmps
        {
            get
            {
                if (!CommandedAvailable || State == SyntheticElectricalSourceState.Offline)
                    return 0.0;

                double factor = State == SyntheticElectricalSourceState.Degraded ? 0.50 : 1.0;
                return Math.Max(0.0, CapacityAmps * factor);
            }
        }

        public double AvailableCurrentAmps
        {
            get { return Conducting ? RatedAvailableCurrentAmps : 0.0; }
        }

        internal SyntheticElectricalSource Clone()
        {
            return new SyntheticElectricalSource
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                BusId = BusId ?? string.Empty,
                ParentBusId = ParentBusId ?? string.Empty,
                ContactorId = ContactorId ?? string.Empty,
                Kind = Kind,
                CommandedAvailable = CommandedAvailable,
                State = State,
                NominalVoltage = NominalVoltage,
                CapacityAmps = CapacityAmps,
                SelectedForBus = SelectedForBus,
                Conducting = Conducting
            };
        }
    }

    public sealed class SyntheticElectricalLoad
    {
        public SyntheticElectricalLoad()
        {
            EquipmentId = string.Empty;
            DisplayName = string.Empty;
            BusId = string.Empty;
            BreakerId = string.Empty;
            DemandAmps = 0.0;
            Priority = 2;
            CommandedOn = true;
        }

        public string EquipmentId { get; set; }
        public string DisplayName { get; set; }
        public string BusId { get; set; }
        public string BreakerId { get; set; }
        public double DemandAmps { get; set; }
        public bool CommandedOn { get; set; }
        public int Priority { get; set; }

        internal SyntheticElectricalLoad Clone()
        {
            return new SyntheticElectricalLoad
            {
                EquipmentId = EquipmentId ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                BusId = BusId ?? string.Empty,
                BreakerId = BreakerId ?? string.Empty,
                DemandAmps = DemandAmps,
                Priority = Priority,
                CommandedOn = CommandedOn
            };
        }
    }

    public sealed class SyntheticElectricalBus
    {
        public SyntheticElectricalBus()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            ActiveSourceId = string.Empty;
            TransferSwitchId = string.Empty;
            NominalVoltage = 28.0;
            Voltage = 0.0;
            State = SyntheticElectricalBusState.Unpowered;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string ActiveSourceId { get; internal set; }
        public string TransferSwitchId { get; set; }
        public double NominalVoltage { get; set; }
        public double Voltage { get; internal set; }
        public double AvailableCurrentAmps { get; internal set; }
        public double DemandAmps { get; internal set; }
        public int ActiveSourceCount { get; internal set; }
        public SyntheticElectricalBusState State { get; internal set; }

        public double LoadFraction
        {
            get
            {
                if (AvailableCurrentAmps <= 0.000001)
                    return DemandAmps > 0.000001 ? double.PositiveInfinity : 0.0;
                return DemandAmps / AvailableCurrentAmps;
            }
        }

        public double LoadPercent
        {
            get
            {
                double value = LoadFraction;
                if (double.IsInfinity(value)) return 999.0;
                return Math.Max(0.0, value * 100.0);
            }
        }

        internal SyntheticElectricalBus Clone()
        {
            return new SyntheticElectricalBus
            {
                Id = Id ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                ActiveSourceId = ActiveSourceId ?? string.Empty,
                TransferSwitchId = TransferSwitchId ?? string.Empty,
                NominalVoltage = NominalVoltage,
                Voltage = Voltage,
                AvailableCurrentAmps = AvailableCurrentAmps,
                DemandAmps = DemandAmps,
                ActiveSourceCount = ActiveSourceCount,
                State = State
            };
        }
    }

    public sealed class SyntheticElectricalDistributionModel
    {
        private readonly List<SyntheticElectricalSource> _sources = new List<SyntheticElectricalSource>();
        private readonly List<SyntheticElectricalBus> _buses = new List<SyntheticElectricalBus>();
        private readonly List<SyntheticElectricalLoad> _loads = new List<SyntheticElectricalLoad>();
        private readonly List<SyntheticElectricalSwitch> _switches = new List<SyntheticElectricalSwitch>();

        public SyntheticElectricalDistributionModel()
        {
            TemplateId = string.Empty;
            GeneratedUtc = DateTime.MinValue;
        }

        public string TemplateId { get; internal set; }
        public DateTime GeneratedUtc { get; internal set; }
        public IList<SyntheticElectricalSource> Sources { get { return _sources; } }
        public IList<SyntheticElectricalBus> Buses { get { return _buses; } }
        public IList<SyntheticElectricalLoad> Loads { get { return _loads; } }
        public IList<SyntheticElectricalSwitch> Switches { get { return _switches; } }

        public SyntheticElectricalSource FindSource(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i=0;i<_sources.Count;i++)
                if (_sources[i] != null && string.Equals(_sources[i].Id,id,StringComparison.Ordinal)) return _sources[i];
            return null;
        }

        public SyntheticElectricalBus FindBus(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i=0;i<_buses.Count;i++)
                if (_buses[i] != null && string.Equals(_buses[i].Id,id,StringComparison.Ordinal)) return _buses[i];
            return null;
        }

        public SyntheticElectricalSwitch FindSwitch(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i=0;i<_switches.Count;i++)
                if (_switches[i] != null && string.Equals(_switches[i].Id,id,StringComparison.Ordinal)) return _switches[i];
            return null;
        }

        public SyntheticElectricalDistributionModel Clone()
        {
            SyntheticElectricalDistributionModel clone = new SyntheticElectricalDistributionModel
            {
                TemplateId = TemplateId ?? string.Empty,
                GeneratedUtc = GeneratedUtc
            };
            for (int i=0;i<_sources.Count;i++) if (_sources[i]!=null) clone.Sources.Add(_sources[i].Clone());
            for (int i=0;i<_buses.Count;i++) if (_buses[i]!=null) clone.Buses.Add(_buses[i].Clone());
            for (int i=0;i<_loads.Count;i++) if (_loads[i]!=null) clone.Loads.Add(_loads[i].Clone());
            for (int i=0;i<_switches.Count;i++) if (_switches[i]!=null) clone.Switches.Add(_switches[i].Clone());
            return clone;
        }
    }
}
