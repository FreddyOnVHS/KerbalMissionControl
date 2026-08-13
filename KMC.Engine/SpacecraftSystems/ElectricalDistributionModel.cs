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

    /// <summary>
    /// Build 14.11.3 explicit electrical switching device.
    ///
    /// Command, actual position, indication and conduction are separate truth
    /// so later hardware failures do not have to overload source/load state.
    /// </summary>
    public enum SyntheticElectricalSwitchKind
    {
        SourceContactor = 0,
        SourceTransfer = 1,
        BusFeedContactor = 2,
        LoadBreaker = 3
    }

    public enum SyntheticElectricalSwitchFailureMode
    {
        None = 0,
        FailedOpen = 1,
        WeldedClosed = 2,
        TrippedOpen = 3,
        FalseClosedIndication = 4,
        FalseOpenIndication = 5
    }

    /// <summary>
    /// Failure target IDs used by the existing Engine failure scheduler.
    /// Mechanical switch faults use Component failure records; indication-only
    /// faults use Instrumentation records. The electrical distribution parses
    /// these IDs and owns the resulting hardware behavior.
    /// </summary>
    public static class SyntheticElectricalSwitchFailureTargets
    {
        private const string Prefix =
            "ELEC_SWITCH:";

        public static string Create(
            string switchId,
            SyntheticElectricalSwitchFailureMode mode)
        {
            if (string.IsNullOrWhiteSpace(switchId) ||
                mode == SyntheticElectricalSwitchFailureMode.None)
            {
                return string.Empty;
            }

            return
                Prefix +
                mode.ToString().ToUpperInvariant() +
                ":" +
                switchId;
        }

        public static bool TryParse(
            string targetId,
            out string switchId,
            out SyntheticElectricalSwitchFailureMode mode)
        {
            switchId = string.Empty;
            mode = SyntheticElectricalSwitchFailureMode.None;

            if (string.IsNullOrWhiteSpace(targetId) ||
                !targetId.StartsWith(
                    Prefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string remainder =
                targetId.Substring(
                    Prefix.Length);

            int separator =
                remainder.IndexOf(':');

            if (separator <= 0 ||
                separator >= remainder.Length - 1)
            {
                return false;
            }

            string modeText =
                remainder.Substring(
                    0,
                    separator);

            string id =
                remainder.Substring(
                    separator + 1);

            SyntheticElectricalSwitchFailureMode parsed;

            if (!Enum.TryParse(
                    modeText,
                    true,
                    out parsed) ||
                parsed ==
                    SyntheticElectricalSwitchFailureMode.None ||
                string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            switchId = id;
            mode = parsed;
            return true;
        }
    }

    public sealed class SyntheticElectricalSwitch
    {
        public SyntheticElectricalSwitch()
        {
            Id = string.Empty;
            DisplayName = string.Empty;
            UpstreamId = string.Empty;
            DownstreamId = string.Empty;
            Kind =
                SyntheticElectricalSwitchKind.SourceContactor;
            CommandedClosed = true;
            ActualClosed = true;
            IndicatedClosed = true;
            Conducting = false;
            Automatic = false;
            FailureMode =
                SyntheticElectricalSwitchFailureMode.None;
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

        /// <summary>
        /// Engine-only hidden hardware failure truth. Operator displays consume
        /// commanded/indicated/conduction evidence rather than this cause.
        /// </summary>
        internal SyntheticElectricalSwitchFailureMode FailureMode
        {
            get;
            set;
        }

        internal SyntheticElectricalSwitch Clone()
        {
            return
                new SyntheticElectricalSwitch
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
                    Automatic = Automatic,
                    FailureMode = FailureMode
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

        /// <summary>
        /// Hardware/source capability before distribution switching is applied.
        /// </summary>
        public double RatedAvailableCurrentAmps
        {
            get
            {
                if (!CommandedAvailable ||
                    State ==
                        SyntheticElectricalSourceState.Offline)
                {
                    return 0.0;
                }

                double factor =
                    State ==
                        SyntheticElectricalSourceState.Degraded
                        ? 0.50
                        : 1.0;

                return
                    Math.Max(
                        0.0,
                        CapacityAmps * factor);
            }
        }

        /// <summary>
        /// Current capability actually connected to the destination bus.
        /// </summary>
        public double AvailableCurrentAmps
        {
            get
            {
                return
                    Conducting
                        ? RatedAvailableCurrentAmps
                        : 0.0;
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
                    ActiveSourceId =
                        ActiveSourceId ?? string.Empty,
                    TransferSwitchId =
                        TransferSwitchId ?? string.Empty,
                    NominalVoltage = NominalVoltage,
                    Voltage = Voltage,
                    AvailableCurrentAmps =
                        AvailableCurrentAmps,
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
        private readonly List<SyntheticElectricalSwitch> _switches;

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

            _switches =
                new List<SyntheticElectricalSwitch>();
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

        public IList<SyntheticElectricalSwitch> Switches
        {
            get { return _switches; }
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

        public SyntheticElectricalSwitch FindSwitch(
            string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            for (int index = 0;
                 index < _switches.Count;
                 index++)
            {
                SyntheticElectricalSwitch item =
                    _switches[index];

                if (item != null &&
                    string.Equals(
                        item.Id,
                        id,
                        StringComparison.Ordinal))
                {
                    return item;
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

            for (int index = 0;
                 index < _switches.Count;
                 index++)
            {
                SyntheticElectricalSwitch item =
                    _switches[index];

                if (item != null)
                {
                    clone.Switches.Add(
                        item.Clone());
                }
            }

            return clone;
        }
    }
}
