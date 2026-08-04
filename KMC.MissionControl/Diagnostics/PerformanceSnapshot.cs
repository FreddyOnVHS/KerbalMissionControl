using System.Drawing;

namespace KMC.MissionControl.Diagnostics
{
    public sealed class PerformanceSnapshot
    {
        public int SelectedDisplayFps { get; set; }

        public long PacketsReceived { get; set; }
        public long PacketsDisplayed { get; set; }
        public long PacketsSuperseded { get; set; }

        public long RenderCount { get; set; }
        public double LastRenderMilliseconds { get; set; }
        public double AverageRenderMilliseconds { get; set; }

        public long PaintCount { get; set; }
        public double LastPaintMilliseconds { get; set; }
        public double AveragePaintMilliseconds { get; set; }

        public Size BitmapSize { get; set; }
        public long BitmapBytes { get; set; }
        public long BitmapAllocationCount { get; set; }

        public long ManagedMemoryBytes { get; set; }

        public int GenerationZeroCollections { get; set; }
        public int GenerationOneCollections { get; set; }
        public int GenerationTwoCollections { get; set; }

        public bool RenderingSuspended { get; set; }
        public bool LinkOnline { get; set; }

        public long PropulsionCacheHits { get; set; }
        public long PropulsionCacheMisses { get; set; }
        public long PropulsionCacheRebuilds { get; set; }

        public double PropulsionCacheLastRebuildMilliseconds
        {
            get;
            set;
        }

        public double PropulsionCacheAverageRebuildMilliseconds
        {
            get;
            set;
        }

        public long PropulsionCachedTopologyRevision
        {
            get;
            set;
        }

        public int PropulsionCachedStage { get; set; }
        public int PropulsionCachedNodeCount { get; set; }

        public string PropulsionCachedVesselName
        {
            get;
            set;
        }

        public bool HasPropulsionCache { get; set; }
    }
}
