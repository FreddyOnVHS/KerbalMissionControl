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

            bool logSnapshot =
                false;

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
            }

            if (logSnapshot)
            {
                Debug.WriteLine(
                    "KMC.Engine LIVE | Vessel=" +
                    result.Snapshot.Vessel.VesselName +
                    " | Parts=" +
                    result.Snapshot.Vessel.PartCount +
                    " | Stage=" +
                    result.Snapshot.Vessel.CurrentStage +
                    " | TopologyRevision=" +
                    topologyRevision +
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
                _latest =
                    null;

                _publishedSnapshotCount =
                    0;

                _lastLoggedTopologyRevision =
                    long.MinValue;

                _lastError =
                    string.Empty;
            }
        }
    }
}
