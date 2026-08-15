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

        /*
         * Build 14.14.4C:
         * Do not let the first transient distribution snapshot become the
         * operator baseline. KMC startup can briefly expose default 0 V /
         * source NONE values before the synthetic distribution settles.
         */
        private readonly Dictionary<string, BusSnapshot>
            _baselineCandidate =
                new Dictionary<string, BusSnapshot>(
                    StringComparer.Ordinal);

        private int _baselineCandidateMatches;

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
                if (!TryEstablishStableBaseline(
                        distribution))
                {
                    return
                        BuildModel();
                }

                /*
                 * The snapshot that completes baseline establishment is not
                 * itself an event. History begins only with later changes.
                 */
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

        private bool TryEstablishStableBaseline(
            SyntheticElectricalDistributionModel distribution)
        {
            Dictionary<string, BusSnapshot> current =
                CaptureCompleteSnapshot(
                    distribution);

            if (current == null)
            {
                _baselineCandidate.Clear();
                _baselineCandidateMatches = 0;

                return false;
            }

            if (!SnapshotsEquivalent(
                    _baselineCandidate,
                    current))
            {
                _baselineCandidate.Clear();

                foreach (KeyValuePair<string, BusSnapshot> pair in current)
                {
                    _baselineCandidate[pair.Key] =
                        pair.Value;
                }

                _baselineCandidateMatches = 1;

                return false;
            }

            _baselineCandidateMatches++;

            /*
             * Two consecutive equivalent complete observations are enough to
             * distinguish a settled initial condition from the one-frame
             * startup defaults seen during KMC initialization.
             */
            if (_baselineCandidateMatches < 2)
            {
                return false;
            }

            _previous.Clear();

            foreach (KeyValuePair<string, BusSnapshot> pair in current)
            {
                _previous[pair.Key] =
                    pair.Value;
            }

            _baselineCandidate.Clear();
            _baselineCandidateMatches = 0;
            _established = true;

            return true;
        }

        private static Dictionary<string, BusSnapshot> CaptureCompleteSnapshot(
            SyntheticElectricalDistributionModel distribution)
        {
            if (distribution == null)
            {
                return null;
            }

            Dictionary<string, BusSnapshot> snapshot =
                new Dictionary<string, BusSnapshot>(
                    StringComparer.Ordinal);

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
                    return null;
                }

                snapshot[busId] =
                    BusSnapshot.FromBus(
                        bus);
            }

            return snapshot;
        }

        private static bool SnapshotsEquivalent(
            Dictionary<string, BusSnapshot> left,
            Dictionary<string, BusSnapshot> right)
        {
            if (left == null ||
                right == null ||
                left.Count != BusIds.Length ||
                right.Count != BusIds.Length)
            {
                return false;
            }

            for (int index = 0;
                 index < BusIds.Length;
                 index++)
            {
                BusSnapshot a;
                BusSnapshot b;

                if (!left.TryGetValue(
                        BusIds[index],
                        out a) ||
                    !right.TryGetValue(
                        BusIds[index],
                        out b) ||
                    !BusSnapshotsEquivalent(
                        a,
                        b))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BusSnapshotsEquivalent(
            BusSnapshot left,
            BusSnapshot right)
        {
            if (left == null ||
                right == null)
            {
                return false;
            }

            return
                left.State == right.State &&
                string.Equals(
                    NormalizeSource(
                        left.ActiveSourceId),
                    NormalizeSource(
                        right.ActiveSourceId),
                    StringComparison.Ordinal) &&
                Math.Abs(
                    left.Voltage -
                    right.Voltage) <= 0.05 &&
                Math.Abs(
                    left.DemandAmps -
                    right.DemandAmps) <= 0.01 &&
                Math.Abs(
                    left.AutomaticShedAmps -
                    right.AutomaticShedAmps) <=
                    ShedToleranceAmps &&
                Math.Abs(
                    left.ManualShedAmps -
                    right.ManualShedAmps) <=
                    ShedToleranceAmps;
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
            _baselineCandidate.Clear();
            _baselineCandidateMatches = 0;

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

            public double Voltage
            {
                get;
                set;
            }

            public double DemandAmps
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
                                : SyntheticElectricalBusState.Unpowered,

                        ActiveSourceId =
                            bus != null
                                ? bus.ActiveSourceId ?? string.Empty
                                : string.Empty,

                        Voltage =
                            bus != null
                                ? bus.Voltage
                                : 0.0,

                        DemandAmps =
                            bus != null
                                ? bus.DemandAmps
                                : 0.0,

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
    /// <summary>
    /// Build 14.14.4 observable short-term electrical evidence.
    /// This contains only values available to the controller, never hidden
    /// failure identity, inferred cause, or a recommended corrective action.
    /// </summary>
    public sealed class ElectricalDistributionTrendSample
    {
        public DateTime TimestampUtc { get; internal set; }
        public double MainAVoltage { get; internal set; }
        public double MainBVoltage { get; internal set; }
        public double EssentialVoltage { get; internal set; }
        public double MainADemandAmps { get; internal set; }
        public double MainBDemandAmps { get; internal set; }
        public bool NetFlowKnown { get; internal set; }
        public double NetFlowEcPerSecond { get; internal set; }
    }

    public sealed class ElectricalDistributionTrendHistoryModel
    {
        public ElectricalDistributionTrendHistoryModel()
        {
            Samples =
                new List<ElectricalDistributionTrendSample>();
        }

        public List<ElectricalDistributionTrendSample> Samples
        {
            get;
            private set;
        }

        public int Count
        {
            get { return Samples.Count; }
        }

        public ElectricalDistributionTrendSample Oldest
        {
            get
            {
                return
                    Samples.Count > 0
                        ? Samples[0]
                        : null;
            }
        }

        public ElectricalDistributionTrendSample Latest
        {
            get
            {
                return
                    Samples.Count > 0
                        ? Samples[Samples.Count - 1]
                        : null;
            }
        }

        public double WindowSeconds
        {
            get
            {
                if (Samples.Count < 2)
                {
                    return 0.0;
                }

                return
                    Math.Max(
                        0.0,
                        (Samples[Samples.Count - 1].TimestampUtc -
                         Samples[0].TimestampUtc).TotalSeconds);
            }
        }
    }

    internal sealed class ElectricalDistributionTrendTracker
    {
        private static readonly TimeSpan MinimumSampleInterval =
            TimeSpan.FromSeconds(1.0);

        private static readonly TimeSpan RetentionWindow =
            TimeSpan.FromSeconds(60.0);

        private const int MaximumSampleCount = 64;

        private readonly List<ElectricalDistributionTrendSample>
            _samples =
                new List<ElectricalDistributionTrendSample>();

        private string _vesselId =
            string.Empty;

        private TrendBaselineSnapshot _baselineCandidate;
        private int _baselineCandidateMatches;
        private bool _baselineEstablished;

        public ElectricalDistributionTrendHistoryModel Analyze(
            DateTime receivedUtc,
            string vesselId,
            SyntheticElectricalDistributionModel distribution,
            ElectricalFlowModel flow)
        {
            string currentVessel =
                vesselId ?? string.Empty;

            if (!string.Equals(
                    _vesselId,
                    currentVessel,
                    StringComparison.Ordinal))
            {
                _samples.Clear();
                _baselineCandidate = null;
                _baselineCandidateMatches = 0;
                _baselineEstablished = false;

                _vesselId =
                    currentVessel;
            }

            if (distribution == null)
            {
                return
                    BuildModel();
            }

            DateTime utc =
                receivedUtc.Kind == DateTimeKind.Utc
                    ? receivedUtc
                    : receivedUtc.ToUniversalTime();

            if (_samples.Count > 0)
            {
                TimeSpan elapsed =
                    utc -
                    _samples[_samples.Count - 1].TimestampUtc;

                if (elapsed >= TimeSpan.Zero &&
                    elapsed < MinimumSampleInterval)
                {
                    return
                        BuildModel();
                }
            }

            SyntheticElectricalBus mainA =
                distribution.FindBus("BUS_MAIN_A");

            SyntheticElectricalBus mainB =
                distribution.FindBus("BUS_MAIN_B");

            SyntheticElectricalBus ess =
                distribution.FindBus("BUS_ESS");

            if (!_baselineEstablished)
            {
                if (!TryEstablishTrendBaseline(
                        mainA,
                        mainB,
                        ess))
                {
                    return
                        BuildModel();
                }
            }

            _samples.Add(
                new ElectricalDistributionTrendSample
                {
                    TimestampUtc =
                        utc,

                    MainAVoltage =
                        mainA != null
                            ? mainA.Voltage
                            : 0.0,

                    MainBVoltage =
                        mainB != null
                            ? mainB.Voltage
                            : 0.0,

                    EssentialVoltage =
                        ess != null
                            ? ess.Voltage
                            : 0.0,

                    MainADemandAmps =
                        mainA != null
                            ? mainA.DemandAmps
                            : 0.0,

                    MainBDemandAmps =
                        mainB != null
                            ? mainB.DemandAmps
                            : 0.0,

                    NetFlowKnown =
                        flow != null &&
                        flow.HasMeasuredNetStorageRate,

                    NetFlowEcPerSecond =
                        flow != null &&
                        flow.HasMeasuredNetStorageRate
                            ? flow.NetStorageRateEcPerSecond
                            : 0.0
                });

            DateTime cutoff =
                utc -
                RetentionWindow;

            while (_samples.Count > 0 &&
                   (_samples[0].TimestampUtc < cutoff ||
                    _samples.Count > MaximumSampleCount))
            {
                _samples.RemoveAt(0);
            }

            return
                BuildModel();
        }

        private bool TryEstablishTrendBaseline(
            SyntheticElectricalBus mainA,
            SyntheticElectricalBus mainB,
            SyntheticElectricalBus ess)
        {
            if (mainA == null ||
                mainB == null ||
                ess == null)
            {
                _baselineCandidate = null;
                _baselineCandidateMatches = 0;

                return false;
            }

            TrendBaselineSnapshot current =
                TrendBaselineSnapshot.FromBuses(
                    mainA,
                    mainB,
                    ess);

            if (_baselineCandidate == null ||
                !_baselineCandidate.IsEquivalentTo(
                    current))
            {
                _baselineCandidate =
                    current;

                _baselineCandidateMatches = 1;

                return false;
            }

            _baselineCandidateMatches++;

            if (_baselineCandidateMatches < 2)
            {
                return false;
            }

            _baselineCandidate = null;
            _baselineCandidateMatches = 0;
            _baselineEstablished = true;

            return true;
        }

        private static string NormalizeTrendSource(
            string sourceId)
        {
            if (string.IsNullOrWhiteSpace(
                    sourceId))
            {
                return "NONE";
            }

            return
                sourceId
                    .Replace(
                        "SRC_",
                        string.Empty)
                    .Trim()
                    .ToUpperInvariant();
        }

        private sealed class TrendBaselineSnapshot
        {
            public SyntheticElectricalBusState MainAState { get; set; }
            public SyntheticElectricalBusState MainBState { get; set; }
            public SyntheticElectricalBusState EssentialState { get; set; }

            public string MainASource { get; set; }
            public string MainBSource { get; set; }
            public string EssentialSource { get; set; }

            public double MainAVoltage { get; set; }
            public double MainBVoltage { get; set; }
            public double EssentialVoltage { get; set; }

            public double MainADemandAmps { get; set; }
            public double MainBDemandAmps { get; set; }

            public static TrendBaselineSnapshot FromBuses(
                SyntheticElectricalBus mainA,
                SyntheticElectricalBus mainB,
                SyntheticElectricalBus ess)
            {
                return
                    new TrendBaselineSnapshot
                    {
                        MainAState =
                            mainA.State,
                        MainBState =
                            mainB.State,
                        EssentialState =
                            ess.State,

                        MainASource =
                            NormalizeTrendSource(
                                mainA.ActiveSourceId),
                        MainBSource =
                            NormalizeTrendSource(
                                mainB.ActiveSourceId),
                        EssentialSource =
                            NormalizeTrendSource(
                                ess.ActiveSourceId),

                        MainAVoltage =
                            mainA.Voltage,
                        MainBVoltage =
                            mainB.Voltage,
                        EssentialVoltage =
                            ess.Voltage,

                        MainADemandAmps =
                            mainA.DemandAmps,
                        MainBDemandAmps =
                            mainB.DemandAmps
                    };
            }

            public bool IsEquivalentTo(
                TrendBaselineSnapshot other)
            {
                if (other == null)
                {
                    return false;
                }

                return
                    MainAState == other.MainAState &&
                    MainBState == other.MainBState &&
                    EssentialState == other.EssentialState &&
                    string.Equals(
                        MainASource,
                        other.MainASource,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        MainBSource,
                        other.MainBSource,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        EssentialSource,
                        other.EssentialSource,
                        StringComparison.Ordinal) &&
                    Math.Abs(
                        MainAVoltage -
                        other.MainAVoltage) <= 0.05 &&
                    Math.Abs(
                        MainBVoltage -
                        other.MainBVoltage) <= 0.05 &&
                    Math.Abs(
                        EssentialVoltage -
                        other.EssentialVoltage) <= 0.05 &&
                    Math.Abs(
                        MainADemandAmps -
                        other.MainADemandAmps) <= 0.01 &&
                    Math.Abs(
                        MainBDemandAmps -
                        other.MainBDemandAmps) <= 0.01;
            }
        }

        private ElectricalDistributionTrendHistoryModel BuildModel()
        {
            ElectricalDistributionTrendHistoryModel model =
                new ElectricalDistributionTrendHistoryModel();

            for (int index = 0;
                 index < _samples.Count;
                 index++)
            {
                ElectricalDistributionTrendSample sample =
                    _samples[index];

                model.Samples.Add(
                    new ElectricalDistributionTrendSample
                    {
                        TimestampUtc =
                            sample.TimestampUtc,
                        MainAVoltage =
                            sample.MainAVoltage,
                        MainBVoltage =
                            sample.MainBVoltage,
                        EssentialVoltage =
                            sample.EssentialVoltage,
                        MainADemandAmps =
                            sample.MainADemandAmps,
                        MainBDemandAmps =
                            sample.MainBDemandAmps,
                        NetFlowKnown =
                            sample.NetFlowKnown,
                        NetFlowEcPerSecond =
                            sample.NetFlowEcPerSecond
                    });
            }

            return model;
        }
    }

}

namespace KMC.Engine.Models
{
    using KMC.Engine.Electrical;

    public sealed class PowerModel
    {
        public PowerModel()
        {
            ElectricalNetwork =
                new ElectricalNetwork();

            Flow =
                new ElectricalFlowModel();

            Attribution =
                new ElectricalAttributionModel();

            Load =
                new ElectricalLoadModel();

            Diagnostic =
                new ElectricalPowerDiagnosticModel();

            LoadShedding =
                new ElectricalLoadSheddingModel();

            Procedure =
                new ElectricalProcedureModel();

            Events =
                new ElectricalEventHistoryModel();

            DistributionEvents =
                new ElectricalDistributionEventHistoryModel();

            DistributionTrend =
                new ElectricalDistributionTrendHistoryModel();

            Diagnostics =
                new List<string>();
        }

        public ElectricalNetwork ElectricalNetwork
        {
            get;
            internal set;
        }

        public ElectricalFlowModel Flow
        {
            get;
            internal set;
        }

        public ElectricalAttributionModel Attribution
        {
            get;
            internal set;
        }

        public ElectricalLoadModel Load
        {
            get;
            internal set;
        }

        public ElectricalPowerDiagnosticModel Diagnostic
        {
            get;
            internal set;
        }

        public ElectricalLoadSheddingModel LoadShedding
        {
            get;
            internal set;
        }

        public ElectricalProcedureModel Procedure
        {
            get;
            internal set;
        }

        public ElectricalEventHistoryModel Events
        {
            get;
            internal set;
        }

        /// <summary>
        /// Build 14.14.3 transition history for the switched A/B/ESS
        /// distribution model. This is intentionally separate from the older
        /// aggregate KSP electrical diagnostic event history.
        /// </summary>
        public ElectricalDistributionEventHistoryModel DistributionEvents
        {
            get;
            internal set;
        }

        /// <summary>
        /// Build 14.14.4 rolling controller-observable electrical evidence.
        /// </summary>
        public ElectricalDistributionTrendHistoryModel DistributionTrend
        {
            get;
            internal set;
        }


        public List<string> Diagnostics
        {
            get;
            private set;
        }
    }
}
