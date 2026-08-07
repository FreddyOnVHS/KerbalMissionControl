using System;

namespace KMC.Engine.Electrical
{
    public enum ElectricalPowerSeverity
    {
        Unknown = 0,
        Normal,
        Advisory,
        Warning,
        Critical,
        Blackout
    }

    public enum ElectricalPowerCondition
    {
        Unknown = 0,
        TelemetryUnavailable,
        DataIncomplete,
        Nominal,
        Charging,
        Discharging,
        LowReserve,
        CriticalReserve,
        ImminentDepletion,
        StageStorageHazard,
        Depleted
    }

    public enum ElectricalDemandObservability
    {
        Unknown = 0,
        Observable,
        UnobservableAtDepletion
    }

    /// <summary>
    /// Engineering interpretation of the lower-level electrical models.
    ///
    /// This model does not replace raw telemetry or storage/load models.
    /// It provides a concise flight-controller status derived from them.
    /// </summary>
    public sealed class ElectricalPowerDiagnosticModel
    {
        public ElectricalPowerDiagnosticModel()
        {
            Severity =
                ElectricalPowerSeverity.Unknown;

            Condition =
                ElectricalPowerCondition.Unknown;

            DemandObservability =
                ElectricalDemandObservability.Unknown;

            Summary =
                "Power status unavailable.";
        }

        public ElectricalPowerSeverity Severity { get; internal set; }

        public ElectricalPowerCondition Condition { get; internal set; }

        public ElectricalDemandObservability DemandObservability
        {
            get;
            internal set;
        }

        public string Summary { get; internal set; }

        public bool TelemetryAvailable { get; internal set; }

        public double StoredEc { get; internal set; }

        public double CapacityEc { get; internal set; }

        public double ReservePercent { get; internal set; }

        public bool IsCharging { get; internal set; }

        public bool IsDischarging { get; internal set; }

        public bool IsDepleted { get; internal set; }

        public bool HasEndurance { get; internal set; }

        public double EnduranceSeconds { get; internal set; }

        public bool HasPowerMargin { get; internal set; }

        /// <summary>
        /// Net storage margin while storage flow is observable.
        /// Positive = charging surplus. Negative = battery-supported deficit.
        /// </summary>
        public double PowerMarginEcPerSecond { get; internal set; }

        public bool HasInferredDemand { get; internal set; }

        public double InferredDemandEcPerSecond { get; internal set; }

        public bool GenerationComplete { get; internal set; }

        public double GenerationEcPerSecond { get; internal set; }

        public bool AttributionTelemetryAvailable { get; internal set; }

        public bool AttributionCoverageKnown { get; internal set; }

        public double AttributionCoveragePercent { get; internal set; }

        public bool NextStageLosesStorage { get; internal set; }

        public bool NextStageLosesAllStorage { get; internal set; }

        public double NextStageLostStoredEc { get; internal set; }

        public double NextStageLostCapacityEc { get; internal set; }

        public double NextStageRemainingStoredEc { get; internal set; }

        public double NextStageRemainingCapacityEc { get; internal set; }

        public double NextStageRemainingReservePercent { get; internal set; }
    }

    internal static class ElectricalPowerDiagnosticAnalyzer
    {
        private const double CriticalEnduranceSeconds = 30.0;
        private const double WarningEnduranceSeconds = 120.0;
        private const double AdvisoryEnduranceSeconds = 300.0;

        private const double CriticalReservePercent = 5.0;
        private const double WarningReservePercent = 15.0;
        private const double AdvisoryReservePercent = 30.0;

        public static ElectricalPowerDiagnosticModel Analyze(
            ElectricalNetwork network,
            ElectricalFlowModel flow,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution)
        {
            ElectricalPowerDiagnosticModel model =
                new ElectricalPowerDiagnosticModel();

            ElectricalStorageModel storage =
                network != null
                    ? network.Storage
                    : null;

            if (flow == null ||
                !flow.TelemetryAvailable)
            {
                model.Condition =
                    ElectricalPowerCondition.TelemetryUnavailable;

                model.Severity =
                    ElectricalPowerSeverity.Unknown;

                model.Summary =
                    "Live electrical telemetry unavailable.";

                PopulateStageRisk(
                    model,
                    storage);

                return model;
            }

            model.TelemetryAvailable =
                true;

            model.StoredEc =
                Math.Max(
                    0.0,
                    flow.StoredEc);

            model.CapacityEc =
                Math.Max(
                    0.0,
                    flow.CapacityEc);

            model.ReservePercent =
                flow.ChargePercent;

            model.IsCharging =
                flow.State ==
                ElectricalStorageFlowState.Charging;

            model.IsDischarging =
                flow.State ==
                ElectricalStorageFlowState.Discharging;

            model.IsDepleted =
                flow.State ==
                ElectricalStorageFlowState.Depleted;

            model.HasEndurance =
                flow.HasEstimatedSecondsToEmpty;

            model.EnduranceSeconds =
                flow.HasEstimatedSecondsToEmpty
                    ? Math.Max(
                        0.0,
                        flow.EstimatedSecondsToEmpty)
                    : 0.0;

            model.HasPowerMargin =
                flow.HasMeasuredNetStorageRate;

            model.PowerMarginEcPerSecond =
                flow.HasMeasuredNetStorageRate
                    ? flow.NetStorageRateEcPerSecond
                    : 0.0;

            if (load != null)
            {
                model.HasInferredDemand =
                    load.HasInferredTotalLoad;

                model.InferredDemandEcPerSecond =
                    load.HasInferredTotalLoad
                        ? Math.Max(
                            0.0,
                            load.InferredTotalLoadEcPerSecond)
                        : 0.0;

                model.GenerationComplete =
                    load.GenerationRateComplete;

                model.GenerationEcPerSecond =
                    load.GenerationRateComplete
                        ? Math.Max(
                            0.0,
                            load.GenerationEcPerSecond)
                        : 0.0;

                if (load.State ==
                    ElectricalLoadInferenceState.StorageDepleted)
                {
                    model.DemandObservability =
                        ElectricalDemandObservability
                            .UnobservableAtDepletion;
                }
                else if (load.HasInferredTotalLoad)
                {
                    model.DemandObservability =
                        ElectricalDemandObservability.Observable;
                }
            }

            if (attribution != null)
            {
                model.AttributionTelemetryAvailable =
                    attribution.TelemetryAvailable;

                if (load != null &&
                    load.HasInferredTotalLoad)
                {
                    model.AttributionCoverageKnown =
                        true;

                    model.AttributionCoveragePercent =
                        load.AttributionCoveragePercent;
                }
            }

            PopulateStageRisk(
                model,
                storage);

            if (model.IsDepleted)
            {
                model.Severity =
                    ElectricalPowerSeverity.Blackout;

                model.Condition =
                    ElectricalPowerCondition.Depleted;

                model.Summary =
                    "Electrical storage depleted; demand is no longer observable from storage flow.";

                return model;
            }

            if (model.NextStageLosesAllStorage)
            {
                model.Severity =
                    ElectricalPowerSeverity.Critical;

                model.Condition =
                    ElectricalPowerCondition.StageStorageHazard;

                model.Summary =
                    "Next stage removes all known electrical storage.";

                return model;
            }

            if (model.IsDischarging &&
                model.HasEndurance)
            {
                if (model.EnduranceSeconds <=
                    CriticalEnduranceSeconds)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Critical;

                    model.Condition =
                        ElectricalPowerCondition.ImminentDepletion;

                    model.Summary =
                        "Electrical depletion imminent.";

                    return model;
                }

                if (model.EnduranceSeconds <=
                    WarningEnduranceSeconds)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Warning;

                    model.Condition =
                        ElectricalPowerCondition.ImminentDepletion;

                    model.Summary =
                        "Electrical endurance is below two minutes.";

                    return model;
                }

                if (model.EnduranceSeconds <=
                    AdvisoryEnduranceSeconds)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Advisory;

                    model.Condition =
                        ElectricalPowerCondition.Discharging;

                    model.Summary =
                        "Electrical endurance is below five minutes.";

                    return model;
                }
            }

            if (model.CapacityEc > 0.000001)
            {
                if (model.ReservePercent <=
                    CriticalReservePercent)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Critical;

                    model.Condition =
                        ElectricalPowerCondition.CriticalReserve;

                    model.Summary =
                        "Electrical reserve is critically low.";

                    return model;
                }

                if (model.ReservePercent <=
                    WarningReservePercent)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Warning;

                    model.Condition =
                        ElectricalPowerCondition.LowReserve;

                    model.Summary =
                        "Electrical reserve is low.";

                    return model;
                }

                if (model.ReservePercent <=
                    AdvisoryReservePercent)
                {
                    model.Severity =
                        ElectricalPowerSeverity.Advisory;

                    model.Condition =
                        ElectricalPowerCondition.LowReserve;

                    model.Summary =
                        "Electrical reserve is below 30 percent.";

                    return model;
                }
            }

            if (model.NextStageLosesStorage)
            {
                model.Severity =
                    ElectricalPowerSeverity.Advisory;

                model.Condition =
                    ElectricalPowerCondition.StageStorageHazard;

                model.Summary =
                    "Next stage removes part of the electrical storage system.";

                return model;
            }

            if (flow.State ==
                ElectricalStorageFlowState.InsufficientData ||
                (load != null &&
                 (load.State ==
                      ElectricalLoadInferenceState.WaitingForFlow ||
                  load.State ==
                      ElectricalLoadInferenceState.GenerationIncomplete)))
            {
                model.Severity =
                    ElectricalPowerSeverity.Advisory;

                model.Condition =
                    ElectricalPowerCondition.DataIncomplete;

                model.Summary =
                    "Electrical status is live but engineering data is incomplete.";

                return model;
            }

            if (model.IsCharging)
            {
                model.Severity =
                    ElectricalPowerSeverity.Normal;

                model.Condition =
                    ElectricalPowerCondition.Charging;

                model.Summary =
                    "Electrical storage is charging.";

                return model;
            }

            if (model.IsDischarging)
            {
                model.Severity =
                    ElectricalPowerSeverity.Normal;

                model.Condition =
                    ElectricalPowerCondition.Discharging;

                model.Summary =
                    "Electrical storage is discharging with acceptable reserve.";

                return model;
            }

            model.Severity =
                ElectricalPowerSeverity.Normal;

            model.Condition =
                ElectricalPowerCondition.Nominal;

            model.Summary =
                "Electrical system nominal.";

            return model;
        }

        private static void PopulateStageRisk(
            ElectricalPowerDiagnosticModel model,
            ElectricalStorageModel storage)
        {
            if (model == null ||
                storage == null)
            {
                return;
            }

            model.NextStageLosesStorage =
                storage.HasStorageLossOnNextStage;

            model.NextStageLosesAllStorage =
                storage.LosesAllStorageOnNextStage;

            model.NextStageLostStoredEc =
                storage.NextStageLostStoredEc;

            model.NextStageLostCapacityEc =
                storage.NextStageLostCapacityEc;

            model.NextStageRemainingStoredEc =
                storage.NextStageRemainingStoredEc;

            model.NextStageRemainingCapacityEc =
                storage.NextStageRemainingCapacityEc;

            model.NextStageRemainingReservePercent =
                storage.NextStageRemainingChargePercent;
        }
    }

}
