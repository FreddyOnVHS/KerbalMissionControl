using System;
using System.Diagnostics;

namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Caches topology-dependent propulsion analysis.
    ///
    /// PropulsionSystemModelBuilder and EngineClusterProjector are relatively
    /// expensive and depend on vessel topology rather than rapidly changing
    /// telemetry. This cache rebuilds those objects only when the graph
    /// identity or topology key changes.
    /// </summary>
    public sealed class PropulsionAnalysisCache
    {
        private static readonly PropulsionAnalysisCache
            SharedInstance =
                new PropulsionAnalysisCache();

        private readonly object _syncRoot =
            new object();

        private readonly PropulsionSystemModelBuilder
            _systemBuilder =
                new PropulsionSystemModelBuilder();

        private readonly EngineClusterProjector
            _clusterProjector =
                new EngineClusterProjector();

        private PropulsionRenderGraph _cachedGraph;
        private PropulsionAnalysis _cachedAnalysis;

        private long _cachedRevision;
        private int _cachedStage;
        private int _cachedNodeCount;
        private string _cachedVesselName;

        private long _hitCount;
        private long _missCount;
        private long _rebuildCount;

        private double _lastRebuildMilliseconds;
        private double _averageRebuildMilliseconds;

        private PropulsionAnalysisCache()
        {
            _cachedRevision =
                -1;

            _cachedStage =
                -1;

            _cachedNodeCount =
                -1;

            _cachedVesselName =
                string.Empty;
        }

        public static PropulsionAnalysis GetOrBuild(
            PropulsionRenderGraph graph)
        {
            return SharedInstance.GetOrBuildInternal(
                graph);
        }

        public static void Clear()
        {
            SharedInstance.ClearInternal();
        }

        public static PropulsionAnalysisCacheSnapshot
            GetSnapshot()
        {
            return SharedInstance.GetSnapshotInternal();
        }

        private PropulsionAnalysis GetOrBuildInternal(
            PropulsionRenderGraph graph)
        {
            if (graph == null)
            {
                ClearInternal();

                return new PropulsionAnalysis(
                    new PropulsionSystemModel(),
                    new EngineClusterProjection(),
                    -1,
                    -1,
                    string.Empty);
            }

            lock (_syncRoot)
            {
                string vesselName =
                    graph.VesselName ??
                    string.Empty;

                int nodeCount =
                    graph.Nodes != null
                        ? graph.Nodes.Count
                        : 0;

                bool cacheMatches =
                    _cachedAnalysis != null &&
                    ReferenceEquals(
                        _cachedGraph,
                        graph) &&
                    _cachedRevision ==
                        graph.TopologyRevision &&
                    _cachedStage ==
                        graph.CurrentStage &&
                    _cachedNodeCount ==
                        nodeCount &&
                    string.Equals(
                        _cachedVesselName,
                        vesselName,
                        StringComparison.Ordinal);

                if (cacheMatches)
                {
                    _hitCount++;
                    return _cachedAnalysis;
                }

                _missCount++;

                Stopwatch stopwatch =
                    Stopwatch.StartNew();

                PropulsionSystemModel system =
                    _systemBuilder.Build(
                        graph);

                EngineClusterProjection cluster =
                    _clusterProjector.Build(
                        graph);

                stopwatch.Stop();

                _cachedGraph =
                    graph;

                _cachedRevision =
                    graph.TopologyRevision;

                _cachedStage =
                    graph.CurrentStage;

                _cachedNodeCount =
                    nodeCount;

                _cachedVesselName =
                    vesselName;

                _cachedAnalysis =
                    new PropulsionAnalysis(
                        system,
                        cluster,
                        _cachedRevision,
                        _cachedStage,
                        _cachedVesselName);

                _rebuildCount++;

                _lastRebuildMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;

                _averageRebuildMilliseconds =
                    UpdateRunningAverage(
                        _averageRebuildMilliseconds,
                        _lastRebuildMilliseconds,
                        _rebuildCount);

                return _cachedAnalysis;
            }
        }

        private void ClearInternal()
        {
            lock (_syncRoot)
            {
                _cachedGraph =
                    null;

                _cachedAnalysis =
                    null;

                _cachedRevision =
                    -1;

                _cachedStage =
                    -1;

                _cachedNodeCount =
                    -1;

                _cachedVesselName =
                    string.Empty;
            }
        }

        private PropulsionAnalysisCacheSnapshot
            GetSnapshotInternal()
        {
            lock (_syncRoot)
            {
                return new PropulsionAnalysisCacheSnapshot
                {
                    HitCount =
                        _hitCount,

                    MissCount =
                        _missCount,

                    RebuildCount =
                        _rebuildCount,

                    LastRebuildMilliseconds =
                        _lastRebuildMilliseconds,

                    AverageRebuildMilliseconds =
                        _averageRebuildMilliseconds,

                    CachedTopologyRevision =
                        _cachedRevision,

                    CachedStage =
                        _cachedStage,

                    CachedNodeCount =
                        _cachedNodeCount,

                    CachedVesselName =
                        _cachedVesselName,

                    HasCachedAnalysis =
                        _cachedAnalysis != null
                };
            }
        }

        private static double UpdateRunningAverage(
            double currentAverage,
            double sample,
            long sampleCount)
        {
            if (sampleCount <= 1)
            {
                return sample;
            }

            return
                currentAverage +
                (sample -
                 currentAverage) /
                sampleCount;
        }
    }
}
