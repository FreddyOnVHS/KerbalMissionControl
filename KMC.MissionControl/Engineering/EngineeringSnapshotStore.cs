using System;
using System.Diagnostics;
using KMC.Engine.Analysis;

namespace KMC.MissionControl.Engineering
{
    /// <summary>
    /// Thread-safe bridge between the background telemetry receiver and
    /// Mission Control consumers that need the latest engineering analysis.
    ///
    /// Milestone 7.1 deliberately exposes the complete pipeline result so
    /// diagnostics can confirm which engineering systems executed. UI pages
    /// should not perform engineering analysis themselves.
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
                throw new ArgumentNullException(nameof(result));
            }

            bool logSnapshot = false;
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
                    "KMC.Engine LIVE | Vessel="
                    + result.Snapshot.Vessel.VesselName
                    + " | Parts="
                    + result.Snapshot.Vessel.PartCount
                    + " | Stage="
                    + result.Snapshot.Vessel.CurrentStage
                    + " | TopologyRevision="
                    + topologyRevision
                    + " | Systems="
                    + string.Join(
                        ", ",
                        result.ExecutedSystems));
            }
        }

        public static bool TryGetLatest(
            out AnalysisPipelineResult result)
        {
            lock (SyncRoot)
            {
                result =
                    _latest;

                return result != null;
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
                "KMC.Engine ERROR | "
                + exception);
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
