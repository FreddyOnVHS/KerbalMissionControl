namespace KMC.MissionControl.Rendering.Propulsion
{
    /// <summary>
    /// Read-only diagnostic view of the propulsion analysis cache.
    /// </summary>
    public sealed class PropulsionAnalysisCacheSnapshot
    {
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public long RebuildCount { get; set; }

        public double LastRebuildMilliseconds { get; set; }
        public double AverageRebuildMilliseconds { get; set; }

        public long CachedTopologyRevision { get; set; }
        public int CachedStage { get; set; }
        public int CachedNodeCount { get; set; }

        public string CachedVesselName { get; set; }

        public bool HasCachedAnalysis { get; set; }
    }
}
