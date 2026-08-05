using System.Drawing;

namespace KMC.MissionControl.Cards
{
    public sealed class CardDiagnosticSnapshot
    {
        public string Id { get; set; }

        public Rectangle Bounds { get; set; }

        public CardDirtyState DirtyStateBeforeDraw
        {
            get;
            set;
        }

        public long DrawCount { get; set; }

        public long PresentationCount { get; set; }

        public long CacheHitCount { get; set; }

        public long BitmapAllocationCount { get; set; }

        public long CachedBitmapBytes { get; set; }

        public double LastDrawMilliseconds { get; set; }

        public double AverageDrawMilliseconds { get; set; }
    }
}
