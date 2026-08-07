using System;
using System.Diagnostics;
using KMC.Engine.Analysis;
using KMC.Engine.Electrical;

namespace KMC.MissionControl.Engineering
{
    public static class EngineeringSnapshotStore
    {
        private static readonly object SyncRoot =
            new object();

        private static AnalysisPipelineResult _latest;
        private static long _publishedSnapshotCount;
        private static long _lastLoggedTopologyRevision =
            long.MinValue;
        private static DateTime _lastFlowLogUtc =
            DateTime.MinValue;
        private static string _lastError =
            string.Empty;

        public static long PublishedSnapshotCount
        {
            get
            {
                lock (SyncRoot)
                {
                    return _publishedSnapshotCount;
                }
            }
        }

        public static string LastError
        {
            get
            {
                lock (SyncRoot)
                {
                    return _lastError;
                }
            }
        }

        public static void Publish(
            AnalysisPipelineResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(
                    nameof(result));
            }

            bool logSnapshot = false;
            bool logFlow = false;
            DateTime nowUtc = DateTime.UtcNow;

            long topologyRevision =
                result.Snapshot.Vessel.TopologyRevision;

            lock (SyncRoot)
            {
                _latest =
                    result;

                _publishedSnapshotCount++;
                _lastError =
                    string.Empty;

                if (topologyRevision !=
                    _lastLoggedTopologyRevision)
                {
                    _lastLoggedTopologyRevision =
                        topologyRevision;

                    logSnapshot =
                        true;
                }

                if ((nowUtc -
                     _lastFlowLogUtc)
                        .TotalSeconds >=
                    1.0)
                {
                    _lastFlowLogUtc =
                        nowUtc;

                    logFlow =
                        true;
                }
            }

            if (logSnapshot)
            {
                WriteTopologyDiagnostics(
                    result);
            }

            if (logFlow)
            {
                WriteFlowDiagnostic(
                    result);

                WriteAttributionDiagnostic(
                    result);

                WriteLoadDiagnostic(
                    result);

                WritePowerStatusDiagnostic(
                    result);

                WriteLoadSheddingDiagnostic(
                    result);
            }
        }

        private static void WriteTopologyDiagnostics(
            AnalysisPipelineResult result)
        {
            Debug.WriteLine(
                "KMC.Engine LIVE | Vessel=" +
                result.Snapshot.Vessel.VesselName +
                " | Parts=" +
                result.Snapshot.Vessel.PartCount +
                " | Stage=" +
                result.Snapshot.Vessel.CurrentStage +
                " | TopologyRevision=" +
                result.Snapshot.Vessel.TopologyRevision +
                " | Systems=" +
                string.Join(
                    ", ",
                    result.ExecutedSystems));

            Debug.WriteLine(
                "KMC.Engine CAPABILITIES | " +
                result.Snapshot.Capabilities.CreateSummary() +
                " | ClassifiedParts=" +
                result.Snapshot.Capabilities.ClassifiedPartCount +
                " | UnclassifiedParts=" +
                result.Snapshot.Capabilities.UnclassifiedPartCount);

            Debug.WriteLine(
                "KMC.Engine CAPABILITY SOURCE | EngineOwned | DetailedParts=" +
                result.Snapshot.Capabilities.Details.Parts.Count);

            ElectricalNetwork electrical =
                result.Snapshot.Power.ElectricalNetwork;

            Debug.WriteLine(
                "KMC.Engine ELECTRICAL | Vessel=" +
                electrical.VesselName +
                " | Nodes=" +
                electrical.Nodes.Count +
                " | BusMembers=" +
                electrical.BusMemberships.Count +
                " | StructuralParts=" +
                electrical.StructuralPartCount +
                " | StructuralLinks=" +
                electrical.StructuralConnections.Count +
                " | Sources=" +
                electrical.SourceNodeCount +
                " | Storage=" +
                electrical.StorageNodeCount +
                " | Consumers=" +
                electrical.ConsumerNodeCount +
                " | ExplicitConsumers=" +
                electrical.ExplicitConsumerNodeCount +
                " | PotentialConsumers=" +
                electrical.PotentialConsumerNodeCount +
                " | StoredEC=" +
                electrical.StoredElectricCharge.ToString("0.###") +
                "/" +
                electrical.ElectricChargeCapacity.ToString("0.###"));

            ElectricalStorageModel storage =
                electrical.Storage;

            Debug.WriteLine(
                "KMC.Engine STORAGE | Parts=" +
                storage.Parts.Count +
                " | Sections=" +
                storage.StageSections.Count +
                " | Branches=" +
                storage.BranchSections.Count +
                " | EC=" +
                storage.StoredEc.ToString("0.###") +
                "/" +
                storage.CapacityEc.ToString("0.###") +
                " | Charge=" +
                storage.ChargePercent.ToString("0.0") +
                "% | NextStage=" +
                storage.NextStage +
                " | LoseEC=" +
                storage.NextStageLostStoredEc.ToString("0.###") +
                "/" +
                storage.NextStageLostCapacityEc.ToString("0.###") +
                " | RemainEC=" +
                storage.NextStageRemainingStoredEc.ToString("0.###") +
                "/" +
                storage.NextStageRemainingCapacityEc.ToString("0.###") +
                " | LoseAll=" +
                storage.LosesAllStorageOnNextStage);
        }

        private static void WriteFlowDiagnostic(
            AnalysisPipelineResult result)
        {
            ElectricalFlowModel flow =
                result.Snapshot.Power.Flow;

            if (flow == null ||
                !flow.TelemetryAvailable)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER FLOW | WaitingForSystemsTelemetry");

                return;
            }

            string rate =
                flow.HasMeasuredNetStorageRate
                    ? flow.NetStorageRateEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "--";

            string toEmpty =
                flow.HasEstimatedSecondsToEmpty
                    ? FormatDuration(
                        flow.EstimatedSecondsToEmpty)
                    : "--";

            string toFull =
                flow.HasEstimatedSecondsToFull
                    ? FormatDuration(
                        flow.EstimatedSecondsToFull)
                    : "--";

            Debug.WriteLine(
                "KMC.Engine POWER FLOW | LiveEC=" +
                flow.StoredEc.ToString("0.###") +
                "/" +
                flow.CapacityEc.ToString("0.###") +
                " | Charge=" +
                flow.ChargePercent.ToString("0.0") +
                "% | State=" +
                flow.State +
                " | NetStorageRate=" +
                rate +
                " | Window=" +
                flow.WindowSeconds.ToString("0.0") +
                "s/" +
                flow.SampleCount +
                " samples" +
                " | ToEmpty=" +
                toEmpty +
                " | ToFull=" +
                toFull +
                " | AtCapacity=" +
                flow.IsAtCapacity +
                " | Depleted=" +
                flow.IsDepleted);
        }

        private static void WriteAttributionDiagnostic(
            AnalysisPipelineResult result)
        {
            ElectricalAttributionModel attribution =
                result.Snapshot.Power.Attribution;

            if (attribution == null ||
                !attribution.TelemetryAvailable)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER ATTRIBUTION | Telemetry=WAITING");

                return;
            }

            Debug.WriteLine(
                "KMC.Engine POWER ATTRIBUTION | Telemetry=LIVE | Producers=" +
                attribution.ProducerCount +
                " | Consumers=" +
                attribution.ConsumerCount +
                " | KnownGeneration=" +
                attribution.KnownCurrentGenerationEcPerSecond.ToString("0.###") +
                " EC/s (" +
                attribution.KnownCurrentProducerCount +
                "/" +
                attribution.ProducerCount +
                ")" +
                " | KnownConsumption=" +
                attribution.KnownCurrentConsumptionEcPerSecond.ToString("0.###") +
                " EC/s (" +
                attribution.KnownCurrentConsumerCount +
                "/" +
                attribution.ConsumerCount +
                ")" +
                " | KnownBalance=" +
                attribution.KnownCurrentBalanceEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | MaxGeneration=" +
                attribution.DeclaredMaximumGenerationEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | MaxConsumption=" +
                attribution.DeclaredMaximumConsumptionEcPerSecond.ToString("0.###") +
                " EC/s");
        }

        private static void WriteLoadDiagnostic(
            AnalysisPipelineResult result)
        {
            ElectricalLoadModel load =
                result.Snapshot.Power.Load;

            if (load == null)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER LOAD | State=Unavailable");

                return;
            }

            if (load.State ==
                ElectricalLoadInferenceState.StorageDepleted)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER LOAD | State=StorageDepleted" +
                    " | TotalDemand=UNKNOWN" +
                    " | StorageRate=UNOBSERVABLE" +
                    " | Attributed=" +
                    load.AttributedCurrentLoadEcPerSecond.ToString("0.###") +
                    " EC/s" +
                    " | Coverage=UNKNOWN");

                return;
            }

            if (!load.HasInferredTotalLoad)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER LOAD | State=" +
                    load.State);

                return;
            }

            Debug.WriteLine(
                "KMC.Engine POWER LOAD | State=" +
                load.State +
                " | Generation=" +
                load.GenerationEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | StorageRate=" +
                load.StorageRateEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | TotalDemand=" +
                load.InferredTotalLoadEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | Attributed=" +
                load.AttributedCurrentLoadEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | Unattributed=" +
                load.UnattributedLoadEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | Coverage=" +
                load.AttributionCoveragePercent.ToString("0.0") +
                "%" +
                " | NoSourceProof=" +
                load.GenerationDerivedFromNoSources +
                " | AttributionConflict=" +
                load.AttributionExceedsInferredLoad);
        }

        private static void WritePowerStatusDiagnostic(
            AnalysisPipelineResult result)
        {
            ElectricalPowerDiagnosticModel status =
                result.Snapshot.Power.Diagnostic;

            if (status == null)
            {
                Debug.WriteLine(
                    "KMC.Engine POWER STATUS | Severity=Unknown | Condition=Unknown");

                return;
            }

            string endurance =
                status.HasEndurance
                    ? FormatDuration(
                        status.EnduranceSeconds)
                    : "--";

            string margin =
                status.HasPowerMargin
                    ? status.PowerMarginEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "--";

            string demand =
                status.HasInferredDemand
                    ? status.InferredDemandEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "UNKNOWN";

            string coverage =
                status.AttributionCoverageKnown
                    ? status.AttributionCoveragePercent.ToString("0.0") +
                      "%"
                    : "UNKNOWN";

            Debug.WriteLine(
                "KMC.Engine POWER STATUS | Severity=" +
                status.Severity +
                " | Condition=" +
                status.Condition +
                " | Reserve=" +
                status.ReservePercent.ToString("0.0") +
                "%" +
                " | Endurance=" +
                endurance +
                " | Margin=" +
                margin +
                " | Demand=" +
                demand +
                " | DemandObservability=" +
                status.DemandObservability +
                " | AttributionCoverage=" +
                coverage +
                " | NextStageStorageLoss=" +
                status.NextStageLosesStorage +
                " | NextStageLoseAll=" +
                status.NextStageLosesAllStorage +
                " | Summary=" +
                status.Summary);
        }

        private static void WriteLoadSheddingDiagnostic(
            AnalysisPipelineResult result)
        {
            ElectricalLoadSheddingModel shedding =
                result.Snapshot.Power.LoadShedding;

            if (shedding == null)
            {
                Debug.WriteLine(
                    "KMC.Engine LOAD SHEDDING | State=Unavailable");

                return;
            }

            string deficit =
                shedding.HasCurrentDeficit
                    ? shedding.CurrentDeficitEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "--";

            string postMargin =
                shedding.CanQuantifyPostShedMargin
                    ? shedding.QuantifiedPostShedMarginEcPerSecond.ToString("0.###") +
                      " EC/s"
                    : "UNKNOWN";

            Debug.WriteLine(
                "KMC.Engine LOAD SHEDDING | State=" +
                shedding.State +
                " | Recommended=" +
                shedding.SheddingRecommended +
                " | Consumers=" +
                shedding.ConsumerCount +
                " | Protected=" +
                shedding.ProtectedConsumerCount +
                " | Candidates=" +
                shedding.CandidateCount +
                " | Quantified=" +
                shedding.QuantifiedCandidateCount +
                " | PotentialOnly=" +
                shedding.PotentialOnlyCandidateCount +
                " | Deficit=" +
                deficit +
                " | QuantifiedRecovery=" +
                shedding.QuantifiedRecoverableEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | PotentialMax=" +
                shedding.PotentialMaximumRecoverableEcPerSecond.ToString("0.###") +
                " EC/s" +
                " | PostShedMargin=" +
                postMargin +
                " | EliminatesDeficit=" +
                shedding.QuantifiedRecoveryEliminatesDeficit +
                " | Summary=" +
                shedding.Summary);
        }

        private static string FormatDuration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds) ||
                seconds < 0.0)
            {
                return "--";
            }

            TimeSpan time =
                TimeSpan.FromSeconds(
                    seconds);

            if (time.TotalHours >= 1.0)
            {
                return
                    ((int)time.TotalHours).ToString("00") +
                    ":" +
                    time.Minutes.ToString("00") +
                    ":" +
                    time.Seconds.ToString("00");
            }

            return
                time.Minutes.ToString("00") +
                ":" +
                time.Seconds.ToString("00");
        }

        public static bool TryGetLatest(
            out AnalysisPipelineResult result)
        {
            lock (SyncRoot)
            {
                result =
                    _latest;

                return
                    result != null;
            }
        }

        public static void ReportError(
            Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            lock (SyncRoot)
            {
                _lastError =
                    exception.ToString();
            }

            Debug.WriteLine(
                "KMC.Engine ERROR | " +
                exception);
        }

        public static void Clear()
        {
            lock (SyncRoot)
            {
                _latest = null;
                _publishedSnapshotCount = 0;
                _lastLoggedTopologyRevision = long.MinValue;
                _lastFlowLogUtc = DateTime.MinValue;
                _lastError = string.Empty;
            }
        }
    }
}
