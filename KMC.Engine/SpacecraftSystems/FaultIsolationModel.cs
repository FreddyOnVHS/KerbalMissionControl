using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    public enum FaultIsolationSeverity
    {
        Advisory = 0,
        Caution = 1,
        Critical = 2
    }

    public enum FaultIsolationConfidence
    {
        Limited = 0,
        Probable = 1,
        High = 2
    }

    public sealed class FaultIsolationCase
    {
        public FaultIsolationCase()
        {
            CaseId = string.Empty;
            Subsystem = string.Empty;
            Condition = string.Empty;
            Isolation = string.Empty;
            ImmediateAction = string.Empty;
            Verification = string.Empty;
            RecoveryObjective = string.Empty;
            Severity = FaultIsolationSeverity.Advisory;
            Confidence = FaultIsolationConfidence.Limited;
            Active = true;
        }

        public string CaseId { get; internal set; }
        public string Subsystem { get; internal set; }
        public string Condition { get; internal set; }
        public string Isolation { get; internal set; }
        public string ImmediateAction { get; internal set; }
        public string Verification { get; internal set; }
        public string RecoveryObjective { get; internal set; }
        public FaultIsolationSeverity Severity { get; internal set; }
        public FaultIsolationConfidence Confidence { get; internal set; }
        public bool Active { get; internal set; }
    }

    public sealed class FaultIsolationSnapshot
    {
        private readonly List<FaultIsolationCase> _cases;

        public FaultIsolationSnapshot()
        {
            GeneratedUtc = DateTime.MinValue;
            Summary = "NO ACTIVE ISOLATION CASES";
            _cases = new List<FaultIsolationCase>();
        }

        public DateTime GeneratedUtc { get; internal set; }
        public string Summary { get; internal set; }

        public IList<FaultIsolationCase> Cases
        {
            get { return _cases; }
        }

        public int ActiveCaseCount
        {
            get { return _cases.Count; }
        }
    }

    /// <summary>
    /// Build 14.8 crew-facing procedure / fault-isolation layer.
    ///
    /// This analyzer does not clear failures and does not mutate KSP.
    /// It derives crew guidance from the Engine-owned systems graph plus
    /// explicit real-effect failure records where no synthetic component
    /// currently represents the physical effect.
    /// </summary>
    public static class FaultIsolationAnalyzer
    {
        public static FaultIsolationSnapshot Build(
            SpacecraftSystemsModel systems)
        {
            FaultIsolationSnapshot snapshot =
                new FaultIsolationSnapshot
                {
                    GeneratedUtc =
                        systems != null
                            ? systems.GeneratedUtc
                            : DateTime.UtcNow
                };

            if (systems == null)
            {
                snapshot.Summary =
                    "SYSTEMS SNAPSHOT UNAVAILABLE";
                return snapshot;
            }

            AddBusCases(
                systems,
                snapshot);

            AddRedundantPairCase(
                systems,
                snapshot,
                "GUID_A",
                "GUID_B",
                "GNC",
                "GUIDANCE COMPUTER REDUNDANCY",
                "Maintain the surviving guidance channel. Verify the affected main-bus path before crew reconfiguration.",
                "Confirm one guidance computer remains ONLINE and PRIMARY FLIGHT COMPUTER remains available.",
                "Restore dual-channel guidance availability.");

            AddRedundantPairCase(
                systems,
                snapshot,
                "COMM_A",
                "COMM_B",
                "COMM",
                "COMMUNICATIONS REDUNDANCY",
                "Maintain the surviving communications channel. Verify the affected main-bus path before switching or shedding equipment.",
                "Confirm one COMM channel remains ONLINE and its supplying bus is stable.",
                "Restore dual-channel communications availability.");

            AddRedundantPairCase(
                systems,
                snapshot,
                "PUMP_A",
                "PUMP_B",
                "PROP",
                "PROP FEED REDUNDANCY",
                "Preserve the available feed path. Avoid unnecessary propulsion configuration changes until the failed side is isolated.",
                "Verify at least one PROP FEED PUMP remains ONLINE and review PROP feed status.",
                "Restore redundant propellant-feed capability.");

            AddSingleComponentCase(
                systems,
                snapshot,
                "FLIGHT_COMPUTER",
                "GNC",
                "PRIMARY FLIGHT COMPUTER",
                "Hold the current safe vehicle configuration. Do not initiate new maneuver execution until computer availability is confirmed.",
                "Confirm PRIMARY FLIGHT COMPUTER state and ESSENTIAL BUS state.",
                "Restore a stable flight-computer power/data path.");

            AddRealEffectCases(
                systems.FailureSimulation,
                snapshot);

            SortCases(snapshot);

            if (snapshot.Cases.Count == 0)
            {
                snapshot.Summary =
                    "NO ACTIVE ISOLATION CASES / SPACECRAFT SYSTEMS NOMINAL";
            }
            else
            {
                FaultIsolationCase primary =
                    snapshot.Cases[0];

                snapshot.Summary =
                    primary.Severity.ToString().ToUpperInvariant() +
                    " / " +
                    primary.Subsystem +
                    " / " +
                    primary.Condition;
            }

            return snapshot;
        }

        private static void AddBusCases(
            SpacecraftSystemsModel systems,
            FaultIsolationSnapshot snapshot)
        {
            AddBusCase(
                systems,
                snapshot,
                "BUS_MAIN_A",
                "MAIN BUS A");

            AddBusCase(
                systems,
                snapshot,
                "BUS_MAIN_B",
                "MAIN BUS B");

            AddBusCase(
                systems,
                snapshot,
                "BUS_ESS",
                "ESSENTIAL BUS");
        }

        private static void AddBusCase(
            SpacecraftSystemsModel systems,
            FaultIsolationSnapshot snapshot,
            string id,
            string display)
        {
            SpacecraftSystemComponent component =
                systems.FindComponent(id);

            if (IsNominal(component))
            {
                return;
            }

            FaultIsolationSeverity severity =
                id == "BUS_ESS"
                    ? FaultIsolationSeverity.Critical
                    : FaultIsolationSeverity.Caution;

            snapshot.Cases.Add(
                new FaultIsolationCase
                {
                    CaseId = "FI-" + id,
                    Subsystem = "POWER",
                    Condition =
                        display + " " +
                        GetState(component),
                    Isolation =
                        "Electrical distribution path is not nominal. Downstream equipment on this bus may be degraded or unpowered.",
                    ImmediateAction =
                        "Verify source availability and bus controls. Preserve ESSENTIAL BUS supply and shed nonessential loads if required.",
                    Verification =
                        "Confirm bus state returns ONLINE/NOMINAL and dependent equipment recovers.",
                    RecoveryObjective =
                        "Restore a stable powered distribution path without masking downstream faults.",
                    Severity = severity,
                    Confidence =
                        FaultIsolationConfidence.High,
                    Active = true
                });
        }

        private static void AddRedundantPairCase(
            SpacecraftSystemsModel systems,
            FaultIsolationSnapshot snapshot,
            string firstId,
            string secondId,
            string subsystem,
            string label,
            string singleAction,
            string verification,
            string recovery)
        {
            SpacecraftSystemComponent first =
                systems.FindComponent(firstId);

            SpacecraftSystemComponent second =
                systems.FindComponent(secondId);

            bool firstGood = IsUsable(first);
            bool secondGood = IsUsable(second);

            if (firstGood && secondGood)
            {
                return;
            }

            bool bothLost =
                !firstGood &&
                !secondGood;

            string condition =
                bothLost
                    ? label + " LOST"
                    : label + " DEGRADED / SINGLE CHANNEL";

            string isolation =
                firstId + "=" + GetState(first) +
                " / " +
                secondId + "=" + GetState(second);

            snapshot.Cases.Add(
                new FaultIsolationCase
                {
                    CaseId =
                        "FI-" +
                        firstId + "-" +
                        secondId,
                    Subsystem = subsystem,
                    Condition = condition,
                    Isolation = isolation,
                    ImmediateAction =
                        bothLost
                            ? "Hold the safest current vehicle configuration. Restore one independent channel before continuing dependent operations."
                            : singleAction,
                    Verification = verification,
                    RecoveryObjective = recovery,
                    Severity =
                        bothLost
                            ? FaultIsolationSeverity.Critical
                            : FaultIsolationSeverity.Caution,
                    Confidence =
                        FaultIsolationConfidence.High,
                    Active = true
                });
        }

        private static void AddSingleComponentCase(
            SpacecraftSystemsModel systems,
            FaultIsolationSnapshot snapshot,
            string id,
            string subsystem,
            string label,
            string action,
            string verification,
            string recovery)
        {
            SpacecraftSystemComponent component =
                systems.FindComponent(id);

            if (IsUsable(component))
            {
                return;
            }

            snapshot.Cases.Add(
                new FaultIsolationCase
                {
                    CaseId = "FI-" + id,
                    Subsystem = subsystem,
                    Condition =
                        label + " " +
                        GetState(component),
                    Isolation =
                        id + "=" +
                        GetState(component),
                    ImmediateAction = action,
                    Verification = verification,
                    RecoveryObjective = recovery,
                    Severity =
                        FaultIsolationSeverity.Critical,
                    Confidence =
                        FaultIsolationConfidence.High,
                    Active = true
                });
        }

        private static void AddRealEffectCases(
            FailureSimulationSnapshot failures,
            FaultIsolationSnapshot snapshot)
        {
            if (failures == null)
            {
                return;
            }

            for (int index = 0;
                 index < failures.Failures.Count;
                 index++)
            {
                SyntheticFailureRecord failure =
                    failures.Failures[index];

                if (failure == null ||
                    !failure.EffectiveNow)
                {
                    continue;
                }

                if (failure.TargetKind ==
                    SyntheticFailureTargetKind.PowerEffect)
                {
                    AddEffectCase(
                        snapshot,
                        failure,
                        "POWER",
                        "UNCOMMANDED ELECTRICAL LOAD / STORAGE DISCHARGE",
                        "A vehicle-wide electrical load effect is active; use live POWER flow as the observable verification source.",
                        "Reduce nonessential load and restore generation if available. Do not treat load shedding as repair of the underlying fault.",
                        "Verify POWER net storage rate stabilizes or becomes positive.",
                        "Remove the electrical deficit and restore reserve margin.");
                }
                else if (failure.TargetKind ==
                         SyntheticFailureTargetKind.PropulsionEffect)
                {
                    bool shutdown =
                        failure.TargetId != null &&
                        failure.TargetId.StartsWith(
                            SyntheticFailureTargets.EngineShutdownPrefix,
                            StringComparison.Ordinal);

                    AddEffectCase(
                        snapshot,
                        failure,
                        "PROP",
                        shutdown
                            ? "ENGINE SHUTDOWN / THRUST CAPABILITY LOSS"
                            : "ENGINE THRUST AUTHORITY DEGRADED",
                        "The affected engine identity is fixed by the failure target. PROP telemetry remains authoritative for actual thrust state.",
                        "Maintain required vehicle control and avoid commanding a maneuver that assumes unavailable thrust.",
                        "Verify affected engine state and total available thrust on PROP.",
                        "Recover the planned thrust capability or replan around the reduced capability.");
                }
                else if (failure.TargetKind ==
                         SyntheticFailureTargetKind.GuidanceEffect)
                {
                    AddEffectCase(
                        snapshot,
                        failure,
                        "GNC",
                        "ATTITUDE CONTROL AUTHORITY DEGRADED",
                        "Reaction-wheel authority is reduced on an exact vehicle part; attitude telemetry remains live.",
                        "Reduce aggressive attitude demands and preserve alternate control authority such as remaining wheels or RCS.",
                        "Verify vehicle response and confirm authority returns after recovery.",
                        "Restore adequate attitude-control margin before precision guidance.");
                }
                else if (failure.TargetKind ==
                         SyntheticFailureTargetKind.Instrumentation)
                {
                    AddEffectCase(
                        snapshot,
                        failure,
                        "INSTR",
                        "INSTRUMENTATION DATA SUSPECT",
                        "An instrumentation fault is represented; do not substitute invented values for unavailable or suspect data.",
                        "Cross-check independent sensors/telemetry sources and hold actions that depend on the suspect measurement.",
                        "Verify agreement using an independent data source.",
                        "Re-establish trusted measurement quality.");
                }
            }
        }

        private static void AddEffectCase(
            FaultIsolationSnapshot snapshot,
            SyntheticFailureRecord failure,
            string subsystem,
            string condition,
            string isolation,
            string action,
            string verification,
            string recovery)
        {
            snapshot.Cases.Add(
                new FaultIsolationCase
                {
                    CaseId =
                        "FI-" +
                        (failure.FailureId ??
                         failure.TargetId ??
                         subsystem),
                    Subsystem = subsystem,
                    Condition = condition,
                    Isolation = isolation,
                    ImmediateAction = action,
                    Verification = verification,
                    RecoveryObjective = recovery,
                    Severity =
                        ConvertSeverity(
                            failure.Severity),
                    Confidence =
                        FaultIsolationConfidence.High,
                    Active = true
                });
        }

        private static FaultIsolationSeverity ConvertSeverity(
            SyntheticFailureSeverity severity)
        {
            switch (severity)
            {
                case SyntheticFailureSeverity.Critical:
                    return
                        FaultIsolationSeverity.Critical;

                case SyntheticFailureSeverity.Caution:
                    return
                        FaultIsolationSeverity.Caution;

                default:
                    return
                        FaultIsolationSeverity.Advisory;
            }
        }

        private static bool IsNominal(
            SpacecraftSystemComponent component)
        {
            return
                component != null &&
                component.State ==
                    SpacecraftSystemState.Online &&
                component.Health ==
                    SpacecraftSystemHealth.Nominal;
        }

        private static bool IsUsable(
            SpacecraftSystemComponent component)
        {
            return
                component != null &&
                (component.State ==
                     SpacecraftSystemState.Online ||
                 component.State ==
                     SpacecraftSystemState.Degraded);
        }

        private static string GetState(
            SpacecraftSystemComponent component)
        {
            return
                component != null
                    ? component.State.ToString().ToUpperInvariant()
                    : "UNAVAILABLE";
        }

        private static void SortCases(
            FaultIsolationSnapshot snapshot)
        {
            List<FaultIsolationCase> sorted =
                new List<FaultIsolationCase>(
                    snapshot.Cases);

            sorted.Sort(
                delegate(
                    FaultIsolationCase left,
                    FaultIsolationCase right)
                {
                    int severity =
                        right.Severity.CompareTo(
                            left.Severity);

                    if (severity != 0)
                    {
                        return severity;
                    }

                    return
                        string.Compare(
                            left.Subsystem,
                            right.Subsystem,
                            StringComparison.Ordinal);
                });

            snapshot.Cases.Clear();

            for (int index = 0;
                 index < sorted.Count;
                 index++)
            {
                snapshot.Cases.Add(
                    sorted[index]);
            }
        }
    }
}
