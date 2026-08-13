using System;
using System.Collections.Generic;

namespace KMC.Engine.SpacecraftSystems
{
    public enum IntegratedAlertSeverity
    {
        Normal = 0,
        Advisory = 1,
        Caution = 2,
        Warning = 3
    }

    public sealed class IntegratedAlertItem
    {
        public IntegratedAlertItem()
        {
            AlertId = string.Empty;
            Subsystem = string.Empty;
            Summary = string.Empty;
            Detail = string.Empty;
            Severity = IntegratedAlertSeverity.Normal;
        }

        public string AlertId { get; internal set; }
        public string Subsystem { get; internal set; }
        public string Summary { get; internal set; }
        public string Detail { get; internal set; }
        public IntegratedAlertSeverity Severity { get; internal set; }
    }

    public sealed class IntegratedCautionWarningSnapshot
    {
        private readonly List<IntegratedAlertItem> _alerts;

        public IntegratedCautionWarningSnapshot()
        {
            GeneratedUtc = DateTime.MinValue;
            VesselId = string.Empty;
            VesselName = string.Empty;
            HighestSeverity = IntegratedAlertSeverity.Normal;
            Summary = "NOMINAL / NO ACTIVE SYSTEM CAUTIONS";
            _alerts = new List<IntegratedAlertItem>();
        }

        public DateTime GeneratedUtc { get; internal set; }
        public string VesselId { get; internal set; }
        public string VesselName { get; internal set; }
        public IntegratedAlertSeverity HighestSeverity { get; internal set; }
        public string Summary { get; internal set; }

        public IList<IntegratedAlertItem> Alerts
        {
            get { return _alerts; }
        }

        public int WarningCount
        {
            get { return CountSeverity(IntegratedAlertSeverity.Warning); }
        }

        public int CautionCount
        {
            get { return CountSeverity(IntegratedAlertSeverity.Caution); }
        }

        public int AdvisoryCount
        {
            get { return CountSeverity(IntegratedAlertSeverity.Advisory); }
        }

        public IntegratedAlertItem PrimaryAlert
        {
            get
            {
                if (_alerts.Count == 0)
                {
                    return null;
                }

                return _alerts[0];
            }
        }

        public bool HasSubsystem(string subsystem)
        {
            if (string.IsNullOrWhiteSpace(subsystem))
            {
                return false;
            }

            for (int index = 0;
                 index < _alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    _alerts[index];

                if (item != null &&
                    string.Equals(
                        item.Subsystem,
                        subsystem,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public IntegratedAlertSeverity GetSubsystemSeverity(
            string subsystem)
        {
            IntegratedAlertSeverity result =
                IntegratedAlertSeverity.Normal;

            if (string.IsNullOrWhiteSpace(subsystem))
            {
                return result;
            }

            for (int index = 0;
                 index < _alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    _alerts[index];

                if (item == null ||
                    !string.Equals(
                        item.Subsystem,
                        subsystem,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (item.Severity > result)
                {
                    result =
                        item.Severity;
                }
            }

            return result;
        }

        private int CountSeverity(
            IntegratedAlertSeverity severity)
        {
            int count = 0;

            for (int index = 0;
                 index < _alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    _alerts[index];

                if (item != null &&
                    item.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// Build 14.10 mission-wide caution/warning derivation.
    ///
    /// The model consumes the Build 14.8 fault-isolation layer rather than
    /// inventing a second set of subsystem truth. Critical isolation cases
    /// become WARNING, caution cases become CAUTION, and advisory cases remain
    /// ADVISORY. UI acknowledgment never changes this model or clears failures.
    /// </summary>
    public static class IntegratedCautionWarningAnalyzer
    {
        public static IntegratedCautionWarningSnapshot Build(
            SpacecraftSystemsModel systems)
        {
            IntegratedCautionWarningSnapshot snapshot =
                new IntegratedCautionWarningSnapshot();

            if (systems == null)
            {
                snapshot.HighestSeverity =
                    IntegratedAlertSeverity.Advisory;

                snapshot.Summary =
                    "ADVISORY / SYSTEMS SNAPSHOT UNAVAILABLE";

                snapshot.Alerts.Add(
                    new IntegratedAlertItem
                    {
                        AlertId = "CW-SYSTEMS-UNAVAILABLE",
                        Subsystem = "DATA",
                        Summary = "SYSTEMS SNAPSHOT UNAVAILABLE",
                        Detail =
                            "Mission Control does not currently have an Engine-owned spacecraft systems snapshot.",
                        Severity =
                            IntegratedAlertSeverity.Advisory
                    });

                return snapshot;
            }

            snapshot.GeneratedUtc =
                systems.GeneratedUtc;

            snapshot.VesselId =
                systems.VesselId ??
                string.Empty;

            snapshot.VesselName =
                systems.VesselName ??
                string.Empty;

            FaultIsolationSnapshot isolation =
                FaultIsolationAnalyzer.Build(
                    systems);

            if (isolation != null)
            {
                for (int index = 0;
                     index < isolation.Cases.Count;
                     index++)
                {
                    FaultIsolationCase item =
                        isolation.Cases[index];

                    if (item == null ||
                        !item.Active)
                    {
                        continue;
                    }

                    snapshot.Alerts.Add(
                        new IntegratedAlertItem
                        {
                            AlertId =
                                "CW-" +
                                (item.CaseId ??
                                 index.ToString()),
                            Subsystem =
                                NormalizeSubsystem(
                                    item.Subsystem),
                            Summary =
                                item.Condition ??
                                string.Empty,
                            Detail =
                                item.Isolation ??
                                string.Empty,
                            Severity =
                                ConvertSeverity(
                                    item.Severity)
                        });
                }
            }

            AddUnrepresentedActiveFailures(
                systems.FailureSimulation,
                snapshot);

            SortAlerts(
                snapshot);

            if (snapshot.Alerts.Count == 0)
            {
                snapshot.HighestSeverity =
                    IntegratedAlertSeverity.Normal;

                snapshot.Summary =
                    "NOMINAL / NO ACTIVE SYSTEM CAUTIONS";

                return snapshot;
            }

            snapshot.HighestSeverity =
                snapshot.Alerts[0].Severity;

            IntegratedAlertItem primary =
                snapshot.Alerts[0];

            snapshot.Summary =
                snapshot.HighestSeverity.ToString().ToUpperInvariant() +
                " / " +
                primary.Subsystem +
                " / " +
                primary.Summary;

            return snapshot;
        }

        private static void AddUnrepresentedActiveFailures(
            FailureSimulationSnapshot failures,
            IntegratedCautionWarningSnapshot snapshot)
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

                string expectedCaseId =
                    "CW-FI-" +
                    (failure.FailureId ??
                     string.Empty);

                if (ContainsAlert(
                        snapshot,
                        expectedCaseId))
                {
                    continue;
                }

                /*
                 * Component failures for the established redundant-system
                 * template are already represented by 14.8 pair/bus cases.
                 * Avoid duplicating those with a generic SYS alert.
                 */
                if (failure.TargetKind ==
                        SyntheticFailureTargetKind.Component &&
                    IsKnownTemplateComponent(
                        failure.TargetId))
                {
                    continue;
                }

                string subsystem =
                    ResolveFailureSubsystem(
                        failure);

                snapshot.Alerts.Add(
                    new IntegratedAlertItem
                    {
                        AlertId =
                            "CW-FAIL-" +
                            (failure.FailureId ??
                             failure.TargetId ??
                             index.ToString()),
                        Subsystem = subsystem,
                        Summary =
                            "ACTIVE FAILURE / " +
                            (failure.TargetId ??
                             "UNKNOWN TARGET"),
                        Detail =
                            failure.Detail ??
                            string.Empty,
                        Severity =
                            ConvertSeverity(
                                failure.Severity)
                    });
            }
        }

        private static bool ContainsAlert(
            IntegratedCautionWarningSnapshot snapshot,
            string alertId)
        {
            for (int index = 0;
                 index < snapshot.Alerts.Count;
                 index++)
            {
                IntegratedAlertItem item =
                    snapshot.Alerts[index];

                if (item != null &&
                    string.Equals(
                        item.AlertId,
                        alertId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsKnownTemplateComponent(
            string targetId)
        {
            switch (targetId)
            {
                case "BUS_MAIN_A":
                case "BUS_MAIN_B":
                case "BUS_ESS":
                case "GUID_A":
                case "GUID_B":
                case "FLIGHT_COMPUTER":
                case "COMM_A":
                case "COMM_B":
                case "PUMP_A":
                case "PUMP_B":
                    return true;

                default:
                    return false;
            }
        }

        private static string ResolveFailureSubsystem(
            SyntheticFailureRecord failure)
        {
            if (failure == null)
            {
                return "SYS";
            }

            switch (failure.TargetKind)
            {
                case SyntheticFailureTargetKind.PowerEffect:
                case SyntheticFailureTargetKind.ElectricalSource:
                    return "POWER";

                case SyntheticFailureTargetKind.PropulsionEffect:
                    return "PROP";

                case SyntheticFailureTargetKind.GuidanceEffect:
                    return "GNC";

                case SyntheticFailureTargetKind.Instrumentation:
                    return "DATA";

                default:
                    return NormalizeSubsystem(
                        failure.TargetId);
            }
        }

        private static string NormalizeSubsystem(
            string subsystem)
        {
            if (string.IsNullOrWhiteSpace(subsystem))
            {
                return "SYS";
            }

            string value =
                subsystem.Trim().ToUpperInvariant();

            if (value.StartsWith(
                    "COMM",
                    StringComparison.Ordinal))
            {
                return "COMM";
            }

            if (value.StartsWith(
                    "GUID",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "GNC",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "FLIGHT_COMPUTER",
                    StringComparison.Ordinal))
            {
                return "GNC";
            }

            if (value.StartsWith(
                    "PUMP",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "PROP",
                    StringComparison.Ordinal))
            {
                return "PROP";
            }

            if (value.StartsWith(
                    "BUS",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "POWER",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "PWR",
                    StringComparison.Ordinal))
            {
                return "POWER";
            }

            if (value.StartsWith(
                    "INSTR",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "DATA",
                    StringComparison.Ordinal) ||
                value.StartsWith(
                    "SENSOR",
                    StringComparison.Ordinal))
            {
                return "DATA";
            }

            return "SYS";
        }

        private static IntegratedAlertSeverity ConvertSeverity(
            FaultIsolationSeverity severity)
        {
            switch (severity)
            {
                case FaultIsolationSeverity.Critical:
                    return
                        IntegratedAlertSeverity.Warning;

                case FaultIsolationSeverity.Caution:
                    return
                        IntegratedAlertSeverity.Caution;

                default:
                    return
                        IntegratedAlertSeverity.Advisory;
            }
        }

        private static IntegratedAlertSeverity ConvertSeverity(
            SyntheticFailureSeverity severity)
        {
            switch (severity)
            {
                case SyntheticFailureSeverity.Critical:
                    return
                        IntegratedAlertSeverity.Warning;

                case SyntheticFailureSeverity.Caution:
                    return
                        IntegratedAlertSeverity.Caution;

                default:
                    return
                        IntegratedAlertSeverity.Advisory;
            }
        }

        private static void SortAlerts(
            IntegratedCautionWarningSnapshot snapshot)
        {
            List<IntegratedAlertItem> sorted =
                new List<IntegratedAlertItem>(
                    snapshot.Alerts);

            sorted.Sort(
                delegate(
                    IntegratedAlertItem left,
                    IntegratedAlertItem right)
                {
                    int severity =
                        right.Severity.CompareTo(
                            left.Severity);

                    if (severity != 0)
                    {
                        return severity;
                    }

                    int subsystem =
                        string.Compare(
                            left.Subsystem,
                            right.Subsystem,
                            StringComparison.Ordinal);

                    if (subsystem != 0)
                    {
                        return subsystem;
                    }

                    return
                        string.Compare(
                            left.AlertId,
                            right.AlertId,
                            StringComparison.Ordinal);
                });

            snapshot.Alerts.Clear();

            for (int index = 0;
                 index < sorted.Count;
                 index++)
            {
                snapshot.Alerts.Add(
                    sorted[index]);
            }
        }
    }
}
