using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace KMC.Engine.SpacecraftSystems
{
    /// <summary>
    /// Build 14.3 Engine-owned hidden failure truth.
    ///
    /// The engine owns synthetic fault identity, scheduling, state transitions,
    /// intermittent duty cycles, cascade prerequisites and event history.
    ///
    /// It does not mutate KSP. Real vessel effects begin in Build 14.4.
    /// </summary>
    public sealed class SyntheticFailureEngine
    {
        private const int MaximumEventHistory = 64;

        private readonly object _syncRoot;
        private readonly Dictionary<string, VesselFailureState> _byVessel;
        private long _failureSequence;

#if DEBUG
        private static bool _selfTestCompleted;
#endif

        public SyntheticFailureEngine()
        {
            _syncRoot = new object();
            _byVessel =
                new Dictionary<string, VesselFailureState>(
                    StringComparer.Ordinal);
            _failureSequence = 0;
        }

        public bool SetMode(
            string vesselId,
            FailureSimulationMode mode,
            out string resultText)
        {
            resultText = string.Empty;

            if (string.IsNullOrWhiteSpace(vesselId))
            {
                resultText = "VESSEL ID REQUIRED";
                return false;
            }

            lock (_syncRoot)
            {
                VesselFailureState state =
                    GetOrCreateState(
                        vesselId);

                state.Mode =
                    mode;

                AddEvent(
                    state,
                    new SyntheticFailureEvent
                    {
                        TimestampUtc = DateTime.UtcNow,
                        EventKind =
                            SyntheticFailureEventKind.ModeChanged,
                        VesselId = vesselId,
                        Detail =
                            "MODE " +
                            mode.ToString().ToUpperInvariant()
                    });

                resultText =
                    "ACK MODE " +
                    mode.ToString().ToUpperInvariant();
            }

            Debug.WriteLine(
                "KMC.Engine FAILURE COMMAND ACK" +
                " | VesselId=" + vesselId +
                " | Command=SET MODE" +
                " | Result=" + resultText);

            return true;
        }

        public bool Inject(
            SyntheticFailureRequest request,
            out string failureId,
            out string resultText)
        {
            failureId = string.Empty;
            resultText = string.Empty;

            if (request == null)
            {
                resultText = "FAILURE REQUEST REQUIRED";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.VesselId) ||
                string.IsNullOrWhiteSpace(request.TargetId))
            {
                resultText = "VESSEL AND TARGET REQUIRED";
                return false;
            }

            lock (_syncRoot)
            {
                VesselFailureState state =
                    GetOrCreateState(
                        request.VesselId);

                if (state.Mode ==
                        FailureSimulationMode.Nominal)
                {
                    resultText =
                        "REJECTED - FAILURE MODE NOMINAL";

                    AddRejectedEvent(
                        state,
                        request,
                        resultText);

                    WriteAck(
                        request.VesselId,
                        string.Empty,
                        request.TargetId,
                        resultText);

                    return false;
                }

                if (request.ComponentHealth ==
                        SpacecraftSystemHealth.Nominal)
                {
                    resultText =
                        "REJECTED - FAILURE HEALTH MUST BE DEGRADED OR FAILED";

                    AddRejectedEvent(
                        state,
                        request,
                        resultText);

                    WriteAck(
                        request.VesselId,
                        string.Empty,
                        request.TargetId,
                        resultText);

                    return false;
                }

                DateTime activateUtc =
                    request.ActivateUtc == DateTime.MinValue
                        ? DateTime.UtcNow
                        : request.ActivateUtc;

                DateTime clearUtc =
                    request.ClearUtc;

                if (clearUtc != DateTime.MinValue &&
                    clearUtc <= activateUtc)
                {
                    resultText =
                        "REJECTED - CLEAR TIME PRECEDES ACTIVATION";

                    AddRejectedEvent(
                        state,
                        request,
                        resultText);

                    WriteAck(
                        request.VesselId,
                        string.Empty,
                        request.TargetId,
                        resultText);

                    return false;
                }

                _failureSequence++;

                failureId =
                    "FAIL-14.3-" +
                    _failureSequence.ToString("0000");

                SyntheticFailureRecord record =
                    new SyntheticFailureRecord
                    {
                        FailureId = failureId,
                        VesselId = request.VesselId,
                        TargetId = request.TargetId,
                        TargetKind = request.TargetKind,
                        Kind = request.Kind,
                        Severity = request.Severity,
                        ComponentHealth = request.ComponentHealth,
                        CreatedUtc = DateTime.UtcNow,
                        ActivateUtc = activateUtc,
                        ClearUtc = clearUtc,
                        IntermittentPeriodSeconds =
                            NormalizePeriod(
                                request.IntermittentPeriodSeconds),
                        IntermittentDutyCycle =
                            NormalizeDutyCycle(
                                request.IntermittentDutyCycle),
                        ParentFailureId =
                            request.ParentFailureId ?? string.Empty,
                        Detail =
                            request.Detail ?? string.Empty,
                        Condition =
                            SyntheticFailureCondition.Armed,
                        LastTransitionUtc =
                            DateTime.UtcNow,
                        EffectiveNow = false
                    };

                state.Failures.Add(
                    record);

                resultText =
                    "ACK ACCEPTED " +
                    failureId;

                AddEvent(
                    state,
                    new SyntheticFailureEvent
                    {
                        TimestampUtc = DateTime.UtcNow,
                        EventKind =
                            SyntheticFailureEventKind.Accepted,
                        FailureId = failureId,
                        VesselId = request.VesselId,
                        TargetId = request.TargetId,
                        Detail =
                            DescribeFailure(record)
                    });
            }

            WriteAck(
                request.VesselId,
                failureId,
                request.TargetId,
                resultText);

            return true;
        }

        public bool ClearFailure(
            string vesselId,
            string failureId,
            out string resultText)
        {
            resultText = string.Empty;

            if (string.IsNullOrWhiteSpace(vesselId) ||
                string.IsNullOrWhiteSpace(failureId))
            {
                resultText = "VESSEL AND FAILURE ID REQUIRED";
                return false;
            }

            lock (_syncRoot)
            {
                VesselFailureState state;

                if (!_byVessel.TryGetValue(
                        vesselId,
                        out state))
                {
                    resultText =
                        "FAILURE NOT FOUND";

                    return false;
                }

                SyntheticFailureRecord record =
                    FindFailure(
                        state,
                        failureId);

                if (record == null)
                {
                    resultText =
                        "FAILURE NOT FOUND";

                    return false;
                }

                record.Condition =
                    SyntheticFailureCondition.Cleared;
                record.EffectiveNow =
                    false;
                record.LastTransitionUtc =
                    DateTime.UtcNow;

                AddEvent(
                    state,
                    new SyntheticFailureEvent
                    {
                        TimestampUtc = DateTime.UtcNow,
                        EventKind =
                            SyntheticFailureEventKind.Cleared,
                        FailureId = record.FailureId,
                        VesselId = vesselId,
                        TargetId = record.TargetId,
                        Detail = "CREW/HOST CLEARED"
                    });

                resultText =
                    "ACK CLEARED " +
                    failureId;
            }

            WriteAck(
                vesselId,
                failureId,
                string.Empty,
                resultText);

            return true;
        }

        public FailureSimulationSnapshot GetSnapshot(
            string vesselId,
            DateTime generatedUtc)
        {
#if DEBUG
            RunSelfTestOnce();
#endif

            if (string.IsNullOrWhiteSpace(vesselId))
            {
                return
                    new FailureSimulationSnapshot
                    {
                        GeneratedUtc = generatedUtc
                    };
            }

            lock (_syncRoot)
            {
                VesselFailureState state =
                    GetOrCreateState(
                        vesselId);

                EvaluateState(
                    state,
                    generatedUtc);

                FailureSimulationSnapshot snapshot =
                    new FailureSimulationSnapshot
                    {
                        VesselId = vesselId,
                        GeneratedUtc = generatedUtc,
                        Mode = state.Mode
                    };

                for (int index = 0;
                     index < state.Failures.Count;
                     index++)
                {
                    SyntheticFailureRecord failure =
                        state.Failures[index];

                    if (failure != null)
                    {
                        snapshot.Failures.Add(
                            failure.Clone());
                    }
                }

                for (int index = 0;
                     index < state.Events.Count;
                     index++)
                {
                    SyntheticFailureEvent item =
                        state.Events[index];

                    if (item != null)
                    {
                        snapshot.Events.Add(
                            item.Clone());
                    }
                }

                return snapshot;
            }
        }

        public static void ApplyComponentFailures(
            SpacecraftSystemsModel systems,
            FailureSimulationSnapshot snapshot)
        {
            if (systems == null ||
                snapshot == null ||
                snapshot.Mode ==
                    FailureSimulationMode.Nominal)
            {
                return;
            }

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.Component)
                {
                    continue;
                }

                SpacecraftSystemComponent component =
                    systems.FindComponent(
                        failure.TargetId);

                if (component == null)
                {
                    continue;
                }

                component.Health =
                    MoreSevereHealth(
                        component.Health,
                        failure.ComponentHealth);
            }

            systems.Recalculate();
        }

        public static void ApplyElectricalSourceFailures(
            SyntheticElectricalDistributionModel distribution,
            FailureSimulationSnapshot snapshot)
        {
            if (distribution == null ||
                snapshot == null ||
                snapshot.Mode ==
                    FailureSimulationMode.Nominal)
            {
                return;
            }

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow ||
                    failure.TargetKind !=
                        SyntheticFailureTargetKind.ElectricalSource)
                {
                    continue;
                }

                SyntheticElectricalSource source =
                    distribution.FindSource(
                        failure.TargetId);

                if (source == null)
                {
                    continue;
                }

                source.State =
                    failure.ComponentHealth ==
                        SpacecraftSystemHealth.Failed
                        ? SyntheticElectricalSourceState.Offline
                        : SyntheticElectricalSourceState.Degraded;
            }
        }

        private void EvaluateState(
            VesselFailureState state,
            DateTime nowUtc)
        {
            for (int index = 0;
                 index < state.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    state.Failures[index];

                if (failure == null ||
                    failure.Condition ==
                        SyntheticFailureCondition.Cleared)
                {
                    continue;
                }

                bool effective =
                    DetermineEffective(
                        state,
                        failure,
                        nowUtc);

                if (failure.ClearUtc != DateTime.MinValue &&
                    nowUtc >= failure.ClearUtc)
                {
                    if (failure.Condition !=
                            SyntheticFailureCondition.Cleared)
                    {
                        failure.Condition =
                            SyntheticFailureCondition.Cleared;
                        failure.EffectiveNow =
                            false;
                        failure.LastTransitionUtc =
                            nowUtc;

                        AddTransitionEvent(
                            state,
                            failure,
                            SyntheticFailureEventKind.Cleared,
                            "TIMED CLEAR");
                    }

                    continue;
                }

                if (effective !=
                        failure.EffectiveNow)
                {
                    failure.EffectiveNow =
                        effective;

                    failure.Condition =
                        effective
                            ? SyntheticFailureCondition.Active
                            : SyntheticFailureCondition.Armed;

                    failure.LastTransitionUtc =
                        nowUtc;

                    AddTransitionEvent(
                        state,
                        failure,
                        effective
                            ? SyntheticFailureEventKind.Activated
                            : SyntheticFailureEventKind.Recovered,
                        effective
                            ? "FAILURE EFFECT ACTIVE"
                            : "INTERMITTENT RECOVERY");
                }
            }
        }

        private static bool DetermineEffective(
            VesselFailureState state,
            SyntheticFailureRecord failure,
            DateTime nowUtc)
        {
            if (failure == null ||
                nowUtc < failure.ActivateUtc)
            {
                return false;
            }

            if (failure.ClearUtc != DateTime.MinValue &&
                nowUtc >= failure.ClearUtc)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(
                    failure.ParentFailureId))
            {
                SyntheticFailureRecord parent =
                    FindFailure(
                        state,
                        failure.ParentFailureId);

                if (parent == null ||
                    !parent.EffectiveNow)
                {
                    return false;
                }
            }

            if (failure.Kind !=
                    SyntheticFailureKind.Intermittent)
            {
                return true;
            }

            double period =
                NormalizePeriod(
                    failure.IntermittentPeriodSeconds);

            double duty =
                NormalizeDutyCycle(
                    failure.IntermittentDutyCycle);

            double elapsed =
                Math.Max(
                    0.0,
                    (nowUtc -
                     failure.ActivateUtc)
                    .TotalSeconds);

            double phase =
                elapsed %
                period;

            return
                phase <
                period * duty;
        }

        private void AddTransitionEvent(
            VesselFailureState state,
            SyntheticFailureRecord failure,
            SyntheticFailureEventKind kind,
            string detail)
        {
            SyntheticFailureEvent item =
                new SyntheticFailureEvent
                {
                    TimestampUtc =
                        failure.LastTransitionUtc,
                    EventKind = kind,
                    FailureId =
                        failure.FailureId,
                    VesselId =
                        failure.VesselId,
                    TargetId =
                        failure.TargetId,
                    Detail = detail
                };

            AddEvent(
                state,
                item);

            Debug.WriteLine(
                "KMC.Engine FAILURE EVENT" +
                " | VesselId=" +
                failure.VesselId +
                " | FailureId=" +
                failure.FailureId +
                " | Target=" +
                failure.TargetId +
                " | Kind=" +
                failure.Kind +
                " | Severity=" +
                failure.Severity +
                " | Event=" +
                kind +
                " | Detail=" +
                detail);
        }

        private static void AddRejectedEvent(
            VesselFailureState state,
            SyntheticFailureRequest request,
            string resultText)
        {
            AddEvent(
                state,
                new SyntheticFailureEvent
                {
                    TimestampUtc =
                        DateTime.UtcNow,
                    EventKind =
                        SyntheticFailureEventKind.Rejected,
                    VesselId =
                        request.VesselId ?? string.Empty,
                    TargetId =
                        request.TargetId ?? string.Empty,
                    Detail =
                        resultText ?? string.Empty
                });
        }

        private static void AddEvent(
            VesselFailureState state,
            SyntheticFailureEvent item)
        {
            if (state == null ||
                item == null)
            {
                return;
            }

            state.Events.Add(
                item);

            while (state.Events.Count >
                   MaximumEventHistory)
            {
                state.Events.RemoveAt(0);
            }
        }

        private VesselFailureState GetOrCreateState(
            string vesselId)
        {
            VesselFailureState state;

            if (!_byVessel.TryGetValue(
                    vesselId,
                    out state))
            {
                state =
                    new VesselFailureState
                    {
                        VesselId = vesselId,
                        Mode =
                            FailureSimulationMode.Nominal
                    };

                _byVessel[vesselId] =
                    state;
            }

            return state;
        }

        private static SyntheticFailureRecord FindFailure(
            VesselFailureState state,
            string failureId)
        {
            if (state == null ||
                string.IsNullOrWhiteSpace(failureId))
            {
                return null;
            }

            for (int index = 0;
                 index < state.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    state.Failures[index];

                if (failure != null &&
                    string.Equals(
                        failure.FailureId,
                        failureId,
                        StringComparison.Ordinal))
                {
                    return failure;
                }
            }

            return null;
        }

        private static SpacecraftSystemHealth MoreSevereHealth(
            SpacecraftSystemHealth left,
            SpacecraftSystemHealth right)
        {
            return
                (int)right >
                (int)left
                    ? right
                    : left;
        }

        private static double NormalizePeriod(
            double value)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return 4.0;
            }

            return
                Math.Max(
                    0.5,
                    value);
        }

        private static double NormalizeDutyCycle(
            double value)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value))
            {
                return 0.50;
            }

            return
                Math.Max(
                    0.05,
                    Math.Min(
                        0.95,
                        value));
        }

        private static string DescribeFailure(
            SyntheticFailureRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            return
                record.Kind.ToString().ToUpperInvariant() +
                " " +
                record.TargetKind.ToString().ToUpperInvariant() +
                " " +
                record.TargetId +
                " " +
                record.ComponentHealth.ToString().ToUpperInvariant();
        }

        private static void WriteAck(
            string vesselId,
            string failureId,
            string targetId,
            string resultText)
        {
            Debug.WriteLine(
                "KMC.Engine FAILURE COMMAND ACK" +
                " | VesselId=" +
                (vesselId ?? string.Empty) +
                " | FailureId=" +
                (failureId ?? string.Empty) +
                " | Target=" +
                (targetId ?? string.Empty) +
                " | Result=" +
                (resultText ?? string.Empty));
        }

#if DEBUG
        private static void RunSelfTestOnce()
        {
            if (_selfTestCompleted)
            {
                return;
            }

            _selfTestCompleted = true;

            DateTime t0 =
                new DateTime(
                    2030,
                    1,
                    1,
                    0,
                    0,
                    0,
                    DateTimeKind.Utc);

            SyntheticFailureEngine engine =
                new SyntheticFailureEngine();

            string text;
            engine.SetMode(
                "SELFTEST",
                FailureSimulationMode.Training,
                out text);

            string parentId;
            engine.Inject(
                new SyntheticFailureRequest
                {
                    VesselId = "SELFTEST",
                    TargetId = "GUID_B",
                    TargetKind =
                        SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Sudden,
                    Severity =
                        SyntheticFailureSeverity.Critical,
                    ComponentHealth =
                        SpacecraftSystemHealth.Failed,
                    ActivateUtc = t0
                },
                out parentId,
                out text);

            string degradeId;
            engine.Inject(
                new SyntheticFailureRequest
                {
                    VesselId = "SELFTEST",
                    TargetId = "COMM_A",
                    TargetKind =
                        SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Degrading,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        SpacecraftSystemHealth.Degraded,
                    ActivateUtc =
                        t0.AddSeconds(5.0)
                },
                out degradeId,
                out text);

            string intermittentId;
            engine.Inject(
                new SyntheticFailureRequest
                {
                    VesselId = "SELFTEST",
                    TargetId = "SENSOR_X",
                    TargetKind =
                        SyntheticFailureTargetKind.Instrumentation,
                    Kind =
                        SyntheticFailureKind.Intermittent,
                    Severity =
                        SyntheticFailureSeverity.Advisory,
                    ComponentHealth =
                        SpacecraftSystemHealth.Degraded,
                    ActivateUtc = t0,
                    IntermittentPeriodSeconds = 4.0,
                    IntermittentDutyCycle = 0.50
                },
                out intermittentId,
                out text);

            string cascadeId;
            engine.Inject(
                new SyntheticFailureRequest
                {
                    VesselId = "SELFTEST",
                    TargetId = "COMM_B",
                    TargetKind =
                        SyntheticFailureTargetKind.Component,
                    Kind =
                        SyntheticFailureKind.Cascade,
                    Severity =
                        SyntheticFailureSeverity.Caution,
                    ComponentHealth =
                        SpacecraftSystemHealth.Failed,
                    ActivateUtc = t0,
                    ParentFailureId = parentId
                },
                out cascadeId,
                out text);

            FailureSimulationSnapshot early =
                engine.GetSnapshot(
                    "SELFTEST",
                    t0.AddSeconds(1.0));

            FailureSimulationSnapshot intermittentRecovery =
                engine.GetSnapshot(
                    "SELFTEST",
                    t0.AddSeconds(3.0));

            FailureSimulationSnapshot late =
                engine.GetSnapshot(
                    "SELFTEST",
                    t0.AddSeconds(6.0));

            bool sudden =
                IsEffective(
                    early,
                    parentId);

            bool degradingWait =
                !IsEffective(
                    early,
                    degradeId);

            bool degradingActive =
                IsEffective(
                    late,
                    degradeId);

            bool intermittentOn =
                IsEffective(
                    early,
                    intermittentId);

            bool intermittentOff =
                !IsEffective(
                    intermittentRecovery,
                    intermittentId);

            bool cascade =
                IsEffective(
                    early,
                    cascadeId);

            bool pass =
                sudden &&
                degradingWait &&
                degradingActive &&
                intermittentOn &&
                intermittentOff &&
                cascade;

            Debug.WriteLine(
                "KMC.Engine FAILURE ENGINE SELFTEST" +
                " | " +
                (pass ? "PASS" : "FAIL") +
                " | MODE=Training" +
                " | SUDDEN=" +
                (sudden ? "ACTIVE" : "FAIL") +
                " | SCHEDULE=" +
                (degradingWait &&
                 degradingActive
                    ? "WAIT->ACTIVE"
                    : "FAIL") +
                " | INTERMITTENT=" +
                (intermittentOn &&
                 intermittentOff
                    ? "ACTIVE->RECOVERED"
                    : "FAIL") +
                " | CASCADE=" +
                (cascade ? "ACTIVE" : "FAIL") +
                " | EVENTS=" +
                late.Events.Count.ToString());

            Debug.Assert(
                pass,
                "Build 14.3 failure engine self-test failed.");
        }

        private static bool IsEffective(
            FailureSimulationSnapshot snapshot,
            string failureId)
        {
            if (snapshot == null)
            {
                return false;
            }

            for (int index = 0;
                 index < snapshot.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    snapshot.Failures[index];

                if (failure != null &&
                    string.Equals(
                        failure.FailureId,
                        failureId,
                        StringComparison.Ordinal))
                {
                    return
                        failure.EffectiveNow;
                }
            }

            return false;
        }
#endif

        private sealed class VesselFailureState
        {
            public VesselFailureState()
            {
                VesselId = string.Empty;
                Mode = FailureSimulationMode.Nominal;
                Failures =
                    new List<SyntheticFailureRecord>();
                Events =
                    new List<SyntheticFailureEvent>();
            }

            public string VesselId { get; set; }
            public FailureSimulationMode Mode { get; set; }
            public List<SyntheticFailureRecord> Failures { get; private set; }
            public List<SyntheticFailureEvent> Events { get; private set; }
        }
    }
}
