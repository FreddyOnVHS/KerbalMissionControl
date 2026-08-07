using System;
using System.Diagnostics;
using KMC.Engine.Analysis;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Thread-safe bridge between the background telemetry receiver and
    /// Mission Control consumers that need the latest engineering analysis.
    ///
    /// The complete pipeline result remains exposed while Engine subsystems
    /// are migrated and verified in parallel with existing Mission Control
    /// analysis.
    /// </summary>
    public static class EngineeringSnapshotStore
    {
        private static readonly object SyncRoot =
            new object();

        private static AnalysisPipelineResult _latest;
        private static long _publishedSnapshotCount;
        private static long _lastLoggedTopologyRevision = long.MinValue;
        private static string _lastError = string.Empty;

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
                    CapabilityComparison.Compare(
                        result.Snapshot.Vessel.Topology,
                        result.Snapshot.Capabilities));
            }
        }

        public static bool TryGetLatest(
            out AnalysisPipelineResult result)
        {
            lock (SyncRoot)
            {
                result =
                    _latest;

                return result !=
                    null;
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
