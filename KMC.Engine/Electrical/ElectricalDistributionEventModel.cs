using System;
using System.Collections.Generic;
using KMC.Engine.SpacecraftSystems;

namespace KMC.Engine.Electrical
{
    public enum ElectricalDistributionEventKind
    {
        BusState = 0,
        SourceTransfer,
        AutomaticShed,
        ManualShed
    }

    public sealed class ElectricalDistributionEventRecord
    {
        public long Sequence { get; internal set; }
        public DateTime TimestampUtc { get; internal set; }

        public ElectricalEventSeverity Severity
        {
            get;
            internal set;
        }

        public ElectricalDistributionEventKind Kind
        {
            get;
            internal set;
        }

        public string BusId { get; internal set; }
        public string BusName { get; internal set; }
        public string Code { get; internal set; }
        public string Message { get; internal set; }
    }

    public sealed class ElectricalDistributionEventHistoryModel
    {
        public ElectricalDistributionEventHistoryModel()
        {
            Events =
                new List<ElectricalDistributionEventRecord>();
        }

        public List<ElectricalDistributionEventRecord> Events
        {
            get;
            private set;
        }

        public int Count
        {
            get { return Events.Count; }
        }

        public ElectricalDistributionEventRecord Latest
        {
            get
            {
                return
                    Events.Count > 0
                        ? Events[Events.Count - 1]
                        : null;
            }
        }
    }

    /// <summary>
    /// Build 14.14.3 Engine-owned transition history for the synthetic
    /// spacecraft electrical distribution.
    ///
    /// The older POWER event tracker observes aggregate KSP electrical
    /// diagnostic/procedure transitions. This tracker intentionally observes
    /// the later A/B/ESS switched-distribution truth:
    ///
    /// - bus state
    /// - active source
    /// - automatic shed demand
    /// - manual shed demand
    ///
    /// The first valid snapshot for a vessel establishes the baseline and
    /// produces no event. Events are transition-only and bounded.
    /// </summary>
    internal sealed class ElectricalDistributionEventTracker
    {
        private const int MaximumHistoryCount = 24;
        private const double ShedToleranceAmps = 0.01;

        private static readonly string[] BusIds =
            new[]
            {
                "BUS_MAIN_A",
                "BUS_MAIN_B",
                "BUS_ESS"
            };

        private readonly List<ElectricalDistributionEventRecord>
            _events =
                new List<ElectricalDistributionEventRecord>();

        private readonly Dictionary<string, BusSnapshot>
            _previous =
                new Dictionary<string, BusSnapshot>(
                    StringComparer.Ordinal);

        private string _vesselId =
            string.Empty;

        private bool _established;
        private long _nextSequence = 1;

        public ElectricalDistributionEventHistoryModel Analyze(
            DateTime receivedUtc,
            string vesselId,
            SyntheticElectricalDistributionModel distribution)
        {
            string currentVessel =
                vesselId ?? string.Empty;

            if (!string.Equals(
                    _vesselId,
                    currentVessel,
                    StringComparison.Ordinal))
            {
                Reset(
                    currentVessel);
            }

            if (distribution == null)
            {
                return
                    BuildModel();
            }

            DateTime utc =
                receivedUtc.Kind ==
                    DateTimeKind.Utc
                        ? receivedUtc
                        : receivedUtc.ToUniversalTime();

            if (!_established)
            {
                Establish(
                    distribution);

                return
                    BuildModel();
            }

            for (int index = 0;
                 index < BusIds.Length;
                 index++)
            {
                string busId =
                    BusIds[index];

                SyntheticElectricalBus bus =
                    distribution.FindBus(
                        busId);

                if (bus == null)
                {
                    continue;
                }

                BusSnapshot previous;

                if (!_previous.TryGetValue(
                        busId,
                        out previous))
                {
                    _previous[busId] =
                        BusSnapshot.FromBus(
                            bus);

                    continue;
                }

                TrackState(
                    utc,
                    bus,
                    previous);

                TrackSource(
                    utc,
                    bus,
                    previous);

                TrackAutomaticShed(
                    utc,
                    bus,
                    previous);

                TrackManualShed(
                    utc,
                    bus,
                    previous);

                _previous[busId] =
                    BusSnapshot.FromBus(
                        bus);
            }

            return
                BuildModel();
        }

        private void Establish(
            SyntheticElectricalDistributionModel distribution)
        {
            _previous.Clear();

            for (int index = 0;
                 index < BusIds.Length;
                 index++)
            {
                SyntheticElectricalBus bus =
                    distribution.FindBus(
                        BusIds[index]);

                if (bus != null)
                {
                    _previous[bus.Id] =
                        BusSnapshot.FromBus(
                            bus);
                }
            }

            _established =
                true;
        }

        private void TrackState(
            DateTime utc,
            SyntheticElectricalBus bus,
            BusSnapshot previous)
        {
            if (bus.State ==
                previous.State)
            {
                return;
            }

            AddEvent(
                utc,
                SeverityFromState(
                    bus.State),
                ElectricalDistributionEventKind.BusState,
                bus,
                DisplayName(bus) +
                " " +
                SplitWords(
                    bus.State.ToString()),
                SplitWords(
                    previous.State.ToString()) +
                " -> " +
                SplitWords(
                    bus.State.ToString()) +
                " / " +
                bus.Voltage.ToString("0.0") +
                " V");
        }

        private void TrackSource(
            DateTime utc,
            SyntheticElectricalBus bus,
            BusSnapshot previous)
        {
            string current =
                NormalizeSource(
                    bus.ActiveSourceId);

            string old =
                NormalizeSource(
                    previous.ActiveSourceId);

            if (string.Equals(
                    current,
                    old,
                    StringComparison.Ordinal))
            {
                return;
            }

            ElectricalEventSeverity severity =
                string.Equals(
                    current,
                    "NONE",
                    StringComparison.Ordinal)
                    ? ElectricalEventSeverity.Critical
                    : current.StartsWith(
                          "BAT_",
                          StringComparison.Ordinal)
                        ? ElectricalEventSeverity.Advisory
                        : ElectricalEventSeverity.Info;

            AddEvent(
                utc,
                severity,
                ElectricalDistributionEventKind.SourceTransfer,
                bus,
                DisplayName(bus) +
                " SOURCE " +
                current,
                old +
                " -> " +
                current);
        }

        private void TrackAutomaticShed(
            DateTime utc,
            SyntheticElectricalBus bus,
            BusSnapshot previous)
        {
            if (Math.Abs(
                    bus.ShedDemandAmps -
                    previous.AutomaticShedAmps) <=
                ShedToleranceAmps)
            {
                return;
            }

            bool active =
                bus.ShedDemandAmps >
                ShedToleranceAmps;

            AddEvent(
                utc,
                active
                    ? ElectricalEventSeverity.Advisory
                    : ElectricalEventSeverity.Info,
                ElectricalDistributionEventKind.AutomaticShed,
                bus,
                DisplayName(bus) +
                (active
                    ? " AUTO SHED " +
                      bus.ShedDemandAmps.ToString("0.0") +
                      " A"
                    : " AUTO SHED CLEARED"),
                previous.AutomaticShedAmps.ToString("0.0") +
                " -> " +
                bus.ShedDemandAmps.ToString("0.0") +
                " A");
        }

        private void TrackManualShed(
            DateTime utc,
            SyntheticElectricalBus bus,
            BusSnapshot previous)
        {
            if (Math.Abs(
                    bus.ManualShedDemandAmps -
                    previous.ManualShedAmps) <=
                ShedToleranceAmps)
            {
                return;
            }

            bool active =
                bus.ManualShedDemandAmps >
                ShedToleranceAmps;

            AddEvent(
                utc,
                active
                    ? ElectricalEventSeverity.Advisory
                    : ElectricalEventSeverity.Info,
                ElectricalDistributionEventKind.ManualShed,
                bus,
                DisplayName(bus) +
                (active
                    ? " MAN SHED " +
                      bus.ManualShedDemandAmps.ToString("0.0") +
                      " A"
                    : " MAN SHED CLEARED"),
                previous.ManualShedAmps.ToString("0.0") +
                " -> " +
                bus.ManualShedDemandAmps.ToString("0.0") +
                " A");
        }

        private void AddEvent(
            DateTime utc,
            ElectricalEventSeverity severity,
            ElectricalDistributionEventKind kind,
            SyntheticElectricalBus bus,
            string code,
            string message)
        {
            _events.Add(
                new ElectricalDistributionEventRecord
                {
                    Sequence =
                        _nextSequence++,

                    TimestampUtc =
                        utc,

                    Severity =
                        severity,

                    Kind =
                        kind,

                    BusId =
                        bus != null
                            ? bus.Id ?? string.Empty
                            : string.Empty,

                    BusName =
                        DisplayName(
                            bus),

                    Code =
                        code ?? string.Empty,

                    Message =
                        message ?? string.Empty
                });

            while (_events.Count >
                   MaximumHistoryCount)
            {
                _events.RemoveAt(
                    0);
            }
        }

        private ElectricalDistributionEventHistoryModel BuildModel()
        {
            ElectricalDistributionEventHistoryModel model =
                new ElectricalDistributionEventHistoryModel();

            for (int index = 0;
                 index < _events.Count;
                 index++)
            {
                ElectricalDistributionEventRecord source =
                    _events[index];

                model.Events.Add(
                    new ElectricalDistributionEventRecord
                    {
                        Sequence =
                            source.Sequence,

                        TimestampUtc =
                            source.TimestampUtc,

                        Severity =
                            source.Severity,

                        Kind =
                            source.Kind,

                        BusId =
                            source.BusId ?? string.Empty,

                        BusName =
                            source.BusName ?? string.Empty,

                        Code =
                            source.Code ?? string.Empty,

                        Message =
                            source.Message ?? string.Empty
                    });
            }

            return model;
        }

        private void Reset(
            string vesselId)
        {
            _events.Clear();
            _previous.Clear();

            _vesselId =
                vesselId ?? string.Empty;

            _established =
                false;

            _nextSequence =
                1;
        }

        private static string NormalizeSource(
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceId))
            {
                return
                    "NONE";
            }

            return
                sourceId
                    .Replace(
                        "SRC_",
                        string.Empty)
                    .Trim()
                    .ToUpperInvariant();
        }

        private static string DisplayName(
            SyntheticElectricalBus bus)
        {
            if (bus == null)
            {
                return
                    "BUS";
            }

            if (!string.IsNullOrWhiteSpace(
                    bus.DisplayName))
            {
                return
                    bus.DisplayName
                        .Trim()
                        .ToUpperInvariant();
            }

            return
                (bus.Id ?? "BUS")
                    .Replace(
                        "BUS_",
                        string.Empty)
                    .Replace(
                        '_',
                        ' ')
                    .Trim()
                    .ToUpperInvariant();
        }

        private static ElectricalEventSeverity SeverityFromState(
            SyntheticElectricalBusState state)
        {
            switch (state)
            {
                case SyntheticElectricalBusState.Nominal:
                    return
                        ElectricalEventSeverity.Info;

                case SyntheticElectricalBusState.HighLoad:
                    return
                        ElectricalEventSeverity.Advisory;

                case SyntheticElectricalBusState.Overloaded:
                case SyntheticElectricalBusState.Undervoltage:
                    return
                        ElectricalEventSeverity.Warning;

                case SyntheticElectricalBusState.Failed:
                case SyntheticElectricalBusState.Unpowered:
                    return
                        ElectricalEventSeverity.Critical;

                default:
                    return
                        ElectricalEventSeverity.Advisory;
            }
        }

        private static string SplitWords(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                    value))
            {
                return
                    "---";
            }

            System.Text.StringBuilder builder =
                new System.Text.StringBuilder();

            for (int index = 0;
                 index < value.Length;
                 index++)
            {
                char current =
                    value[index];

                if (index > 0 &&
                    char.IsUpper(
                        current) &&
                    !char.IsUpper(
                        value[index - 1]))
                {
                    builder.Append(
                        ' ');
                }

                builder.Append(
                    current);
            }

            return
                builder.ToString()
                    .ToUpperInvariant();
        }

        private sealed class BusSnapshot
        {
            public SyntheticElectricalBusState State
            {
                get;
                set;
            }

            public string ActiveSourceId
            {
                get;
                set;
            }

            public double AutomaticShedAmps
            {
                get;
                set;
            }

            public double ManualShedAmps
            {
                get;
                set;
            }

            public static BusSnapshot FromBus(
                SyntheticElectricalBus bus)
            {
                return
                    new BusSnapshot
                    {
                        State =
                            bus != null
                                ? bus.State
                                : SyntheticElectricalBusState.Unknown,

                        ActiveSourceId =
                            bus != null
                                ? bus.ActiveSourceId ?? string.Empty
                                : string.Empty,

                        AutomaticShedAmps =
                            bus != null
                                ? bus.ShedDemandAmps
                                : 0.0,

                        ManualShedAmps =
                            bus != null
                                ? bus.ManualShedDemandAmps
                                : 0.0
                    };
            }
        }
    }
}
