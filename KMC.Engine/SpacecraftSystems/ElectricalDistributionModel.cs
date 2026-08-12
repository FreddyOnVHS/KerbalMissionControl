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

    public sealed class SyntheticElectricalSource
    {
        public SyntheticElectricalSource()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            BusId = string.Empty;
            ParentBusId = string.Empty;
            Kind = SyntheticElectricalSourceKind.Generator;
            CommandedAvailable = true;
            State = SyntheticElectricalSourceState.Online;
            NominalVoltage = 28.0;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public string BusId { get; set; }
        public string ParentBusId { get; set; }
        public SyntheticElectricalSourceKind Kind { get; set; }
        public bool CommandedAvailable { get; set; }
        public SyntheticElectricalSourceState State { get; set; }
        public double NominalVoltage { get; set; }
        public double CapacityAmps { get; set; }

        public double AvailableCurrentAmps
        {
            get
            {
                if (!CommandedAvailable ||
                    State == SyntheticElectricalSourceState.Offline)
                {
                    return 0.0;
                }

                double factor =
                    State == SyntheticElectricalSourceState.Degraded
                        ? 0.50
                        : 1.0;

                return
                    Math.Max(
                        0.0,
                        CapacityAmps * factor);
            }
        }

        internal SyntheticElectricalSource Clone()
        {
            return
                new SyntheticElectricalSource
                {
                    Id = Id ?? string.Empty,
                    DisplayName = DisplayName ?? string.Empty,
                    BusId = BusId ?? string.Empty,
                    ParentBusId = ParentBusId ?? string.Empty,
                    Kind = Kind,
                    CommandedAvailable = CommandedAvailable,
                    State = State,
                    NominalVoltage = NominalVoltage,
                    CapacityAmps = CapacityAmps
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
            DemandAmps = 0.0;
            Priority = 2;
            CommandedOn = true;
        }

        public string EquipmentId { get; set; }
        public string DisplayName { get; set; }
        public string BusId { get; set; }
        public double DemandAmps { get; set; }
        public bool CommandedOn { get; set; }

        /// <summary>
        /// 1 = essential/protected, 2 = normal, 3 = shed-first.
        /// Build 14.2 will use this when crew load-shed controls arrive.
        /// </summary>
        public int Priority { get; set; }

        internal SyntheticElectricalLoad Clone()
        {
            return
                new SyntheticElectricalLoad
                {
                    EquipmentId = EquipmentId ?? string.Empty,
                    DisplayName = DisplayName ?? string.Empty,
                    BusId = BusId ?? string.Empty,
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
            NominalVoltage = 28.0;
            Voltage = 0.0;
            State = SyntheticElectricalBusState.Unpowered;
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
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
                {
                    return
                        DemandAmps > 0.000001
                            ? double.PositiveInfinity
                            : 0.0;
                }

                return
                    DemandAmps /
                    AvailableCurrentAmps;
            }
        }

        public double LoadPercent
        {
            get
            {
                double value =
                    LoadFraction;

                if (double.IsInfinity(value))
                {
                    return 999.0;
                }

                return
                    Math.Max(
                        0.0,
                        value * 100.0);
            }
        }

        internal SyntheticElectricalBus Clone()
        {
            return
                new SyntheticElectricalBus
                {
                    Id = Id ?? string.Empty,
                    DisplayName = DisplayName ?? string.Empty,
                    NominalVoltage = NominalVoltage,
                    Voltage = Voltage,
                    AvailableCurrentAmps = AvailableCurrentAmps,
                    DemandAmps = DemandAmps,
                    ActiveSourceCount = ActiveSourceCount,
                    State = State
                };
        }
    }

    /// <summary>
    /// Build 14.1 synthetic electrical-distribution result.
    ///
    /// Values are KMC spacecraft-design simulation values, not direct claims
    /// about stock KSP electrical wiring. The existing POWER model continues
    /// to own observed ElectricCharge storage/generation truth.
    /// </summary>
    public sealed class SyntheticElectricalDistributionModel
    {
        private readonly List<SyntheticElectricalSource> _sources;
        private readonly List<SyntheticElectricalBus> _buses;
        private readonly List<SyntheticElectricalLoad> _loads;

        public SyntheticElectricalDistributionModel()
        {
            TemplateId = string.Empty;
            GeneratedUtc = DateTime.MinValue;

            _sources =
                new List<SyntheticElectricalSource>();

            _buses =
                new List<SyntheticElectricalBus>();

            _loads =
                new List<SyntheticElectricalLoad>();
        }

        public string TemplateId { get; internal set; }
        public DateTime GeneratedUtc { get; internal set; }

        public IList<SyntheticElectricalSource> Sources
        {
            get { return _sources; }
        }

        public IList<SyntheticElectricalBus> Buses
        {
            get { return _buses; }
        }

        public IList<SyntheticElectricalLoad> Loads
        {
            get { return _loads; }
        }

        public SyntheticElectricalSource FindSource(
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int index = 0;
                 index < _sources.Count;
                 index++)
            {
                SyntheticElectricalSource source =
                    _sources[index];

                if (source != null &&
                    string.Equals(
                        source.Id,
                        id,
                        StringComparison.Ordinal))
                {
                    return source;
                }
            }

            return null;
        }

        public SyntheticElectricalBus FindBus(
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int index = 0;
                 index < _buses.Count;
                 index++)
            {
                SyntheticElectricalBus bus =
                    _buses[index];

                if (bus != null &&
                    string.Equals(
                        bus.Id,
                        id,
                        StringComparison.Ordinal))
                {
                    return bus;
                }
            }

            return null;
        }

        public SyntheticElectricalDistributionModel Clone()
        {
            SyntheticElectricalDistributionModel clone =
                new SyntheticElectricalDistributionModel
                {
                    TemplateId =
                        TemplateId ?? string.Empty,

                    GeneratedUtc =
                        GeneratedUtc
                };

            for (int index = 0;
                 index < _sources.Count;
                 index++)
            {
                SyntheticElectricalSource source =
                    _sources[index];

                if (source != null)
                {
                    clone.Sources.Add(
                        source.Clone());
                }
            }

            for (int index = 0;
                 index < _buses.Count;
                 index++)
            {
                SyntheticElectricalBus bus =
                    _buses[index];

                if (bus != null)
                {
                    clone.Buses.Add(
                        bus.Clone());
                }
            }

            for (int index = 0;
                 index < _loads.Count;
                 index++)
            {
                SyntheticElectricalLoad load =
                    _loads[index];

                if (load != null)
                {
                    clone.Loads.Add(
                        load.Clone());
                }
            }

            return clone;
        }
    }
}
