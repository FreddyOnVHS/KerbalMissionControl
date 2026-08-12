using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    public enum FailureSimulationMode
    {
        Nominal = 0,
        Reliability = 1,
        Training = 2,
        Scenario = 3
    }

    public enum SyntheticFailureKind
    {
        Sudden = 0,
        Degrading = 1,
        Intermittent = 2,
        Cascade = 3
    }

    public enum SyntheticFailureSeverity
    {
        Advisory = 0,
        Caution = 1,
        Critical = 2
    }

    public enum SyntheticFailureTargetKind
    {
        Component = 0,
        ElectricalSource = 1,
        Instrumentation = 2,
        PowerEffect = 3,
        PropulsionEffect = 4
    }

    /// <summary>
    /// Explicit integration targets whose meaning is vehicle-wide rather than
    /// a claim about stock KSP wiring.
    /// </summary>
    public static class SyntheticFailureTargets
    {
        public const string ElectricChargeLeak =
            "PWR_EC_LEAK";

        public const string EngineDeratePrefix =
            "PROP_ENGINE_DERATE:";

        public const string EngineShutdownPrefix =
            "PROP_ENGINE_SHUTDOWN:";

        public static string CreateEngineDerateTarget(
            uint partPersistentId)
        {
            return
                EngineDeratePrefix +
                partPersistentId.ToString();
        }

        public static string CreateEngineShutdownTarget(
            uint partPersistentId)
        {
            return
                EngineShutdownPrefix +
                partPersistentId.ToString();
        }

        public static bool TryParsePropulsionTarget(
            string targetId,
            out uint partPersistentId,
            out bool shutdown)
        {
            partPersistentId = 0;
            shutdown = false;

            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string value;

            if (targetId.StartsWith(
                    EngineDeratePrefix,
                    StringComparison.Ordinal))
            {
                value =
                    targetId.Substring(
                        EngineDeratePrefix.Length);
            }
            else if (targetId.StartsWith(
                         EngineShutdownPrefix,
                         StringComparison.Ordinal))
            {
                shutdown = true;
                value =
                    targetId.Substring(
                        EngineShutdownPrefix.Length);
            }
            else
            {
                return false;
            }

            return
                uint.TryParse(
                    value,
                    out partPersistentId) &&
                partPersistentId != 0;
        }
    }

    public enum SyntheticFailureCondition
    {
        Armed = 0,
        Active = 1,
        Cleared = 2
    }

    public enum SyntheticFailureEventKind
    {
        ModeChanged = 0,
        Accepted = 1,
        Activated = 2,
        Recovered = 3,
        Cleared = 4,
        Rejected = 5
    }

    public sealed class SyntheticFailureRequest
    {
        public SyntheticFailureRequest()
        {
            VesselId = string.Empty;
            TargetId = string.Empty;
            TargetKind = SyntheticFailureTargetKind.Component;
            Kind = SyntheticFailureKind.Sudden;
            Severity = SyntheticFailureSeverity.Caution;
            ComponentHealth = SpacecraftSystemHealth.Failed;
            ActivateUtc = DateTime.UtcNow;
            ClearUtc = DateTime.MinValue;
            IntermittentPeriodSeconds = 4.0;
            IntermittentDutyCycle = 0.50;
            ParentFailureId = string.Empty;
            Detail = string.Empty;
            EffectMagnitude = double.NaN;
        }

        public string VesselId { get; set; }
        public string TargetId { get; set; }
        public SyntheticFailureTargetKind TargetKind { get; set; }
        public SyntheticFailureKind Kind { get; set; }
        public SyntheticFailureSeverity Severity { get; set; }
        public SpacecraftSystemHealth ComponentHealth { get; set; }
        public DateTime ActivateUtc { get; set; }
        public DateTime ClearUtc { get; set; }
        public double IntermittentPeriodSeconds { get; set; }
        public double IntermittentDutyCycle { get; set; }
        public string ParentFailureId { get; set; }
        public string Detail { get; set; }

        /// <summary>
        /// Optional magnitude for an explicit real-effect integration target.
        /// Build 14.5 uses EC/s for PWR_EC_LEAK.
        /// </summary>
        public double EffectMagnitude { get; set; }
    }

    public sealed class SyntheticFailureRecord
    {
        public SyntheticFailureRecord()
        {
            FailureId = string.Empty;
            VesselId = string.Empty;
            TargetId = string.Empty;
            ParentFailureId = string.Empty;
            Detail = string.Empty;
            EffectMagnitude = double.NaN;
            CreatedUtc = DateTime.MinValue;
            ActivateUtc = DateTime.MinValue;
            ClearUtc = DateTime.MinValue;
            LastTransitionUtc = DateTime.MinValue;
            Condition = SyntheticFailureCondition.Armed;
        }

        public string FailureId { get; internal set; }
        public string VesselId { get; internal set; }
        public string TargetId { get; internal set; }
        public SyntheticFailureTargetKind TargetKind { get; internal set; }
        public SyntheticFailureKind Kind { get; internal set; }
        public SyntheticFailureSeverity Severity { get; internal set; }
        public SpacecraftSystemHealth ComponentHealth { get; internal set; }
        public DateTime CreatedUtc { get; internal set; }
        public DateTime ActivateUtc { get; internal set; }
        public DateTime ClearUtc { get; internal set; }
        public double IntermittentPeriodSeconds { get; internal set; }
        public double IntermittentDutyCycle { get; internal set; }
        public string ParentFailureId { get; internal set; }
        public string Detail { get; internal set; }
        public double EffectMagnitude { get; internal set; }
        public SyntheticFailureCondition Condition { get; internal set; }
        public DateTime LastTransitionUtc { get; internal set; }
        public bool EffectiveNow { get; internal set; }

        internal SyntheticFailureRecord Clone()
        {
            return
                new SyntheticFailureRecord
                {
                    FailureId = FailureId ?? string.Empty,
                    VesselId = VesselId ?? string.Empty,
                    TargetId = TargetId ?? string.Empty,
                    TargetKind = TargetKind,
                    Kind = Kind,
                    Severity = Severity,
                    ComponentHealth = ComponentHealth,
                    CreatedUtc = CreatedUtc,
                    ActivateUtc = ActivateUtc,
                    ClearUtc = ClearUtc,
                    IntermittentPeriodSeconds = IntermittentPeriodSeconds,
                    IntermittentDutyCycle = IntermittentDutyCycle,
                    ParentFailureId = ParentFailureId ?? string.Empty,
                    Detail = Detail ?? string.Empty,
                    EffectMagnitude = EffectMagnitude,
                    Condition = Condition,
                    LastTransitionUtc = LastTransitionUtc,
                    EffectiveNow = EffectiveNow
                };
        }
    }

    public sealed class SyntheticFailureEvent
    {
        public SyntheticFailureEvent()
        {
            TimestampUtc = DateTime.MinValue;
            FailureId = string.Empty;
            VesselId = string.Empty;
            TargetId = string.Empty;
            Detail = string.Empty;
        }

        public DateTime TimestampUtc { get; internal set; }
        public SyntheticFailureEventKind EventKind { get; internal set; }
        public string FailureId { get; internal set; }
        public string VesselId { get; internal set; }
        public string TargetId { get; internal set; }
        public string Detail { get; internal set; }

        internal SyntheticFailureEvent Clone()
        {
            return
                new SyntheticFailureEvent
                {
                    TimestampUtc = TimestampUtc,
                    EventKind = EventKind,
                    FailureId = FailureId ?? string.Empty,
                    VesselId = VesselId ?? string.Empty,
                    TargetId = TargetId ?? string.Empty,
                    Detail = Detail ?? string.Empty
                };
        }
    }

    public sealed class FailureSimulationSnapshot
    {
        private readonly List<SyntheticFailureRecord> _failures;
        private readonly List<SyntheticFailureEvent> _events;

        public FailureSimulationSnapshot()
        {
            VesselId = string.Empty;
            GeneratedUtc = DateTime.MinValue;
            Mode = FailureSimulationMode.Nominal;
            _failures = new List<SyntheticFailureRecord>();
            _events = new List<SyntheticFailureEvent>();
        }

        public string VesselId { get; internal set; }
        public DateTime GeneratedUtc { get; internal set; }
        public FailureSimulationMode Mode { get; internal set; }

        public IList<SyntheticFailureRecord> Failures
        {
            get { return _failures; }
        }

        public IList<SyntheticFailureEvent> Events
        {
            get { return _events; }
        }

        public int ActiveFailureCount
        {
            get
            {
                int count = 0;

                for (int index = 0;
                     index < _failures.Count;
                     index++)
                {
                    SyntheticFailureRecord failure =
                        _failures[index];

                    if (failure != null &&
                        failure.EffectiveNow)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public FailureSimulationSnapshot Clone()
        {
            FailureSimulationSnapshot clone =
                new FailureSimulationSnapshot
                {
                    VesselId = VesselId ?? string.Empty,
                    GeneratedUtc = GeneratedUtc,
                    Mode = Mode
                };

            for (int index = 0;
                 index < _failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    _failures[index];

                if (failure != null)
                {
                    clone.Failures.Add(
                        failure.Clone());
                }
            }

            for (int index = 0;
                 index < _events.Count;
                 index++)
            {
                SyntheticFailureEvent item =
                    _events[index];

                if (item != null)
                {
                    clone.Events.Add(
                        item.Clone());
                }
            }

            return clone;
        }
    }
}
