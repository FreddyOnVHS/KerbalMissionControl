using System;

namespace KMC.Engine.Electrical
{
    public enum ElectricalProcedureState
    {
        Unavailable = 0,
        Monitor,
        ConservePower,
        ShedNonessentialLoad,
        ImmediateLoadReduction,
        RestoreGeneration,
        BlackoutRecovery,
        DataReview
    }

    public enum ElectricalRecoveryState
    {
        None = 0,
        BaselineEstablished,
        Improving,
        StableImprovement,
        DeficitCleared,
        Worsening,
        Unobservable
    }

    public enum ElectricalRecoveryConfidence
    {
        Unknown = 0,
        Limited,
        Moderate,
        High
    }

    public sealed class ElectricalProcedureModel
    {
        public ElectricalProcedureModel()
        {
            State =
                ElectricalProcedureState.Unavailable;

            RecoveryState =
                ElectricalRecoveryState.None;

            RecoveryConfidence =
                ElectricalRecoveryConfidence.Unknown;

            PrimaryAction =
                "No electrical procedure available.";

            Objective =
                string.Empty;

            Verification =
                string.Empty;
        }

        public ElectricalProcedureState State { get; internal set; }

        public ElectricalRecoveryState RecoveryState { get; internal set; }

        public ElectricalRecoveryConfidence RecoveryConfidence
        {
            get;
            internal set;
        }

        public string PrimaryAction { get; internal set; }

        public string Objective { get; internal set; }

        public string Verification { get; internal set; }

        public bool ActionRequired { get; internal set; }

        public bool LoadSheddingRecommended { get; internal set; }

        public bool GenerationRestorationRecommended { get; internal set; }

        public bool BlackoutRecoveryRequired { get; internal set; }

        public bool HasBaseline { get; internal set; }

        public double BaselineStorageRateEcPerSecond { get; internal set; }

        public double CurrentStorageRateEcPerSecond { get; internal set; }

        public bool CurrentStorageRateObservable { get; internal set; }

        public bool HasImprovement { get; internal set; }

        public double ImprovementEcPerSecond { get; internal set; }

        public bool DeficitCleared { get; internal set; }

        public bool HasEndurance { get; internal set; }

        public double EnduranceSeconds { get; internal set; }

        public double ReservePercent { get; internal set; }

        public ElectricalPowerSeverity Severity { get; internal set; }

        public ElectricalPowerCondition Condition { get; internal set; }

        public int ShedCandidateCount { get; internal set; }

        public int ProtectedConsumerCount { get; internal set; }

        public double QuantifiedRecoverableEcPerSecond { get; internal set; }

        public double PotentialMaximumRecoverableEcPerSecond { get; internal set; }

        public bool RecoveryIsObservedOnly { get; internal set; }
    }

    /// <summary>
    /// Stateful engineering procedure tracker. It does not command KSP.
    ///
    /// A baseline is established when an actionable electrical deficit begins.
    /// Later storage-rate changes are compared with that baseline. Improvement
    /// is an observed electrical response only; KMC does not claim causation
    /// unless a future command/event channel can prove a crew action occurred.
    /// </summary>
    internal sealed class ElectricalProcedureTracker
    {
        private const double ImprovementThresholdEcPerSecond = 0.05;
        private const double DeficitClearedToleranceEcPerSecond = 0.005;
        private const double StableImprovementSeconds = 2.0;

        private bool _hasBaseline;
        private double _baselineStorageRate;
        private DateTime _baselineUtc;
        private DateTime _improvementSinceUtc;
        private bool _hasImprovementSince;

        public ElectricalProcedureModel Analyze(
            DateTime receivedUtc,
            ElectricalFlowModel flow,
            ElectricalLoadModel load,
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalLoadSheddingModel shedding)
        {
            ElectricalProcedureModel model =
                new ElectricalProcedureModel();

            if (diagnostic == null)
            {
                model.State =
                    ElectricalProcedureState.Unavailable;

                return model;
            }

            model.Severity =
                diagnostic.Severity;

            model.Condition =
                diagnostic.Condition;

            model.ReservePercent =
                diagnostic.ReservePercent;

            model.HasEndurance =
                diagnostic.HasEndurance;

            model.EnduranceSeconds =
                diagnostic.EnduranceSeconds;

            if (shedding != null)
            {
                model.LoadSheddingRecommended =
                    shedding.SheddingRecommended;

                model.ShedCandidateCount =
                    shedding.CandidateCount;

                model.ProtectedConsumerCount =
                    shedding.ProtectedConsumerCount;

                model.QuantifiedRecoverableEcPerSecond =
                    shedding.QuantifiedRecoverableEcPerSecond;

                model.PotentialMaximumRecoverableEcPerSecond =
                    shedding.PotentialMaximumRecoverableEcPerSecond;
            }

            bool storageRateObservable =
                flow != null &&
                flow.HasMeasuredNetStorageRate;

            model.CurrentStorageRateObservable =
                storageRateObservable;

            if (storageRateObservable)
            {
                model.CurrentStorageRateEcPerSecond =
                    flow.NetStorageRateEcPerSecond;
            }

            if (diagnostic.Severity ==
                    ElectricalPowerSeverity.Blackout ||
                diagnostic.Condition ==
                    ElectricalPowerCondition.Depleted)
            {
                EnsureBaseline(
                    receivedUtc,
                    flow);

                model.State =
                    ElectricalProcedureState.BlackoutRecovery;

                model.ActionRequired =
                    true;

                model.BlackoutRecoveryRequired =
                    true;

                model.GenerationRestorationRecommended =
                    true;

                model.LoadSheddingRecommended =
                    true;

                model.PrimaryAction =
                    "Restore electrical generation and reduce nonessential load.";

                model.Objective =
                    "Recover stored ElectricCharge above the depletion boundary and re-establish an observable power balance.";

                model.RecoveryState =
                    ElectricalRecoveryState.Unobservable;

                model.RecoveryConfidence =
                    ElectricalRecoveryConfidence.Limited;

                model.Verification =
                    "Storage-flow demand is unobservable at depletion. Verify recovery only after EC rises above zero.";

                PopulateBaseline(
                    model);

                return model;
            }

            if (diagnostic.Condition ==
                ElectricalPowerCondition.DataIncomplete)
            {
                model.State =
                    ElectricalProcedureState.DataReview;

                model.PrimaryAction =
                    "Continue monitoring until the electrical estimator has sufficient data.";

                model.Objective =
                    "Establish a valid electrical flow and demand estimate.";

                model.RecoveryConfidence =
                    ElectricalRecoveryConfidence.Unknown;

                ResetBaselineIfHealthy(
                    diagnostic);

                return model;
            }

            if (diagnostic.Severity ==
                    ElectricalPowerSeverity.Critical ||
                (diagnostic.Severity ==
                     ElectricalPowerSeverity.Warning &&
                 diagnostic.Condition ==
                     ElectricalPowerCondition.ImminentDepletion))
            {
                EnsureBaseline(
                    receivedUtc,
                    flow);

                model.State =
                    ElectricalProcedureState.ImmediateLoadReduction;

                model.ActionRequired =
                    true;

                model.LoadSheddingRecommended =
                    true;

                model.GenerationRestorationRecommended =
                    true;

                model.PrimaryAction =
                    "Reduce nonessential electrical load immediately; restore generation if available.";

                model.Objective =
                    "Clear the electrical deficit and restore a stable or positive storage rate.";

                EvaluateRecovery(
                    receivedUtc,
                    flow,
                    model);

                SetRecoveryConfidence(
                    model,
                    shedding);

                return model;
            }

            if (diagnostic.Severity ==
                ElectricalPowerSeverity.Advisory)
            {
                EnsureBaseline(
                    receivedUtc,
                    flow);

                if (shedding != null &&
                    shedding.CandidateCount > 0)
                {
                    model.State =
                        ElectricalProcedureState.ShedNonessentialLoad;

                    model.PrimaryAction =
                        "Prepare to reduce nonessential electrical loads.";
                }
                else
                {
                    model.State =
                        ElectricalProcedureState.ConservePower;

                    model.PrimaryAction =
                        "Conserve electrical power and monitor endurance.";
                }

                model.ActionRequired =
                    true;

                model.Objective =
                    "Increase electrical endurance and prevent escalation to a critical deficit.";

                EvaluateRecovery(
                    receivedUtc,
                    flow,
                    model);

                SetRecoveryConfidence(
                    model,
                    shedding);

                return model;
            }

            if (diagnostic.Severity ==
                    ElectricalPowerSeverity.Normal &&
                diagnostic.Condition ==
                    ElectricalPowerCondition.Charging)
            {
                model.State =
                    ElectricalProcedureState.Monitor;

                model.PrimaryAction =
                    "Monitor electrical recovery.";

                model.Objective =
                    "Maintain positive storage rate until reserve is restored.";

                EvaluateRecovery(
                    receivedUtc,
                    flow,
                    model);

                SetRecoveryConfidence(
                    model,
                    shedding);

                if (model.DeficitCleared)
                {
                    model.RecoveryState =
                        ElectricalRecoveryState.DeficitCleared;

                    model.Verification =
                        "Observed storage flow is nonnegative; previous electrical deficit is cleared.";
                }

                return model;
            }

            model.State =
                ElectricalProcedureState.Monitor;

            model.PrimaryAction =
                "No immediate corrective action required.";

            model.Objective =
                "Monitor reserve, endurance, and storage-rate trend.";

            EvaluateRecovery(
                receivedUtc,
                flow,
                model);

            SetRecoveryConfidence(
                model,
                shedding);

            if (model.DeficitCleared)
            {
                model.RecoveryState =
                    ElectricalRecoveryState.DeficitCleared;

                model.Verification =
                    "Observed storage flow is nonnegative; previous electrical deficit is cleared.";
            }
            else if (!_hasBaseline)
            {
                model.RecoveryState =
                    ElectricalRecoveryState.None;

                model.Verification =
                    "No active recovery baseline.";
            }

            ResetBaselineIfHealthy(
                diagnostic,
                model);

            return model;
        }

        private void EnsureBaseline(
            DateTime receivedUtc,
            ElectricalFlowModel flow)
        {
            if (_hasBaseline ||
                flow == null ||
                !flow.HasMeasuredNetStorageRate ||
                flow.NetStorageRateEcPerSecond >=
                    -DeficitClearedToleranceEcPerSecond)
            {
                return;
            }

            _hasBaseline =
                true;

            _baselineStorageRate =
                flow.NetStorageRateEcPerSecond;

            _baselineUtc =
                receivedUtc;

            _hasImprovementSince =
                false;
        }

        private void EvaluateRecovery(
            DateTime receivedUtc,
            ElectricalFlowModel flow,
            ElectricalProcedureModel model)
        {
            if (!_hasBaseline)
            {
                model.RecoveryState =
                    ElectricalRecoveryState.None;

                model.Verification =
                    "No actionable deficit baseline has been established.";

                return;
            }

            if (flow == null ||
                !flow.HasMeasuredNetStorageRate)
            {
                PopulateBaseline(
                    model);

                model.RecoveryState =
                    ElectricalRecoveryState.Unobservable;

                model.Verification =
                    "Recovery cannot currently be verified from storage flow.";

                return;
            }

            double current =
                flow.NetStorageRateEcPerSecond;

            model.CurrentStorageRateObservable =
                true;

            model.CurrentStorageRateEcPerSecond =
                current;

            model.RecoveryIsObservedOnly =
                true;

            /*
             * Recovery baseline policy:
             *
             * The first actionable deficit establishes the baseline, but that
             * value is not frozen while the electrical condition is still
             * getting worse.
             *
             * If a later observable storage rate is materially MORE negative
             * than the stored baseline, move the baseline down to that worse
             * rate. This preserves the meaningful pre-recovery condition for
             * the eventual before/after comparison.
             *
             * Example:
             *   initial advisory  -1.6 EC/s
             *   heavy load       -11.1 EC/s  -> baseline updates
             *   worse load       -13.8 EC/s  -> baseline updates
             *   recovery          -4.0 EC/s  -> baseline stays -13.8
             *   housekeeping      -0.028 EC/s -> improvement ~13.8 EC/s
             */
            if (current <
                _baselineStorageRate -
                ImprovementThresholdEcPerSecond)
            {
                _baselineStorageRate =
                    current;

                _baselineUtc =
                    receivedUtc;

                _hasImprovementSince =
                    false;

                model.HasBaseline =
                    true;

                model.BaselineStorageRateEcPerSecond =
                    _baselineStorageRate;

                model.HasImprovement =
                    false;

                model.ImprovementEcPerSecond =
                    0.0;

                model.DeficitCleared =
                    false;

                model.RecoveryState =
                    ElectricalRecoveryState.Worsening;

                model.Verification =
                    "Observed storage deficit worsened; recovery baseline updated to the new worst rate.";

                return;
            }

            PopulateBaseline(
                model);

            model.ImprovementEcPerSecond =
                current -
                _baselineStorageRate;

            model.HasImprovement =
                model.ImprovementEcPerSecond >=
                ImprovementThresholdEcPerSecond;

            model.DeficitCleared =
                current >=
                -DeficitClearedToleranceEcPerSecond;

            if (model.DeficitCleared)
            {
                model.RecoveryState =
                    ElectricalRecoveryState.DeficitCleared;

                model.Verification =
                    "Observed electrical response cleared the storage deficit.";

                return;
            }

            if (model.HasImprovement)
            {
                if (!_hasImprovementSince)
                {
                    _hasImprovementSince =
                        true;

                    _improvementSinceUtc =
                        receivedUtc;
                }

                double duration =
                    (receivedUtc -
                     _improvementSinceUtc)
                        .TotalSeconds;

                if (duration >=
                    StableImprovementSeconds)
                {
                    model.RecoveryState =
                        ElectricalRecoveryState.StableImprovement;

                    model.Verification =
                        "Observed storage deficit has improved from the worst tracked baseline and remained improved for at least two seconds.";
                }
                else
                {
                    model.RecoveryState =
                        ElectricalRecoveryState.Improving;

                    model.Verification =
                        "Observed storage deficit is improving from the worst tracked baseline.";
                }

                return;
            }

            _hasImprovementSince =
                false;

            model.RecoveryState =
                ElectricalRecoveryState.BaselineEstablished;

            model.Verification =
                "Electrical recovery baseline established; awaiting a measurable improvement.";
        }

        private static void SetRecoveryConfidence(
            ElectricalProcedureModel model,
            ElectricalLoadSheddingModel shedding)
        {
            if (model == null)
            {
                return;
            }

            if (shedding == null ||
                !shedding.AnalysisAvailable)
            {
                model.RecoveryConfidence =
                    ElectricalRecoveryConfidence.Limited;

                return;
            }

            if (shedding.QuantifiedCandidateCount > 0)
            {
                model.RecoveryConfidence =
                    ElectricalRecoveryConfidence.High;

                return;
            }

            if (shedding.CandidateCount > 0)
            {
                model.RecoveryConfidence =
                    ElectricalRecoveryConfidence.Limited;

                return;
            }

            model.RecoveryConfidence =
                ElectricalRecoveryConfidence.Moderate;
        }

        private void PopulateBaseline(
            ElectricalProcedureModel model)
        {
            model.HasBaseline =
                _hasBaseline;

            if (_hasBaseline)
            {
                model.BaselineStorageRateEcPerSecond =
                    _baselineStorageRate;
            }
        }

        private void ResetBaselineIfHealthy(
            ElectricalPowerDiagnosticModel diagnostic)
        {
            ResetBaselineIfHealthy(
                diagnostic,
                null);
        }

        private void ResetBaselineIfHealthy(
            ElectricalPowerDiagnosticModel diagnostic,
            ElectricalProcedureModel model)
        {
            if (!_hasBaseline ||
                diagnostic == null)
            {
                return;
            }

            bool healthy =
                diagnostic.Severity ==
                    ElectricalPowerSeverity.Normal &&
                diagnostic.HasPowerMargin &&
                diagnostic.PowerMarginEcPerSecond >=
                    -DeficitClearedToleranceEcPerSecond;

            if (!healthy)
            {
                return;
            }

            if (model != null)
            {
                model.HasBaseline =
                    true;

                model.BaselineStorageRateEcPerSecond =
                    _baselineStorageRate;

                model.DeficitCleared =
                    true;
            }

            _hasBaseline =
                false;

            _hasImprovementSince =
                false;

            _baselineStorageRate =
                0.0;

            _baselineUtc =
                DateTime.MinValue;
        }
    }
}
