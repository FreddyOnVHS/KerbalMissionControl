using System;
using System.Collections.Generic;

namespace KMC.Engine.Electrical
{
    public enum ElectricalLoadSheddingState
    {
        Unavailable = 0,
        NotRequired,
        EvidenceIncomplete,
        CandidatesAvailable,
        QuantifiedRecoveryAvailable,
        InsufficientQuantifiedRecovery,
        StorageDepleted
    }

    public enum ElectricalLoadSheddingPriority
    {
        Protected = 0,
        Essential,
        Conditional,
        Preferred,
        First
    }

    public enum ElectricalLoadSheddingEvidence
    {
        Unknown = 0,
        PotentialOnly,
        QuantifiedCurrent
    }

    public sealed class ElectricalLoadSheddingCandidate
    {
        public ElectricalLoadSheddingCandidate()
        {
            PartTitle =
                string.Empty;

            Category =
                string.Empty;

            Reason =
                string.Empty;

            Priority =
                ElectricalLoadSheddingPriority.Conditional;

            Evidence =
                ElectricalLoadSheddingEvidence.Unknown;
        }

        public uint PartId { get; set; }

        public string PartTitle { get; set; }

        public string Category { get; set; }

        public ElectricalLoadSheddingPriority Priority { get; set; }

        public ElectricalLoadSheddingEvidence Evidence { get; set; }

        public bool Enabled { get; set; }

        public bool ActiveStateKnown { get; set; }

        public bool Active { get; set; }

        public bool CurrentRateKnown { get; set; }

        public double CurrentRateEcPerSecond { get; set; }

        public bool MaximumRateKnown { get; set; }

        public double MaximumRateEcPerSecond { get; set; }

        public bool IsProtected
        {
            get
            {
                return
                    Priority ==
                    ElectricalLoadSheddingPriority.Protected;
            }
        }

        public bool IsShedCandidate
        {
            get
            {
                return
                    Priority !=
                    ElectricalLoadSheddingPriority.Protected;
            }
        }

        public string Reason { get; set; }
    }

    /// <summary>
    /// Advisory-only load-shedding analysis.
    ///
    /// This model never commands KSP and never assumes a configured maximum
    /// rate is a guaranteed recoverable current load.
    /// </summary>
    public sealed class ElectricalLoadSheddingModel
    {
        public ElectricalLoadSheddingModel()
        {
            State =
                ElectricalLoadSheddingState.Unavailable;

            Summary =
                "Load shedding analysis unavailable.";

            Candidates =
                new List<ElectricalLoadSheddingCandidate>();
        }

        public ElectricalLoadSheddingState State { get; internal set; }

        public string Summary { get; internal set; }

        public bool AnalysisAvailable { get; internal set; }

        public bool SheddingRecommended { get; internal set; }

        public bool StorageDepleted { get; internal set; }

        public int ConsumerCount { get; internal set; }

        public int ProtectedConsumerCount { get; internal set; }

        public int CandidateCount { get; internal set; }

        public int QuantifiedCandidateCount { get; internal set; }

        public int PotentialOnlyCandidateCount { get; internal set; }

        /// <summary>
        /// Sum of current-known rates for non-protected candidates.
        /// This is the only recovery total KMC treats as quantified.
        /// </summary>
        public double QuantifiedRecoverableEcPerSecond { get; internal set; }

        /// <summary>
        /// Sum of declared maximum rates for non-protected candidates.
        /// This is capability/potential only and is not a guaranteed saving.
        /// </summary>
        public double PotentialMaximumRecoverableEcPerSecond { get; internal set; }

        public bool HasCurrentDeficit { get; internal set; }

        public double CurrentDeficitEcPerSecond { get; internal set; }

        public bool CanQuantifyPostShedMargin { get; internal set; }

        public double QuantifiedPostShedMarginEcPerSecond { get; internal set; }

        public bool QuantifiedRecoveryEliminatesDeficit { get; internal set; }

        public bool HasInferredDemand { get; internal set; }

        public double InferredDemandEcPerSecond { get; internal set; }

        public List<ElectricalLoadSheddingCandidate> Candidates
        {
            get;
            private set;
        }
    }

    internal static class ElectricalLoadSheddingAnalyzer
    {
        public static ElectricalLoadSheddingModel Analyze(
            ElectricalFlowModel flow,
            ElectricalLoadModel load,
            ElectricalAttributionModel attribution,
            ElectricalPowerDiagnosticModel diagnostic)
        {
            ElectricalLoadSheddingModel model =
                new ElectricalLoadSheddingModel();

            if (flow != null &&
                flow.State ==
                    ElectricalStorageFlowState.Depleted)
            {
                model.State =
                    ElectricalLoadSheddingState.StorageDepleted;

                model.StorageDepleted =
                    true;

                model.SheddingRecommended =
                    true;

                model.Summary =
                    "Electrical storage is depleted; load shedding may support recovery, but storage-flow demand is unobservable.";

                PopulateCandidates(
                    model,
                    attribution);

                return model;
            }

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                model.State =
                    ElectricalLoadSheddingState.Unavailable;

                model.Summary =
                    "Consumer attribution telemetry is unavailable.";

                return model;
            }

            model.AnalysisAvailable =
                true;

            if (load != null &&
                load.HasInferredTotalLoad)
            {
                model.HasInferredDemand =
                    true;

                model.InferredDemandEcPerSecond =
                    Math.Max(
                        0.0,
                        load.InferredTotalLoadEcPerSecond);
            }

            if (flow != null &&
                flow.HasMeasuredNetStorageRate &&
                flow.NetStorageRateEcPerSecond < 0.0)
            {
                model.HasCurrentDeficit =
                    true;

                model.CurrentDeficitEcPerSecond =
                    -flow.NetStorageRateEcPerSecond;
            }

            PopulateCandidates(
                model,
                attribution);

            bool diagnosticNeedsAction =
                diagnostic != null &&
                (diagnostic.Severity ==
                     ElectricalPowerSeverity.Advisory ||
                 diagnostic.Severity ==
                     ElectricalPowerSeverity.Warning ||
                 diagnostic.Severity ==
                     ElectricalPowerSeverity.Critical ||
                 diagnostic.Severity ==
                     ElectricalPowerSeverity.Blackout);

            /*
             * A measurable deficit is not automatically an operational
             * emergency. Long-duration housekeeping drain can be perfectly
             * acceptable. Recommend shedding only when the higher-level power
             * status has reached an actionable severity.
             */
            model.SheddingRecommended =
                model.HasCurrentDeficit &&
                diagnosticNeedsAction;

            if (!model.SheddingRecommended)
            {
                model.State =
                    ElectricalLoadSheddingState.NotRequired;

                model.Summary =
                    "No load-shedding action is currently indicated.";

                return model;
            }

            if (model.QuantifiedCandidateCount > 0)
            {
                model.CanQuantifyPostShedMargin =
                    flow != null &&
                    flow.HasMeasuredNetStorageRate;

                if (model.CanQuantifyPostShedMargin)
                {
                    model.QuantifiedPostShedMarginEcPerSecond =
                        flow.NetStorageRateEcPerSecond +
                        model.QuantifiedRecoverableEcPerSecond;

                    model.QuantifiedRecoveryEliminatesDeficit =
                        model.QuantifiedPostShedMarginEcPerSecond >=
                        -0.005;
                }

                if (model.QuantifiedRecoveryEliminatesDeficit)
                {
                    model.State =
                        ElectricalLoadSheddingState
                            .QuantifiedRecoveryAvailable;

                    model.Summary =
                        "Measured candidate loads are sufficient to eliminate the current storage deficit.";

                    return model;
                }

                model.State =
                    ElectricalLoadSheddingState
                        .InsufficientQuantifiedRecovery;

                model.Summary =
                    "Measured candidate loads do not fully eliminate the current storage deficit.";

                return model;
            }

            if (model.CandidateCount > 0)
            {
                if (model.PotentialOnlyCandidateCount > 0)
                {
                    model.State =
                        ElectricalLoadSheddingState
                            .EvidenceIncomplete;

                    model.Summary =
                        "Shed candidates exist, but their current recoverable load cannot be quantified from available telemetry.";

                    return model;
                }

                model.State =
                    ElectricalLoadSheddingState
                        .CandidatesAvailable;

                model.Summary =
                    "Load-shedding candidates are available.";

                return model;
            }

            model.State =
                ElectricalLoadSheddingState.EvidenceIncomplete;

            model.Summary =
                "Electrical deficit exists, but no non-protected shed candidates are currently identifiable.";

            return model;
        }

        private static void PopulateCandidates(
            ElectricalLoadSheddingModel model,
            ElectricalAttributionModel attribution)
        {
            if (model == null ||
                attribution == null)
            {
                return;
            }

            for (int index = 0;
                 index < attribution.Entries.Count;
                 index++)
            {
                ElectricalAttributionEntry source =
                    attribution.Entries[index];

                if (source == null ||
                    source.Kind !=
                        ElectricalAttributionKind.Consumer)
                {
                    continue;
                }

                model.ConsumerCount++;

                ElectricalLoadSheddingCandidate candidate =
                    new ElectricalLoadSheddingCandidate();

                candidate.PartId =
                    source.PartId;

                candidate.PartTitle =
                    source.PartTitle ??
                    string.Empty;

                candidate.Category =
                    source.Category ??
                    string.Empty;

                candidate.Enabled =
                    source.Enabled;

                candidate.ActiveStateKnown =
                    source.ActiveStateKnown;

                candidate.Active =
                    source.Active;

                candidate.CurrentRateKnown =
                    source.CurrentRateKnown;

                candidate.CurrentRateEcPerSecond =
                    source.CurrentRateKnown
                        ? Math.Max(
                            0.0,
                            source.CurrentRateEcPerSecond)
                        : 0.0;

                candidate.MaximumRateKnown =
                    source.MaximumRateKnown;

                candidate.MaximumRateEcPerSecond =
                    source.MaximumRateKnown
                        ? Math.Max(
                            0.0,
                            source.MaximumRateEcPerSecond)
                        : 0.0;

                ApplyPriorityPolicy(
                    candidate);

                if (candidate.IsProtected)
                {
                    model.ProtectedConsumerCount++;
                }
                else
                {
                    model.CandidateCount++;

                    if (candidate.CurrentRateKnown)
                    {
                        candidate.Evidence =
                            ElectricalLoadSheddingEvidence
                                .QuantifiedCurrent;

                        model.QuantifiedCandidateCount++;

                        model.QuantifiedRecoverableEcPerSecond +=
                            candidate.CurrentRateEcPerSecond;
                    }
                    else if (candidate.MaximumRateKnown)
                    {
                        candidate.Evidence =
                            ElectricalLoadSheddingEvidence
                                .PotentialOnly;

                        model.PotentialOnlyCandidateCount++;
                    }

                    if (candidate.MaximumRateKnown)
                    {
                        model.PotentialMaximumRecoverableEcPerSecond +=
                            candidate.MaximumRateEcPerSecond;
                    }
                }

                model.Candidates.Add(
                    candidate);
            }
        }

        private static void ApplyPriorityPolicy(
            ElectricalLoadSheddingCandidate candidate)
        {
            string category =
                candidate.Category ??
                string.Empty;

            if (EqualsIgnoreCase(
                    category,
                    "Command"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.Protected;

                candidate.Reason =
                    "Command/control load is protected by default.";

                return;
            }

            if (EqualsIgnoreCase(
                    category,
                    "AttitudeControl"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.Essential;

                candidate.Reason =
                    "Attitude control is operationally important; shed only if mission conditions permit.";

                return;
            }

            if (EqualsIgnoreCase(
                    category,
                    "Communication"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.Conditional;

                candidate.Reason =
                    "Communications may be shed temporarily if mission conditions permit.";

                return;
            }

            if (EqualsIgnoreCase(
                    category,
                    "Propulsion"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.Conditional;

                candidate.Reason =
                    "Propulsion-related electrical load is phase-dependent and should be shed only when inactive or nonessential.";

                return;
            }

            if (EqualsIgnoreCase(
                    category,
                    "Science"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.First;

                candidate.Reason =
                    "Science load is a preferred first-tier shedding candidate.";

                return;
            }

            if (EqualsIgnoreCase(
                    category,
                    "Utility"))
            {
                candidate.Priority =
                    ElectricalLoadSheddingPriority.Preferred;

                candidate.Reason =
                    "Utility load is a preferred shedding candidate when mission conditions permit.";

                return;
            }

            candidate.Priority =
                ElectricalLoadSheddingPriority.Conditional;

            candidate.Reason =
                "Consumer is a conditional shedding candidate until its operational role is better classified.";
        }

        private static bool EqualsIgnoreCase(
            string left,
            string right)
        {
            return
                string.Equals(
                    left,
                    right,
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
