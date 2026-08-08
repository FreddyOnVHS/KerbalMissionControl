using System;
using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    public enum ElectricalEventSeverity
    {
        Info = 0,
        Advisory,
        Warning,
        Critical
    }

    public enum ElectricalEventKind
    {
        PowerState = 0,
        Condition,
        Procedure,
        Recovery,
        Observability,
        StageRisk
    }

    public sealed class ElectricalEventRecord
    {
        public long Sequence { get; internal set; }
        public DateTime TimestampUtc { get; internal set; }
        public ElectricalEventSeverity Severity { get; internal set; }
        public ElectricalEventKind Kind { get; internal set; }
        public string Code { get; internal set; }
        public string Message { get; internal set; }
    }

    public sealed class ElectricalEventHistoryModel
    {
        public ElectricalEventHistoryModel()
        {
            Events =
                new List<ElectricalEventRecord>();
        }

        public List<ElectricalEventRecord> Events
        {
            get;
            private set;
        }

        public int Count
        {
            get { return Events.Count; }
        }

        public ElectricalEventRecord Latest
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
    /// Stateful transition detector for electrical cautions, warnings,
    /// procedures, observability changes, recovery, and staging risk.
    ///
    /// Events are generated on transitions only. Repeated telemetry packets
    /// carrying the same state do not create duplicate history entries.
    /// </summary>
    internal sealed class ElectricalEventTracker
    {
        private const int MaximumHistoryCount = 32;

        private readonly List<ElectricalEventRecord> _events =
            new List<ElectricalEventRecord>();

        private bool _established;
        private string _vesselName =
            string.Empty;

        private ElectricalPowerSeverity _previousSeverity;
        private ElectricalPowerCondition _previousCondition;
        private ElectricalDemandObservability _previousObservability;
        private ElectricalProcedureState _previousProcedureState;
        private ElectricalRecoveryState _previousRecoveryState;
        private bool _previousStageRisk;
        private bool _previousLoseAllStorage;
        private long _nextSequence = 1;

        public ElectricalEventHistoryModel Analyze(
            DateTime receivedUtc,
            string vesselName,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalLoadModel load,
            ElectricalProcedureModel procedure)
        {
            DateTime utc =
                receivedUtc.Kind == DateTimeKind.Utc
                    ? receivedUtc
                    : receivedUtc.ToUniversalTime();

            string currentVessel =
                vesselName ?? string.Empty;

            if (!string.Equals(
                    _vesselName,
                    currentVessel,
                    StringComparison.Ordinal))
            {
                Reset(
                    currentVessel);
            }

            if (diagnostic == null ||
                !diagnostic.TelemetryAvailable)
            {
                if (_established &&
                    diagnostic != null &&
                    diagnostic.Condition ==
                        ElectricalPowerCondition.TelemetryUnavailable &&
                    _previousCondition !=
                        ElectricalPowerCondition.TelemetryUnavailable)
                {
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Advisory,
                        ElectricalEventKind.Condition,
                        "POWER TELEMETRY LOST",
                        diagnostic.Summary);

                    _previousCondition =
                        diagnostic.Condition;
                }

                return BuildModel();
            }

            if (!_established)
            {
                Establish(
                    diagnostic,
                    procedure);

                return BuildModel();
            }

            TrackSeverityTransition(
                utc,
                diagnostic);

            TrackConditionTransition(
                utc,
                diagnostic);

            TrackObservabilityTransition(
                utc,
                diagnostic);

            TrackStageRiskTransition(
                utc,
                diagnostic);

            TrackProcedureTransition(
                utc,
                diagnostic,
                procedure);

            TrackRecoveryTransition(
                utc,
                diagnostic,
                procedure);

            _previousSeverity =
                diagnostic.Severity;

            _previousCondition =
                diagnostic.Condition;

            _previousObservability =
                diagnostic.DemandObservability;

            _previousStageRisk =
                diagnostic.NextStageLosesStorage;

            _previousLoseAllStorage =
                diagnostic.NextStageLosesAllStorage;

            if (procedure != null)
            {
                _previousProcedureState =
                    procedure.State;

                _previousRecoveryState =
                    procedure.RecoveryState;
            }

            return BuildModel();
        }

        private void Establish(
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalProcedureModel procedure)
        {
            _established =
                true;

            _previousSeverity =
                diagnostic.Severity;

            _previousCondition =
                diagnostic.Condition;

            _previousObservability =
                diagnostic.DemandObservability;

            _previousStageRisk =
                diagnostic.NextStageLosesStorage;

            _previousLoseAllStorage =
                diagnostic.NextStageLosesAllStorage;

            if (procedure != null)
            {
                _previousProcedureState =
                    procedure.State;

                _previousRecoveryState =
                    procedure.RecoveryState;
            }
        }

        private void TrackSeverityTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic)
        {
            if (diagnostic.Severity ==
                _previousSeverity)
            {
                return;
            }

            ElectricalEventSeverity severity;
            string code;

            switch (diagnostic.Severity)
            {
                case ElectricalPowerSeverity.Normal:
                    severity =
                        ElectricalEventSeverity.Info;

                    code =
                        "POWER NORMAL";
                    break;

                case ElectricalPowerSeverity.Advisory:
                    severity =
                        ElectricalEventSeverity.Advisory;

                    code =
                        "POWER ADVISORY";
                    break;

                case ElectricalPowerSeverity.Warning:
                    severity =
                        ElectricalEventSeverity.Warning;

                    code =
                        "POWER WARNING";
                    break;

                case ElectricalPowerSeverity.Critical:
                    severity =
                        ElectricalEventSeverity.Critical;

                    code =
                        "POWER CRITICAL";
                    break;

                case ElectricalPowerSeverity.Blackout:
                    severity =
                        ElectricalEventSeverity.Critical;

                    code =
                        "POWER BLACKOUT";
                    break;

                default:
                    return;
            }

            AddEvent(
                utc,
                severity,
                ElectricalEventKind.PowerState,
                code,
                diagnostic.Summary);
        }

        private void TrackConditionTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic)
        {
            if (diagnostic.Condition ==
                _previousCondition)
            {
                return;
            }

            switch (diagnostic.Condition)
            {
                case ElectricalPowerCondition.TelemetryUnavailable:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Advisory,
                        ElectricalEventKind.Condition,
                        "POWER TELEMETRY LOST",
                        diagnostic.Summary);
                    break;

                case ElectricalPowerCondition.DataIncomplete:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Advisory,
                        ElectricalEventKind.Condition,
                        "POWER DATA INCOMPLETE",
                        diagnostic.Summary);
                    break;

                case ElectricalPowerCondition.StorageSaturated:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Info,
                        ElectricalEventKind.Condition,
                        "STORAGE SATURATED",
                        diagnostic.Summary);
                    break;

                case ElectricalPowerCondition.ImminentDepletion:
                    AddEvent(
                        utc,
                        diagnostic.Severity ==
                            ElectricalPowerSeverity.Critical
                                ? ElectricalEventSeverity.Critical
                                : ElectricalEventSeverity.Warning,
                        ElectricalEventKind.Condition,
                        "IMMINENT DEPLETION",
                        diagnostic.Summary);
                    break;

                case ElectricalPowerCondition.Depleted:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Critical,
                        ElectricalEventKind.Condition,
                        "STORAGE DEPLETED",
                        diagnostic.Summary);
                    break;

                default:
                    if (_previousCondition ==
                        ElectricalPowerCondition.TelemetryUnavailable)
                    {
                        AddEvent(
                            utc,
                            ElectricalEventSeverity.Info,
                            ElectricalEventKind.Condition,
                            "POWER TELEMETRY RESTORED",
                            diagnostic.Summary);
                    }
                    else if (_previousCondition ==
                             ElectricalPowerCondition.DataIncomplete)
                    {
                        AddEvent(
                            utc,
                            ElectricalEventSeverity.Info,
                            ElectricalEventKind.Condition,
                            "POWER DATA COMPLETE",
                            diagnostic.Summary);
                    }
                    break;
            }
        }

        private void TrackObservabilityTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic)
        {
            ElectricalDemandObservability current =
                diagnostic.DemandObservability;

            if (current ==
                _previousObservability)
            {
                return;
            }

            if (current ==
                ElectricalDemandObservability.UnobservableAtCapacity)
            {
                AddEvent(
                    utc,
                    ElectricalEventSeverity.Info,
                    ElectricalEventKind.Observability,
                    "DEMAND OBSERVABILITY LOST",
                    "Total vessel demand is unobservable at the upper EC storage boundary.");

                return;
            }

            if (current ==
                ElectricalDemandObservability.UnobservableAtDepletion)
            {
                AddEvent(
                    utc,
                    ElectricalEventSeverity.Critical,
                    ElectricalEventKind.Observability,
                    "DEMAND OBSERVABILITY LOST",
                    "Total vessel demand is unobservable while EC storage is depleted.");

                return;
            }

            if (current ==
                    ElectricalDemandObservability.Observable &&
                (_previousObservability ==
                     ElectricalDemandObservability.UnobservableAtCapacity ||
                 _previousObservability ==
                     ElectricalDemandObservability.UnobservableAtDepletion))
            {
                AddEvent(
                    utc,
                    ElectricalEventSeverity.Info,
                    ElectricalEventKind.Observability,
                    "DEMAND OBSERVABILITY RESTORED",
                    "Storage flow again supports total-demand inference.");
            }
        }

        private void TrackStageRiskTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic)
        {
            bool currentRisk =
                diagnostic.NextStageLosesStorage;

            bool currentLoseAll =
                diagnostic.NextStageLosesAllStorage;

            if (currentRisk &&
                (!_previousStageRisk ||
                 currentLoseAll !=
                    _previousLoseAllStorage))
            {
                AddEvent(
                    utc,
                    currentLoseAll
                        ? ElectricalEventSeverity.Critical
                        : ElectricalEventSeverity.Advisory,
                    ElectricalEventKind.StageRisk,
                    currentLoseAll
                        ? "STAGE LOSES ALL EC STORAGE"
                        : "STAGE STORAGE HAZARD",
                    currentLoseAll
                        ? "Next stage removes all known electrical storage."
                        : "Next stage removes part of the electrical storage system.");

                return;
            }

            if (!currentRisk &&
                _previousStageRisk)
            {
                AddEvent(
                    utc,
                    ElectricalEventSeverity.Info,
                    ElectricalEventKind.StageRisk,
                    "STAGE STORAGE HAZARD CLEARED",
                    "Next-stage electrical storage loss is no longer indicated.");
            }
        }

        private void TrackProcedureTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalProcedureModel procedure)
        {
            if (procedure == null ||
                procedure.State ==
                    _previousProcedureState)
            {
                return;
            }

            ElectricalEventSeverity severity =
                SeverityFromPower(
                    diagnostic.Severity);

            switch (procedure.State)
            {
                case ElectricalProcedureState.ConservePower:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Advisory,
                        ElectricalEventKind.Procedure,
                        "CONSERVE POWER",
                        procedure.PrimaryAction);
                    break;

                case ElectricalProcedureState.ShedNonessentialLoad:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Advisory,
                        ElectricalEventKind.Procedure,
                        "SHED NONESSENTIAL LOAD",
                        procedure.PrimaryAction);
                    break;

                case ElectricalProcedureState.ImmediateLoadReduction:
                    AddEvent(
                        utc,
                        severity < ElectricalEventSeverity.Warning
                            ? ElectricalEventSeverity.Warning
                            : severity,
                        ElectricalEventKind.Procedure,
                        "LOAD REDUCTION REQUIRED",
                        procedure.PrimaryAction);
                    break;

                case ElectricalProcedureState.RestoreGeneration:
                    AddEvent(
                        utc,
                        severity,
                        ElectricalEventKind.Procedure,
                        "RESTORE GENERATION",
                        procedure.PrimaryAction);
                    break;

                case ElectricalProcedureState.BlackoutRecovery:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Critical,
                        ElectricalEventKind.Procedure,
                        "BLACKOUT RECOVERY",
                        procedure.PrimaryAction);
                    break;
            }
        }

        private void TrackRecoveryTransition(
            DateTime utc,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalProcedureModel procedure)
        {
            if (procedure == null ||
                procedure.RecoveryState ==
                    _previousRecoveryState)
            {
                return;
            }

            switch (procedure.RecoveryState)
            {
                case ElectricalRecoveryState.Worsening:
                    AddEvent(
                        utc,
                        SeverityFromPower(
                            diagnostic.Severity),
                        ElectricalEventKind.Recovery,
                        "DEFICIT WORSENING",
                        procedure.Verification);
                    break;

                case ElectricalRecoveryState.Improving:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Info,
                        ElectricalEventKind.Recovery,
                        "RECOVERY DETECTED",
                        procedure.Verification);
                    break;

                case ElectricalRecoveryState.StableImprovement:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Info,
                        ElectricalEventKind.Recovery,
                        "RECOVERY STABLE",
                        procedure.Verification);
                    break;

                case ElectricalRecoveryState.DeficitCleared:
                    AddEvent(
                        utc,
                        ElectricalEventSeverity.Info,
                        ElectricalEventKind.Recovery,
                        "DEFICIT CLEARED",
                        procedure.Verification);
                    break;
            }
        }

        private static ElectricalEventSeverity SeverityFromPower(
            ElectricalPowerSeverity severity)
        {
            switch (severity)
            {
                case ElectricalPowerSeverity.Advisory:
                    return ElectricalEventSeverity.Advisory;

                case ElectricalPowerSeverity.Warning:
                    return ElectricalEventSeverity.Warning;

                case ElectricalPowerSeverity.Critical:
                case ElectricalPowerSeverity.Blackout:
                    return ElectricalEventSeverity.Critical;

                default:
                    return ElectricalEventSeverity.Info;
            }
        }

        private void AddEvent(
            DateTime utc,
            ElectricalEventSeverity severity,
            ElectricalEventKind kind,
            string code,
            string message)
        {
            ElectricalEventRecord record =
                new ElectricalEventRecord
                {
                    Sequence =
                        _nextSequence++,

                    TimestampUtc =
                        utc,

                    Severity =
                        severity,

                    Kind =
                        kind,

                    Code =
                        code ?? string.Empty,

                    Message =
                        message ?? string.Empty
                };

            _events.Add(
                record);

            while (_events.Count >
                   MaximumHistoryCount)
            {
                _events.RemoveAt(
                    0);
            }
        }

        private ElectricalEventHistoryModel BuildModel()
        {
            ElectricalEventHistoryModel model =
                new ElectricalEventHistoryModel();

            for (int i = 0;
                 i < _events.Count;
                 i++)
            {
                ElectricalEventRecord source =
                    _events[i];

                model.Events.Add(
                    new ElectricalEventRecord
                    {
                        Sequence =
                            source.Sequence,

                        TimestampUtc =
                            source.TimestampUtc,

                        Severity =
                            source.Severity,

                        Kind =
                            source.Kind,

                        Code =
                            source.Code,

                        Message =
                            source.Message
                    });
            }

            return model;
        }

        private void Reset(
            string vesselName)
        {
            _events.Clear();

            _vesselName =
                vesselName ?? string.Empty;

            _established =
                false;

            _previousSeverity =
                ElectricalPowerSeverity.Unknown;

            _previousCondition =
                ElectricalPowerCondition.Unknown;

            _previousObservability =
                ElectricalDemandObservability.Unknown;

            _previousProcedureState =
                ElectricalProcedureState.Unavailable;

            _previousRecoveryState =
                ElectricalRecoveryState.None;

            _previousStageRisk =
                false;

            _previousLoseAllStorage =
                false;

            _nextSequence =
                1;
        }
    }
}
